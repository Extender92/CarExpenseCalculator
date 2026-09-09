import {
  Numeric,
  formatMoney,
  formatNumeric,
  shiftDecimal,
} from "@/features/household/numbers";
import {
  costFields,
  leaseFields,
  profileFields,
  vehicleGroups,
  type Field,
} from "@/features/household/form-model";
import { labels, paymentLabel } from "@/features/household/labels";
import {
  criteria,
  evidenceOptions,
  factStateLabels,
  reasonText,
} from "./catalogue";
import type { SectionResult } from "@/features/household/api";
import type { Schema } from "./api";
import type { Immutable } from "./report-model";

export const budgetLabels = {
  notConfigured: "Ingen budgetgräns angiven",
  withinLimit: "Inom budget",
  exceeded: "Budgeten överskrids",
  unknown: "Kan inte bedömas ännu",
  invalid: "Ogiltig budget",
};
export function scoreText(value: Immutable<Schema<"ScoreRange">> | null) {
  return value
    ? `[${formatNumeric(value.lower)}, ${formatNumeric(value.upper)}]`
    : "Ingen poäng";
}
export function amountText(value: Immutable<SectionResult>) {
  if (value.state === "notApplicable") return "Ej tillämpligt";
  if (value.completeTotalSek != null)
    return formatMoney(value.completeTotalSek);
  return value.knownSubtotalSek != null
    ? `${formatMoney(value.knownSubtotalSek)} känd del`
    : "Okänt";
}

