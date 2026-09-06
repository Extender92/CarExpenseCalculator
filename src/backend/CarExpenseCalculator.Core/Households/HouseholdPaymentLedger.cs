namespace CarExpenseCalculator.Core.Households;

// Calculation-local amounts. Public rows are built only after every aggregate/budget.
internal sealed class HouseholdPaymentLedger(HouseholdCostContext context, string path)
{
    private readonly List<PaymentSource> sources = [];
    private static readonly HashSet<string> OperatingCategories = ["energy", "tax", "insurance", "service", "repairs", "customCosts"];

    public void Add(string key, string category, string label, CostSection amount, IReadOnlyList<int>? months,
        bool isEstimate = false, HouseholdPaymentDirection direction = HouseholdPaymentDirection.Outflow,
        bool mayAffectStartup = false, bool mayAffectOngoing = true, bool distribute = false)
    {
        var source = new PaymentSource(key, category, label, direction, isEstimate, mayAffectStartup, mayAffectOngoing);
        if (months is null)
        {
            source.Unscheduled.Merge(amount);
            source.Total.CopyProblems(amount);
            source.Total.HasDetails = amount.HasKnown;
        }
        else
        {
            decimal distributed = 0m;
            for (var index = 0; index < months.Count; index++)
            {
                var payment = new CostSection(key);
                payment.CopyProblems(amount);
                if (distribute)
                {
                    payment.AddTransformed(amount, value => index == months.Count - 1 ? value - distributed : value / months.Count);
                    if (payment.Known is { } known) distributed += known;
                }
                else payment.AddTransformed(amount, value => value);
                source.ByMonth.Add(months[index], payment);
                source.Total.Merge(payment);
            }
        }
        sources.Add(source);
    }

    public void AddCostItem(HouseholdCostItem item, string itemPath, string category)
    {
        var amount = new CostSection(itemPath);
        var months = ItemMonths(item.Cadence, item.MonthOffset, item.DueMonthOfYear, itemPath, amount);
        if (months is not { Count: 0 })
        {
            var value = context.Value(item.AmountSek, $"{itemPath}.amountSek", amount);
            if (value is not null) amount.Add(value.Value);
        }
        Add(itemPath, category, item.Label.Trim(), amount, months,
            item.AmountSek?.Single is null && item.AmountSek is not null,
            mayAffectStartup: item.Cadence is null or HouseholdCostCadence.Once,
            mayAffectOngoing: item.Cadence != HouseholdCostCadence.Once || item.MonthOffset != 0);
    }

    public IReadOnlyList<int>? ItemMonths(HouseholdCostCadence? cadence, int? offset, int? dueMonth,
        string itemPath, CostSection problems)
    {
        var cadenceValid = context.Available($"{itemPath}.cadence", cadence is not null, problems);
        // Month zero does not depend on an ownership horizon.
        if (cadenceValid && cadence == HouseholdCostCadence.Once && offset == 0
            && context.Available($"{itemPath}.monthOffset", true, problems)) return [0];
        var period = context.Period(problems);
        if (!cadenceValid) return null;
        if (cadence == HouseholdCostCadence.Once)
        {
            var valid = context.Available($"{itemPath}.monthOffset", offset is not null, problems);
            return valid && period is not null ? offset <= period ? [offset!.Value] : [] : null;
        }
        if (cadence == HouseholdCostCadence.Monthly)
            return period is not null ? Enumerable.Range(1, period.Value).ToArray() : null;
        var dueValid = context.Available($"{itemPath}.dueMonthOfYear", dueMonth is not null, problems);
        var startValid = context.Available("profile.startMonth", context.Profile.StartMonth is not null, problems);
        if (period is null || !dueValid || !startValid) return null;
        return Enumerable.Range(1, period.Value)
            .Where(month => CalendarAt(context.Profile.StartMonth!.Value, month).Month == dueMonth).ToArray();
    }

    public void AddMissingCategory(string category, string categoryPath)
    {
        var unknown = new CostSection(categoryPath);
        unknown.Missing.Add(categoryPath);
        Add(categoryPath, category, category, unknown, null, mayAffectStartup: true);
    }

