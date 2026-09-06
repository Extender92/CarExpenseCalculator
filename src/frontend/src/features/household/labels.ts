import {
  costFields,
  leaseFields,
  profileFields,
  vehicleGroups,
  type Field,
} from "./form-model";

export const labels: Record<string, string> = {
  ownership: "Ägandekostnad",
  payments: "Betalningar",
  startupBudget: "Startbudget",
  monthlyBudget: "Månadsbudget",
  review: "Granska äldre post",
  targetKey: "Målpost",
  legacyDecisions: "Granskningsbeslut",
  loanFees: "Låneavgifter",
  principal: "Amortering",
  interest: "Låneränta",
  deposit: "Deposition",
  leasePayments: "Leasingbetalningar",
  leaseExcess: "Övermil",
  leaseExtras: "Leasingtillägg",
  financing: "Ränta och låneavgifter",
  depreciation: "Värdeminskning",
  energy: "Energi",
  tax: "Fordonsskatt",
  insurance: "Försäkring",
  service: "Planerad service",
  repairs: "Kända reparationer",
  repairAllowance: "Reparationssparande",
  customCosts: "Egna kostnader",
  lease: "Leasing",
  purchaseCash: "Kontantbetalning för bilen",
  principalRepaid: "Amortering",
  accruedOperatingCosts: "Periodiserade driftskostnader",
  paidOperatingCosts: "Betalda driftskostnader",
  depositPaid: "Betald deposition",
  depositRefund: "Återbetald deposition",
  depositWithheld: "Innehållen deposition",
  reconciledOwnershipCost: "Avstämd ägandekostnad",
  ownershipCost: "Ägandekostnad för perioden",
  monthlyCost: "Kostnad per månad",
  costPerMil: "Kostnad per mil",
  endEquity: "Restvärde minus kvarvarande skuld",
  zeroDistance: "Milkostnad saknas eftersom körsträckan är noll",
  residualHorizonMismatch:
    "Fast restvärde gäller en annan period – ange ett nytt belopp för den valda perioden",
  leaseHorizonMismatch: "Jämförelseperioden matchar inte leasingavtalets längd",
  calculationOutOfRange: "Beloppet är för stort för beräkningen",
  profile: "Hushållsprofil",
  vehicles: "Bilunderlag",
  input: "Bilunderlag",
  items: "Poster",
  single: "Värde",
  favorable: "Gynnsamt",
  baseline: "Normalt",
  cautious: "Försiktigt",
  year: "År",
  month: "Månad",
  registrationNumber: "Registreringsnummer",
  legacyReview: "Äldre uppgift behöver granskas",
  legacy: "Äldre underlag",
  maintenance: "Kombinerat äldre underhåll",
  recurring: "Äldre återkommande kostnad",
  oneTime: "Äldre engångskostnad",
  outflow: "Utbetalning",
  inflow: "Återbetalning",
  internalSaving: "Internt sparande",
  loan: "Lån",
  loanPrincipal: "Amortering",
  loanInterest: "Låneränta",
  loanSetupFee: "Uppläggningsavgift",
  loanMonthlyFee: "Lånets månadsavgift",
  leasePayment: "Leasingbetalning",
  leaseUpfront: "Förhöjd första avgift",
  leaseDeposit: "Deposition",
  leaseDepositRefund: "Återbetalad deposition",
  leaseExcessDistance: "Övermil",
  leaseEndFees: "Slutavgifter",
  leaseOtherPayments: "Leasingtillägg",
};
function collect(fields: Field[]) {
  for (const field of fields) {
    labels[field.key] ??= field.label;
    if (field.fields) collect(field.fields);
  }
}
collect([
  ...profileFields,
  ...vehicleGroups.flatMap((group) => group.fields),
  ...leaseFields,
  ...costFields,
]);

export function fieldLabel(path: string): string {
  path = path.replace(/^transition\.[a-f0-9-]+\./i, "");
  if (labels[path]) return labels[path];
  const parts = path.replace(/^vehicles\[\d+\]\.(input\.)?/, "").split(".");
  return parts
    .map((part) => {
      const match = /^(.*?)\[(\d+)\]$/.exec(part);
      return match
        ? `${labels[match[1]] ?? "Underlag"} ${Number(match[2]) + 1}`
        : (labels[part] ??
            (part.startsWith("legacy")
              ? "Äldre uppgift behöver granskas"
              : "Uppgift saknas eller behöver granskas"));
    })
    .join(" – ");
}

export function paymentLabel(label: string) {
  const builtIn: Record<string, string> = {
    "Purchase cash": "Kontantbetalning för bilen",
    "Loan setup fee": "Uppläggningsavgift",
    "Monthly loan fee": "Lånets månadsavgift",
    "Loan principal": "Amortering",
    "Loan interest": "Låneränta",
    "Loan installments": "Lånebetalningar",
    "Estimated monthly energy": "Uppskattad energibetalning",
    "Additional repair saving": "Reparationssparande",
    "Unresolved lease pricing": "Oklara leasingpriser",
    "Upfront lease fee": "Förhöjd första avgift",
    "Refundable deposit": "Återbetalningsbar deposition",
    "Invalid payment month": "Ogiltig betalningsmånad",
    "Lease payments": "Leasingbetalningar",
    "Deposit refund": "Återbetalning av deposition",
    "Excess distance": "Övermil",
    endFees: "Slutavgifter",
    otherPayments: "Övriga leasingbetalningar",
  };
  return (
    builtIn[label] ?? label.replace(/^Lease month (\d+)$/, "Leasingmånad $1")
  );
}
export const vehicleStateLabels = {
  listingOnly: "Annonsbil – kostnadsunderlag saknas",
  legacyPending: "Äldre kalkyl – väntar på övergång",
  current: "Aktuellt bilunderlag",
  new: "Nytt manuellt underlag",
};