const names: Record<string, string> = {
  ...labels,
  ...Object.fromEntries(criteria.map((c) => [c.key, c.label])),
  // Report distances keep the exact API kilometre value alongside Swedish mil.
  annualDistanceKilometres: "Årlig körsträcka",
  odometerKilometres: "Mätarställning",
  includedDistanceKilometres: "Inkluderad avtalssträcka",
  lastServiceOdometerKilometres: "Mätarställning vid senaste service",
  inspectionValidThrough: "Besiktningsgiltighet till och med",
  lastServiceDate: "Senaste service",
  serviceNotes: "Serviceanteckningar",
  conditionNotes: "Skickuppgifter",
  state: "Status",
  status: "Bedömning",
  isIncluded: "Ingår utan grundkostnad (tillägg redovisas separat)",
  key: "Postnyckel",
  candidateKey: "Kandidatnyckel",
  label: "Benämning",
  value: "Värde",
  facts: "Fordonsuppgifter",
  observations: "Aktuella observationer",
  evidence: "Källunderlag",
  origin: "Ursprung",
  extractionMethod: "Inhämtningsmetod",
  verification: "Verifiering",
  observedAt: "Observerad tidpunkt",
  confirmedAt: "Bekräftad tidpunkt",
  listingVersion: "Observationens annonsversion",
  sourceListingVersion: "Observationens annonsversion",
  fuelType: "Drivmedel",
  sourceUrl: "Källänk",
  costConfirmedAt: "Kostnadsunderlaget bekräftat",
  sourceRevisions: "Källrevisioner",
  householdProfile: "Hushållsprofilens revision",
  ruleProfile: "Regelprofilens revision",
  vehicle: "Fordonsrevision",
  listing: "Aktuell annonsversion",
  factsReviewedListing: "Faktaunderlagets granskade annonsversion",
  costReviewedListing: "Kostnadsunderlagets granskade annonsversion",
  needsListingReview: "Annonsen behöver granskas",
  errors: "Fältfel",
  inputErrors: "Fel i kostnadsindata",
  missingComponents: "Saknade komponenter",
  path: "Fältsökväg",
  code: "Felkod",
  message: "Felbeskrivning",
  reason: "Granskningsorsak",
  reasons: "Orsaker",
  reasonCode: "Orsak",
  affectedSections: "Berörda delar",
  hardRules: "Hårda köpkrav",
  preferences: "Prioriteringar",
  signals: "Förklarande signaler",
  rule: "Köpkrav",
  preference: "Prioritering",
  criterionKey: "Kriterium",
  enabled: "Aktiverat",
  operator: "Villkor",
  minimumEvidence: "Krävd verifiering",
  minimum: "Inkluderande lägsta gräns",
  maximum: "Inkluderande högsta gräns",
  allowedValues: "Tillåtna värden",
  preferredValues: "Önskade värden",
  zeroPoint: "Mål för 0 poäng",
  fullPoint: "Mål för 100 poäng",
  weight: "Vikt (0 stänger av för alla bilar)",
  shortInspectionDays: "Gräns för kort besiktningsgiltighet (dagar)",
  assessment: "Underlagsbedömning",
  actual: "Faktiskt värde",
  number: "Numeriskt värde",
  choice: "Kategoriskt värde",
  boolean: "Ja/nej",
  text: "Text",
  date: "Datum",
  fuels: "Drivmedel",
  hasAdequateEvidence: "Tillräckligt verifierat",
  observedConditionSatisfied: "Det angivna värdet uppfyller villkoret",
  explanation: "Förklaring",
  range: "Poängintervall",
  weightedContribution: "Viktat poängbidrag",
  lower: "Undre gräns",
  upper: "Övre gräns",
  isActive: "Aktiv prioritering",
  isKnown: "Känt bidrag",
  kind: "Typ",
  cost: "Periodkostnad",
  financingDetails: "Finansieringsunderlag",
  allocation: "Kontanter och lån",
  cashAppliedSek: "Kontantbetalning (kr)",
  principalSek: "Lånebelopp (kr)",
  unusedPurchaseCashSek: "Oanvända köpkontanter (kr)",
  annualNominalInterestRatePercent: "Nominell årsränta (%)",
  termMonths: "Löptid / avtalstid (månader)",
  periodMonths: "Period (månader)",
  monthlyInstallmentSek: "Lånebetalning per månad (kr)",
  paymentsDuringPeriodSek: "Lånebetalningar under perioden (kr)",
  principalRepaidSek: "Amortering (kr)",
  interestPaidSek: "Betald ränta (kr)",
  remainingPrincipalSek: "Kvarvarande skuld (kr)",
  installments: "Lånebetalningsplan",
  openingPrincipalSek: "Ingående skuld (kr)",
  interestSek: "Ränta (kr)",
  paymentSek: "Betalning (kr)",
  monthlyFeesDuringPeriodSek: "Lånets månadsavgifter under perioden (kr)",
  financingCostDuringPeriodSek: "Finansieringskostnad (kr)",
  acquisitionCashOutflowSek: "Utbetalningar för anskaffning (kr)",
  residualValueSek: "Restvärde (kr)",
  baseQuantity: "Använd energimängd",
  purchasedQuantity: "Inköpt energimängd",
  effectivePricePerUnitSek: "Effektivt energipris (kr/enhet)",
  sources: "Spårbara underlag",
  totals: "Kostnadssammanställning",
  distanceKilometres: "Periodens körsträcka",
  coveredMonths: "Täckta månader",
  requestedMonths: "Begärda månader",
  isEstimate: "Uppskattad betalning",
  excessDistanceKilometres: "Överkörning",
  calendarStatus: "Kalenderns fullständighet",
  months: "Månader",
  externalOutflow: "Externa utbetalningar",
  externalInflow: "Externa återbetalningar",
  netExternalCashFlow: "Nettokassautflöde",
  calendarMonth: "Kalendermånad",
  categories: "Betalningskategorier",
  category: "Kategori",
  direction: "Betalningsriktning",
  amount: "Belopp",
  monthOffsets: "Betalningsmånader från start",
  unscheduledAmount: "Belopp utan fastställd tidpunkt",
  limitSek: "Budgetgräns (kr)",
  fundingRequired: "Finansieringsbehov",
  reconciliation: "Kostnadsavstämning",
  acquisitionType: "Anskaffningsform",
  energyUnit: "Energienhet",
  knownSubtotalSek: "Känd delsumma (kr)",
  completeTotalSek: "Komplett summa (kr)",
  reviewDecisions: "Granskningsbeslut",
  costConfirmation: "Kostnadsbekräftelse",
  listingReview: "Annonsgranskning",
  contributions: "Poängbidrag",
  unresolvedLegacyItems: "Kvarvarande äldre granskningsposter",
  costCompleteness: "Kostnadernas fullständighet",
  inspectionValidity: "Besiktningsgiltighet",
};
const options: Record<string, Record<string, string>> = {};
function collect(fields: Field[]) {
  for (const f of fields) {
    names[f.key] ??= f.label;
    if (f.options) options[f.key] = Object.fromEntries(f.options);
    if (f.fields) collect(f.fields);
  }
}
collect([
  ...profileFields,
  ...vehicleGroups.flatMap((g) => g.fields),
  ...leaseFields,
  ...costFields,
]);
for (const c of criteria)
  if (c.options) options[c.choiceKey ?? c.key] = Object.fromEntries(c.options);
