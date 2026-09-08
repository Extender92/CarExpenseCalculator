using System.Globalization;
using CarExpenseCalculator.Core.Listings;

namespace CarExpenseCalculator.Core.Comparisons;

internal static class ComparisonText
{
    private static readonly CultureInfo Swedish = CultureInfo.GetCultureInfo("sv-SE");
    internal static string Number(decimal number) => number.ToString("0.############################", Swedish);
    internal static string Label(string key) => key switch
    {
        "purchasePriceSek" => "Köppris", "odometerKilometres" => "Mätarställning", "ownerCount" => "Ägarantal",
        "towBar" => "Dragkrok", "transmission" => "Växellåda", "seats" => "Sittplatser", "modelYear" => "Årsmodell",
        "fuelTypes" => "Drivmedel", "bodyType" => "Kaross", "drivetrain" => "Drivning", "locality" => "Ort", "county" => "Län",
        "towingCapacityKilograms" => "Bromsad dragvikt", "inspectionValidThrough" => "Besiktningsgiltighet",
        "serviceDocumentation" => "Serviceunderlag", "netCostSek" => "Uppskattad ägandekostnad",
        "costPerMonthSek" => "Kostnad per månad", "costPerMilSek" => "Kostnad per mil",
        "startupBudget" => "Startbudget", "monthlyBudget" => "Månadsbudget", _ => throw new InvalidOperationException("Unsupported criterion."),
    };
    internal static string Actual(ComparisonObservedValue? value) => value is null ? "uppgift saknas"
        : value.Date is { } date ? $"{date:yyyy-MM-dd} ({Number(value.Number!.Value)} dagar kvar)"
        : value.Number is { } number ? Number(number)
        : value.Fuels is { } fuels ? fuels.Count == 0 ? "inga angivna drivmedel" : string.Join(", ", fuels.Select(Fuel))
        : Choice(value.Choice!);
    private static string Evidence(EvidenceRequirement evidence) => evidence switch
    {
        EvidenceRequirement.Advertised => "annonsuppgift eller uttryckligt kalkylantagande",
        EvidenceRequirement.UserConfirmed => "användarbekräftat underlag",
        _ => "registerverifierat underlag",
    };
    internal static string Hard(HardRuleInput rule, CriterionAssessment assessment, HardRuleState state, bool? observed)
    {
        var condition = rule.Operator switch
        {
            HardRuleOperator.InclusiveRange => rule.Minimum is { } min
                ? $"minst {Number(min)}" + (rule.Maximum is { } max ? $", högst {Number(max)}" : "")
                : $"högst {Number(rule.Maximum!.Value)}",
            HardRuleOperator.MinimumRemainingDays => $"minst {Number(rule.Minimum!.Value)} återstående dagar",
            HardRuleOperator.WithinBudget => "inom den angivna budgetgränsen",
            _ => string.Join(", ", rule.AllowedValues!.Select(Choice)),
        };
        var outcome = state switch
        {
            HardRuleState.Pass => "Kravet är uppfyllt.",
            HardRuleState.Fail => "Kravet är inte uppfyllt. Bilen är bortvald.",
            _ => "Kravet behöver verifieras. " + (observed is null ? "" : observed.Value
                ? "Den angivna uppgiften skulle uppfylla kravet." : "Den angivna uppgiften skulle inte uppfylla kravet."),
        };
        return $"{Label(rule.CriterionKey)}: {ActualWithUnit(rule.CriterionKey, assessment.Actual)}. Krav: {condition}. Underlag som krävs: {Evidence(rule.MinimumEvidence)}. {outcome}";
    }
    internal static string Preference(PreferenceInput preference, CriterionAssessment assessment, ScoreRange range) =>
        $"{Label(preference.CriterionKey)}: {ActualWithUnit(preference.CriterionKey, assessment.Actual)}. Vikt {preference.Weight}. " +
        (assessment.HasAdequateEvidence ? $"{Number(ComparisonEvaluator.Round(range.Lower))} poäng enligt dina mål."
            : "Möjlig poäng 0–100; underlaget är ofullständigt eller behöver verifieras.");
    private static string ActualWithUnit(string key, ComparisonObservedValue? value) => Actual(value) + (value?.Number is null ? "" : key switch
    {
        "purchasePriceSek" or "netCostSek" => " kr", "costPerMonthSek" => " kr/månad", "costPerMilSek" => " kr/mil",
        "odometerKilometres" => " km", "towingCapacityKilograms" => " kg", _ => "",
    });
    internal static string Source(ComparisonEvidence? evidence) => evidence?.Origin switch
    {
        FieldOrigin.Listing => $"Annonsuppgift ({evidence.SourceUrl})",
        FieldOrigin.User => "Användaruppgift",
        _ => "Uppgift med otillräckligt underlag",
    };
    private static string Choice(ComparisonChoice choice) => choice switch
    {
        { Boolean: { } value } => value ? "ja" : "nej",
        { Text: { } value } => value,
        { Transmission: { } value } => value == Transmission.Automatic ? "automat" : "manuell",
        { FuelType: { } value } => Fuel(value),
        { Drivetrain: { } value } => value switch { Drivetrain.FrontWheelDrive => "framhjulsdrift", Drivetrain.RearWheelDrive => "bakhjulsdrift", _ => "fyrhjulsdrift" },
        { ServiceDocumentation: { } value } => value switch { ServiceDocumentationStatus.Documented => "dokumenterat", ServiceDocumentationStatus.Partial => "delvis dokumenterat", _ => "saknas" },
        { BodyType: { } value } => value switch
        {
            BodyType.Sedan => "sedan", BodyType.Hatchback => "halvkombi", BodyType.Wagon => "kombi", BodyType.Suv => "SUV",
            BodyType.Coupe => "kupé", BodyType.Convertible => "cabriolet", BodyType.Minivan => "familjebuss",
            BodyType.Pickup => "pickup", BodyType.Van => "skåpbil", _ => "övrig kaross",
        },
        _ => "ogiltig uppgift",
    };
    private static string Fuel(FuelType fuel) => fuel switch
    {
        FuelType.Petrol => "bensin", FuelType.Diesel => "diesel", FuelType.Electricity => "el", FuelType.Ethanol => "etanol",
        FuelType.Biogas => "biogas", FuelType.NaturalGas => "naturgas", FuelType.LiquefiedPetroleumGas => "gasol",
        FuelType.Hydrogen => "vätgas", _ => "övrigt drivmedel",
    };
}