    public CostSection Aggregate(string name, Func<PaymentSource, bool> include,
        Func<int, bool>? monthFilter = null, Func<PaymentSource, bool>? includeUnscheduled = null)
    {
        var total = CostSection.Zero($"{path}.{name}");
        foreach (var source in sources.Where(include))
        {
            if (monthFilter is null) total.Merge(source.Total);
            else
            {
                foreach (var payment in source.ByMonth.Where(pair => monthFilter(pair.Key))) total.Merge(payment.Value);
                if (source.Unscheduled.HasKnown || source.Unscheduled.Missing.Count > 0 || source.Unscheduled.Errors.Count > 0)
                    if (includeUnscheduled?.Invoke(source) != false) total.CopyProblems(source.Unscheduled);
            }
        }
        return total;
    }

    public (HouseholdPaymentCalendar Calendar, HouseholdBudgetResult Startup, HouseholdBudgetResult Monthly,
        HouseholdCashReconciliation Reconciliation) Finish(CostSection operating, CostSection depreciation,
        CostSection allowance, CostSection withheld, CostSection ownership, bool isLease)
    {
        var periodProblems = new CostSection($"{path}.coverage");
        var requested = context.RequestedPeriod(periodProblems);
        var covered = context.Period(periodProblems);
        var outflow = Aggregate("externalOutflow", source => source.Direction == HouseholdPaymentDirection.Outflow);
        var inflow = Aggregate("externalInflow", source => source.Direction == HouseholdPaymentDirection.Inflow);
        var saving = Aggregate("internalSaving", source => source.Direction == HouseholdPaymentDirection.InternalSaving);
        outflow.CopyProblems(periodProblems);
        saving.CopyProblems(periodProblems);
        if (isLease && requested > covered)
        {
            outflow.Missing.Add($"{path}.uncoveredMonths");
            saving.Missing.Add($"{path}.uncoveredMonths");
        }
        var net = CostSection.Zero($"{path}.netExternalCashFlow");
        net.Merge(outflow);
        net.Subtract(inflow);
        var calendarStatus = CostSection.Zero($"{path}.calendar");
        calendarStatus.Merge(net);
        calendarStatus.CopyProblems(saving);
        var startValid = context.Available("profile.startMonth", context.Profile.StartMonth is not null, calendarStatus);
        var months = new List<HouseholdPaymentMonth>();
        for (var month = 0; month <= (covered ?? 0); month++)
        {
            var offset = month;
            var groups = sources.Where(source => source.ByMonth.ContainsKey(offset))
                .GroupBy(source => (source.Category, source.Direction)).Select(group => new HouseholdPaymentCategoryResult(
                    group.Key.Category, group.Key.Direction,
                    Aggregate($"months[{offset}]", source => source.Category == group.Key.Category && source.Direction == group.Key.Direction,
                        value => value == offset, _ => false).Result())).ToArray();
            CostSectionResult Sum(HouseholdPaymentDirection direction) =>
                Aggregate($"months[{offset}]", source => source.Direction == direction, value => value == offset,
                    source => offset == 0 ? source.MayAffectStartup : source.MayAffectOngoing).Result();
            months.Add(new(offset, offset > 0 && startValid ? CalendarAt(context.Profile.StartMonth!.Value, offset) : null,
                Sum(HouseholdPaymentDirection.Outflow), Sum(HouseholdPaymentDirection.Inflow), Sum(HouseholdPaymentDirection.InternalSaving),
                Array.AsReadOnly(groups)));
        }

        var startup = Aggregate("startupFunding", source => source.Direction == HouseholdPaymentDirection.Outflow && source.Category != "purchaseCash",
            month => month == 0, source => source.MayAffectStartup);
        var ongoing = Aggregate("ongoingFunding", source => source.Direction is HouseholdPaymentDirection.Outflow or HouseholdPaymentDirection.InternalSaving,
            month => month > 0, source => source.MayAffectOngoing);
        ongoing.CopyProblems(periodProblems);
        if (isLease && requested > covered) ongoing.Missing.Add($"{path}.uncoveredMonths");
        var average = new CostSection($"{path}.monthlyFunding");
        average.CopyProblems(ongoing);
        if (requested is not null)
        {
            if (ongoing.Known is not null) average.AddTransformed(ongoing, Divide);
            else
            {
                // A period sum may overflow even though its mean is representable.
                // Remove only that aggregate's error; individual errors still apply.
                average.Errors.RemoveAll(error => error.Path == $"{path}.ongoingFunding" && error.Code == "calculationOutOfRange");
                foreach (var source in sources.Where(source => source.Direction is HouseholdPaymentDirection.Outflow or HouseholdPaymentDirection.InternalSaving))
                {
                    var contribution = CostSection.Zero($"{path}.monthlyFunding");
                    foreach (var payment in source.ByMonth.Where(pair => pair.Key > 0))
                        contribution.Merge(payment.Value);
                    average.CopyProblems(contribution);
                    average.AddTransformed(contribution, Divide);
                }
            }

            decimal Divide(decimal value)
            {
                var divided = value / requested.Value;
                if (value > 0m && divided == 0m) throw new OverflowException("Positive monthly funding is below decimal precision.");
                return divided;
            }
        }

        var cash = Aggregate("purchaseCash", source => source.Category == "purchaseCash");
        var principal = Aggregate("principalRepaid", source => source.Category == "principal");
        var paidOperating = Aggregate("paidOperating", source => OperatingCategories.Contains(source.Category));
        var deposit = Aggregate("depositPaid", source => source.Category == "deposit");
        var refund = Aggregate("depositRefund", source => source.Category == "depositRefund");
        var reconciled = CostSection.Zero($"{path}.reconciledOwnershipCost");
        reconciled.Merge(net);
        reconciled.Subtract(cash);
        reconciled.Subtract(principal);
        reconciled.Merge(depreciation);
        reconciled.Merge(operating);
        reconciled.Subtract(paidOperating);
        reconciled.Subtract(deposit);
        reconciled.Merge(refund);
        reconciled.Merge(withheld);
        reconciled.Merge(allowance);
        reconciled.CopyProblems(ownership);

        var calendar = new HouseholdPaymentCalendar(requested, covered, calendarStatus.Result(), months.AsReadOnly(),
            Array.AsReadOnly(sources.Select(source => source.Result()).ToArray()), outflow.Result(), inflow.Result(), net.Result(), saving.Result());
        var reconciliation = new HouseholdCashReconciliation(cash.Result(), principal.Result(),
            isLease ? CostSection.NotApplicable() : depreciation.Result(), operating.Result(), paidOperating.Result(),
            isLease ? deposit.Result() : CostSection.NotApplicable(), isLease ? refund.Result() : CostSection.NotApplicable(),
            isLease ? withheld.Result() : CostSection.NotApplicable(), allowance.Result(), reconciled.Result());
        return (calendar, Budget(context.Profile.StartupBudgetSek, "profile.startupBudgetSek", startup),
            Budget(context.Profile.MonthlyBudgetSek, "profile.monthlyBudgetSek", average), reconciliation);
    }