const states: Record<string, string> = {
  ...factStateLabels,
  complete: "Komplett",
  partial: "Delvis känt",
  unavailable: "Uppgifter saknas",
  invalid: "Ogiltigt underlag",
  pass: "Uppfyllt",
  fail: "Uppfylls inte",
  needsVerification: "Behöver verifieras",
};
Object.assign(options, {
  state: states,
  category: labels,
  direction: labels,
  status: budgetLabels,
  minimumEvidence: Object.fromEntries(evidenceOptions),
  verification: {
    unverified: "Obekräftat",
    userConfirmed: "Användarbekräftat",
    registryVerified: "Registerverifierat",
  },
  origin: { listing: "Annons", user: "Användare", registry: "Register" },
  extractionMethod: { manual: "Manuellt", ai: "AI-extraktion" },
  acquisitionType: { purchase: "Köp", lease: "Leasing" },
  kind: {
    information: "Källuppgift",
    warning: "Att granska",
    positive: "Positiv uppgift",
    ...labels,
  },
  fuelTypes: options.fuelType,
  fuels: options.fuelType,
  operator: {
    inclusiveRange: "Inkluderande gränser",
    equals: "Lika med",
    allowedSet: "Tillåten mängd",
    intersects: "Överlapp med vald mängd",
    minimumRemainingDays: "Minsta återstående hela dagar",
    withinBudget: "Inom budgetgränsen",
  },
});

export const reportLabel = (key: string) => names[key] ?? key;
export function exactText(value: Numeric) {
  // Preserve every supplied decimal digit, including trailing zeroes.
  return value.text.replace(".", ",");
}
export interface ReportRow {
  path: string;
  label: string;
  value: string;
}

