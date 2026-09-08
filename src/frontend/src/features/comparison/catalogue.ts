import { fuels, type Field } from "@/features/household/form-model";
import { fieldLabel } from "@/features/household/labels";
import type { FactEdits, Schema } from "./api";

export type FactKey = Exclude<keyof FactEdits, "conditionNotes">;
export interface Criterion {
  key: string;
  label: string;
  kind: "number" | "distance" | "date" | "choice" | "budget";
  operator: Schema<"HardRuleOperator">;
  choiceKey?: keyof Schema<"ComparisonChoice">;
  options?: Field["options"];
  fact?: FactKey;
}
const numeric = (
  key: FactKey,
  label: string,
  kind: "number" | "distance" = "number",
): Criterion => ({ key, fact: key, label, kind, operator: "inclusiveRange" });
const choice = (
  key: FactKey,
  label: string,
  choiceKey: Criterion["choiceKey"],
  options: Field["options"],
): Criterion => ({
  key,
  fact: key,
  label,
  kind: "choice",
  operator: "allowedSet",
  choiceKey,
  options,
});
export const criteria: readonly Criterion[] = [
  numeric("purchasePriceSek", "Köppris (kr)"),
  numeric("odometerKilometres", "Mätarställning (mil)", "distance"),
  numeric("ownerCount", "Ägarantal"),
  {
    ...choice("towBar", "Dragkrok", "boolean", [
      ["true", "Ja"],
      ["false", "Nej"],
    ]),
    operator: "equals",
  },
  choice("transmission", "Växellåda", "transmission", [
    ["manual", "Manuell"],
    ["automatic", "Automat"],
  ]),
  numeric("seats", "Sittplatser"),
  numeric("modelYear", "Årsmodell"),
  {
    ...choice("fuelTypes", "Drivmedel", "fuelType", fuels),
    operator: "intersects",
  },
  choice("bodyType", "Kaross", "bodyType", [
    ["sedan", "Sedan"],
    ["hatchback", "Halvkombi"],
    ["wagon", "Kombi"],
    ["suv", "SUV"],
    ["coupe", "Coupé"],
    ["convertible", "Cabriolet"],
    ["minivan", "Minibuss/MPV"],
    ["pickup", "Pickup"],
    ["van", "Skåpbil"],
    ["other", "Annan"],
  ]),
  choice("drivetrain", "Drivning", "drivetrain", [
    ["frontWheelDrive", "Framhjulsdrift"],
    ["rearWheelDrive", "Bakhjulsdrift"],
    ["allWheelDrive", "Fyrhjulsdrift"],
  ]),
  choice("locality", "Ort", "text", undefined),
  choice("county", "Län", "text", undefined),
  numeric("towingCapacityKilograms", "Bromsad dragvikt (kg)"),
  {
    key: "inspectionValidThrough",
    fact: "inspectionValidThrough",
    label: "Besiktningsgiltighet (återstående dagar)",
    kind: "date",
    operator: "minimumRemainingDays",
  },
  choice("serviceDocumentation", "Serviceunderlag", "serviceDocumentation", [
    ["documented", "Dokumenterat"],
    ["partial", "Delvis dokumenterat"],
    ["absent", "Saknas"],
  ]),
  ...[
    ["netCostSek", "Ägandekostnad (kr)"],
    ["costPerMonthSek", "Månadskostnad (kr)"],
    ["costPerMilSek", "Milkostnad (kr/mil)"],
  ].map(
    ([key, label]): Criterion => ({
      key,
      label,
      kind: "number",
      operator: "inclusiveRange",
    }),
  ),
  ...[
    ["startupBudget", "Startbudget"],
    ["monthlyBudget", "Månadsbudget"],
  ].map(
    ([key, label]): Criterion => ({
      key,
      label,
      kind: "budget",
      operator: "withinBudget",
    }),
  ),
];
export const evidenceOptions = [
  ["advertised", "Annonsuppgift"],
  ["userConfirmed", "Användarbekräftat"],
  ["registryVerified", "Registerverifierat"],
] as const;
export const signalOptions = [
  ["conditionNotes", "Skick- och reparationsuppgifter"],
  ["serviceDocumentation", "Serviceunderlag"],
  ["inspectionValidity", "Besiktningsgiltighet"],
  ["costCompleteness", "Kostnadernas fullständighet"],
  ["startupBudget", "Startbudget"],
  ["monthlyBudget", "Månadsbudget"],
] as const;
export const labelFor = (key: string) =>
  criteria.find((c) => c.key === key)?.label ??
  {
    lastServiceDate: "Senaste service",
    lastServiceOdometerKilometres: "Mätarställning vid service (mil)",
    serviceNotes: "Serviceanteckningar",
    conditionNotes: "Skickuppgifter",
    registrationNumber: "Registreringsnummer",
    weight: "Vikt",
    minimumEvidence: "Verifiering",
    zeroPoint: "Mål för 0 poäng",
    fullPoint: "Mål för 100 poäng",
    allowedValues: "Tillåtna värden",
    preferredValues: "Önskade värden",
    minimum: "Lägsta gräns",
    maximum: "Högsta gräns",
    value: "Värde",
    kind: "Åtgärd",
    asOfDate: "Utvärderingsdatum",
    baseline: "Serverunderlag",
  }[key] ??
  fieldLabel(key);
export const eligibilityLabels = {
  eligible: "Godkänd",
  needsVerification: "Behöver verifieras",
  rejected: "Bortvald",
};
export const factStateLabels = {
  known: "Angivet",
  unknown: "Okänt",
  notApplicable: "Ej tillämpligt",
  conflicting: "Motstridigt",
};
export const reasonText: Record<string, string> = {
  overlapOrTie:
    "Poängintervallen överlappar eller är lika; ingen säker preferensvinnare.",
  definiteWinner:
    "En godkänd bil har en säker poängfördel mot samtliga övriga alternativ som inte är bortvalda.",
  noEligibleCandidate: "Ingen bil uppfyller alla verifierade krav.",
  noActiveCriteria: "Inga aktiva prioriteringar.",
  overlappingIntervals: "Poängintervallen överlappar; ingen säker rangordning.",
  noCandidates: "Inga bilar att jämföra.",
  noEligibleCandidates: "Ingen bil uppfyller alla verifierade krav.",
  outOfRange: "Värdet ligger utanför tillåtet intervall.",
  required: "Uppgiften behöver anges.",
  invalidEnum: "Välj ett giltigt alternativ.",
  invalidAnchors: "Ange olika mål för 0 och 100 poäng.",
  invalidWeight: "Vikten ska vara ett heltal mellan 0 och 5.",
  emptySelection: "Välj minst ett alternativ.",
  insufficientEvidence: "Uppgiften behöver starkare verifiering.",
  unknown: "Uppgiften är okänd.",
  notApplicable: "Uppgiften är inte tillämplig.",
  conflicting: "Källuppgifterna är motstridiga.",
  calculationOutOfRange:
    "Beräkningen kan inte representeras med tillåten precision.",
  residualHorizonMismatch:
    "Restvärdet gäller en annan ägandeperiod. Ange ett nytt belopp eller återställ perioden.",
  leaseHorizonMismatch:
    "Jämförelseperioden matchar inte leasingavtalet. Kända betalningar behålls.",
  zeroDistance: "Milkostnad kan inte beräknas vid noll körsträcka.",
};