    private HouseholdBudgetResult Budget(decimal? limit, string limitPath, CostSection funding)
    {
        var validation = new CostSection(limitPath);
        var valid = context.Available(limitPath, true, validation);
        var status = !valid ? HouseholdBudgetStatus.Invalid : limit is null ? HouseholdBudgetStatus.NotConfigured
            : funding.Known > limit ? HouseholdBudgetStatus.Exceeded
            : funding.Errors.Count > 0 ? HouseholdBudgetStatus.Invalid
            : funding.IsComplete ? HouseholdBudgetStatus.WithinLimit : HouseholdBudgetStatus.Unknown;
        var result = new CostSection(limitPath);
        result.Merge(funding);
        result.CopyProblems(validation);
        return new(limit, status, result.Result());
    }

    private static CalendarMonth CalendarAt(CalendarMonth start, int offset)
    {
        var date = new DateOnly(start.Year, start.Month, 1).AddMonths(offset - 1);
        return new(date.Year, date.Month);
    }

    internal sealed class PaymentSource(string key, string category, string label, HouseholdPaymentDirection direction,
        bool isEstimate, bool mayAffectStartup, bool mayAffectOngoing)
    {
        public string Category { get; } = category;
        public HouseholdPaymentDirection Direction { get; } = direction;
        public bool MayAffectStartup { get; } = mayAffectStartup;
        public bool MayAffectOngoing { get; } = mayAffectOngoing;
        public Dictionary<int, CostSection> ByMonth { get; } = [];
        public CostSection Total { get; } = CostSection.Zero(key);
        public CostSection Unscheduled { get; } = new(key);
        public HouseholdPaymentSourceResult Result() => new(key, Category, label, Direction, isEstimate,
            Array.AsReadOnly(ByMonth.Keys.Order().ToArray()), Total.Result(), Unscheduled.Result());
    }
}