/** Flatten into independently breakable rows; no large object becomes one table cell. */
export function inputRows(value: unknown, exact = true): ReportRow[] {
  const rows: ReportRow[] = [];
  const add = (path: string, label: string, value: string) =>
    rows.push({ path, label, value });
  const walk = (
    item: unknown,
    path: string,
    label: string,
    key: string,
    context: string,
  ) => {
    if (item instanceof Numeric) {
      const distance =
        /(?:Distance|odometer|Odometer).*Kilometres$/.test(context) ||
        context === "distanceKilometres" ||
        context === "includedDistanceKilometres";
      const text =
        key === "lower" || key === "upper"
          ? formatNumeric(item)
          : exact
            ? exactText(item)
            : /Sek$/.test(context)
              ? formatMoney(item)
              : /(?:Percent|Quantity|Kilometres)$/.test(context)
                ? formatNumeric(item, 3)
                : exactText(item);
      add(
        path,
        label,
        distance
          ? `${text} km (${shiftDecimal(item.text, -1).replace(".", ",")} mil)`
          : text,
      );
    } else if (item == null) {
      add(
        path,
        label,
        key === "limitSek" ||
          key === "startupBudgetSek" ||
          key === "monthlyBudgetSek"
          ? "Ingen budgetgräns angiven"
          : "Okänt / ej angivet",
      );
    } else if (typeof item === "boolean") add(path, label, item ? "Ja" : "Nej");
    else if (typeof item === "string") {
      const code = [
        "code",
        "reason",
        "reasonCode",
        "reasons",
        "missingComponents",
        "affectedSections",
      ].includes(key);
      add(
        path,
        label,
        code
          ? `${reasonText[item] ?? labels[item] ?? "Uppgiften behöver granskas"} (${item})`
          : key === "criterionKey"
            ? reportLabel(item)
            : key === "label"
              ? paymentLabel(item)
              : (options[key]?.[item] ??
                (key === "value" ? options[context]?.[item] : undefined) ??
                item),
      );
    } else if (Array.isArray(item)) {
      if (
        key === "monthOffsets" &&
        item.length &&
        item.every((v) => v instanceof Numeric)
      ) {
        add(path, label, item.map(exactText).join(", "));
        return;
      }
      if (!item.length)
        add(
          path,
          label,
          key === "items"
            ? "Bekräftad tom samling – inga kostnadsposter"
            : "Tom samling – inga poster",
        );
      item.forEach((v, i) =>
        walk(v, `${path}[${i}]`, `${label} ${i + 1}`, key, context),
      );
    } else if (typeof item === "object") {
      const obj = item as Record<string, unknown>;
      if (
        typeof obj.path === "string" &&
        typeof obj.code === "string" &&
        "message" in obj
      ) {
        add(
          path,
          label,
          `${reasonText[obj.code] ?? labels[obj.code] ?? "Uppgiften behöver granskas"} · ${obj.path} (${obj.code})`,
        );
      } else if ("completeTotalSek" in obj && "missingComponents" in obj) {
        const section = obj as unknown as Immutable<SectionResult>;
        add(path, label, `${amountText(section)} · ${states[section.state]}`);
        section.missingComponents.forEach((p, i) =>
          walk(
            p,
            `${path}.missingComponents[${i}]`,
            `${label} – saknas`,
            "missingComponents",
            context,
          ),
        );
        section.errors.forEach((e, i) =>
          walk(
            e,
            `${path}.errors[${i}]`,
            `${label} – fältfel ${i + 1}`,
            "errors",
            context,
          ),
        );
      } else {
        let entries = Object.entries(obj);
        // Null slots of a typed union are unused representations, not missing
        // goals/fuels. Keep every active sensitivity slot, including its nulls.
        if (
          ["single", "favorable", "baseline", "cautious"].every((k) => k in obj)
        ) {
          const single =
            obj.single != null ||
            ["favorable", "baseline", "cautious"].every((k) => obj[k] == null);
          entries = entries.filter(([k]) =>
            single ? k === "single" : k !== "single",
          );
        }
        const choiceKeys = [
          "boolean",
          "transmission",
          "fuelType",
          "bodyType",
          "drivetrain",
          "serviceDocumentation",
          "text",
        ];
        const observedKeys = ["number", "choice", "date", "fuels"];
        if (
          (entries.length > 0 &&
            entries.every(([k]) => choiceKeys.includes(k))) ||
          (entries.length === observedKeys.length &&
            entries.every(([k]) => observedKeys.includes(k)))
        ) {
          entries = entries.filter(([, v]) => v != null);
          if (!entries.length) {
            add(path, label, "Okänt / ej angivet");
            return;
          }
        }
        if (!entries.length) add(path, label, "Tomt underlag");
        for (const [k, v] of entries) {
          const inherit = [
            "single",
            "favorable",
            "baseline",
            "cautious",
            "observations",
            "value",
            "number",
          ].includes(k);
          walk(
            v,
            path ? `${path}.${k}` : k,
            label ? `${label} – ${reportLabel(k)}` : reportLabel(k),
            k,
            inherit ? context : k,
          );
        }
      }
    }
  };
  walk(value, "", "", "", "");
  return rows;
}
