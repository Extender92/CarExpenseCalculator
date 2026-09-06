import { useState } from "react";
import { Button } from "@/components/ui/button";
import type { CostWrite, LegacyReview } from "./api";
import {
  legacyTargetKeys,
  removeCostKey,
  fuels,
  units,
  type Field,
  type FormErrors,
} from "./form-model";
import {
  Numeric,
  formatMoney,
  numericText,
  stringifyExact,
  n,
  shiftDecimal,
} from "./numbers";
import { inputClass, Labeled, panelClass } from "./Fields";
import { fieldLabel, labels } from "./labels";

function reviewDecision(
  cost: CostWrite,
  reviews: LegacyReview[],
  key: string,
  disposition: "keepForReview" | "map" | "discard",
  targetKey: string | null,
): CostWrite {
  const decisions = reviews.map(
    (review) =>
      cost.legacyDecisions?.find(
        (decision) => decision.key === review.input.key,
      ) ?? {
        key: review.input.key,
        disposition: "keepForReview" as const,
        targetKey: null,
      },
  );
  return {
    ...cost,
    input:
      disposition === "map" && (!targetKey || targetKey === key)
        ? cost.input
        : removeCostKey(cost.input, key),
    legacyDecisions: decisions.map((item) =>
      item.key === key
        ? {
            key,
            disposition,
            targetKey: disposition === "map" ? targetKey : null,
          }
        : item,
    ),
  };
}

export function LegacyReviewEditor({
  reviews,
  value,
  onChange,
  errors = {},
}: {
  reviews: LegacyReview[];
  value: CostWrite;
  onChange: (value: CostWrite) => void;
  errors?: FormErrors;
}) {
  if (!reviews.length) return null;
  return (
    <details open className={panelClass}>
      <summary className="cursor-pointer font-semibold">
        Granska äldre poster ({reviews.length})
      </summary>
      <p className="my-3 text-sm text-slate-400">
        Granska varje post. Föreslagna entydiga mappningar visas som valda. En
        kvarvarande post ingår inte automatiskt i kostnaden. Skapa eller välj
        ett enda mål för varje mappning.
      </p>
      <div className="space-y-4">
        {reviews.map((review) => (
          <ReviewRow
            key={review.input.key}
            review={review}
            reviews={reviews}
            value={value}
            onChange={onChange}
            errors={errors}
          />
        ))}
      </div>
    </details>
  );
}
function ReviewRow({
  review,
  reviews,
  value,
  onChange,
  errors,
}: {
  review: LegacyReview;
  reviews: LegacyReview[];
  value: CostWrite;
  onChange: (value: CostWrite) => void;
  errors: FormErrors;
}) {
  const [category, setCategory] = useState("");
  const item = review.input;
  const decision = value.legacyDecisions?.find(
    (decision) => decision.key === item.key,
  );
  const disposition = decision?.disposition ?? "keepForReview";
  const keys = legacyTargetKeys(value.input, review);
  const targetLabel = (key: string) => {
    const category = [
      value.input.tax,
      value.input.insurance,
      value.input.service,
      value.input.repairs,
      value.input.customCosts,
    ].find((group) => group?.items.some((row) => row.key === key));
    return (
      category?.items.find((row) => row.key === key)?.label ||
      `Energikälla ${(value.input.energySources?.findIndex((source) => source.key === key) ?? -1) + 1}`
    );
  };
  return (
    <fieldset className="rounded-lg border border-slate-700 p-3">
      <legend className="px-1 font-medium">{item.label}</legend>
      <p className="text-sm">
        {labels[item.kind]} · Ursprungligt belopp: {formatMoney(item.amountSek)}
        {item.cadence
          ? ` per ${item.cadence === "annual" ? "år" : "månad"}`
          : ""}
        {item.consumptionPer100Kilometres != null &&
          ` · Förbrukning: ${numericText(item.consumptionPer100Kilometres)} per 100 km`}
      </p>
      <p className="mt-1 text-xs text-slate-400">
        Post: {item.key}. {reviewReason(review.reason)} Berör:{" "}
        {review.affectedSections.map(fieldLabel).join(", ")}.
      </p>
      <div className="mt-3 grid gap-3 sm:grid-cols-2">
        <Labeled label={`Beslut för ${item.label}`} path={`review.${item.key}`}>
          {(id) => (
            <select
              id={id}
              className={inputClass}
              value={disposition}
              onChange={(event) =>
                onChange(
                  reviewDecision(
                    value,
                    reviews,
                    item.key,
                    event.target.value as typeof disposition,
                    null,
                  ),
                )
              }
            >
              <option value="keepForReview">Behåll för granskning</option>
              <option value="map">Mappa till kostnad/energi</option>
              <option value="discard">Ta bort</option>
            </select>
          )}
        </Labeled>
        {disposition === "map" && (
          <Labeled
            label={`Målpost för ${item.label}`}
            path={`review.${item.key}.targetKey`}
            errors={errors}
          >
            {(id, describedBy) => (
              <select
                id={id}
                data-field-path={`review.${item.key}.targetKey`}
                aria-describedby={describedBy}
                aria-invalid={!!errors[`review.${item.key}.targetKey`]}
                className={inputClass}
                value={decision?.targetKey ?? ""}
                onChange={(event) =>
                  onChange(
                    reviewDecision(
                      value,
                      reviews,
                      item.key,
                      "map",
                      event.target.value || null,
                    ),
                  )
                }
              >
                <option value="">Välj en målpost</option>
                {keys.map((key) => (
                  <option
                    value={key}
                    key={key}
                    disabled={value.legacyDecisions?.some(
                      (other) =>
                        other.key !== item.key &&
                        other.disposition === "map" &&
                        other.targetKey === key,
                    )}
                  >
                    {targetLabel(key)} · {key}
                  </option>
                ))}
              </select>
            )}
          </Labeled>
        )}
      </div>
      {disposition === "map" && !decision?.targetKey && (
        <div className="mt-3 flex flex-wrap items-end gap-3">
          <Labeled
            label={`Skapa mål för ${item.label}`}
            path={`review.${item.key}.category`}
          >
            {(id) => (
              <select
                id={id}
                className={inputClass}
                value={category}
                onChange={(event) => setCategory(event.target.value)}
              >
                <option value="">Välj kategori uttryckligen</option>
                {(item.kind === "energy"
                  ? ["energySources"]
                  : ["tax", "insurance", "service", "repairs", "customCosts"]
                ).map((key) => (
                  <option key={key} value={key}>
                    {fieldLabel(key)}
                  </option>
                ))}
              </select>
            )}
          </Labeled>
          <Button
            variant="secondary"
            disabled={!category}
            onClick={() => {
              const input = removeCostKey(value.input, item.key);
              if (category === "energySources") {
                if ((input.energySources?.length ?? 0) >= 2) return;
                input.energySources = [
                  ...(input.energySources ?? []),
                  {
                    key: item.key,
                    unit: item.energyUnit,
                    consumptionPer100Kilometres:
                      item.consumptionPer100Kilometres == null
                        ? null
                        : { single: item.consumptionPer100Kilometres },
                  },
                ];
              } else {
                const key = category as
                  | "tax"
                  | "insurance"
                  | "service"
                  | "repairs"
                  | "customCosts";
                if ((input[key]?.items.length ?? 0) >= 50) return;
                input[key] = {
                  isIncluded: input[key]?.isIncluded ?? false,
                  items: [
                    ...(input[key]?.items ?? []),
                    {
                      key: item.key,
                      label: item.label,
                      amountSek:
                        item.amountSek == null
                          ? null
                          : { single: item.amountSek },
                      cadence: item.kind === "oneTime" ? "once" : item.cadence,
                    },
                  ],
                };
              }
              onChange(
                reviewDecision(
                  { ...value, input },
                  reviews,
                  item.key,
                  "map",
                  item.key,
                ),
              );
            }}
          >
            Skapa från originalposten
          </Button>
          <p className="w-full text-xs text-slate-400">
            Högst 50 kostnadsposter per kategori eller två energikällor.
            Komplettera målpostens tidpunkt, drivmedel och förbrukningsgrund i
            bilens formulär.
          </p>
        </div>
      )}
    </fieldset>
  );
}
function reviewReason(reason: string) {
  const text: Record<string, string> = {
    combinedMaintenanceRequiresClassification:
      "Service, reparationer och reserv måste skiljas åt.",
    energyIdentityAndConsumptionBasisRequireReview:
      "Drivmedel och förbrukningsgrund behöver bekräftas.",
    oneTimePaymentMonthRequired: "Betalningstidpunkt behöver anges.",
    explicitCostMappingRequired:
      "Kontrollera och bekräfta kostnadens föreslagna målpost.",
  };
  return (
    text[reason] ??
    "Kontrollera originaluppgiften, kategorin och betalningstidpunkten innan den används."
  );
}

/** Human-readable input display, also used to compare server values without mutating local forms. */
export function InputFacts({
  value,
  fields,
}: {
  value: unknown;
  fields?: Field[];
}) {
  if (value == null) return <span className="text-slate-400">Okänt</span>;
  if (value instanceof Numeric)
    return <span className="break-all">{value.text.replace(".", ",")}</span>;
  if (typeof value === "boolean") return <span>{value ? "Ja" : "Nej"}</span>;
  if (typeof value !== "object")
    return <span className="break-words">{String(value)}</span>;
  if (Array.isArray(value))
    return value.length ? (
      <ol className="space-y-2">
        {value.map((item, index) => (
          <li key={index} className="border-l border-slate-700 pl-3">
            <InputFacts value={item} fields={fields} />
          </li>
        ))}
      </ol>
    ) : (
      <span>Bekräftat inga poster</span>
    );
  return (
    <dl className="space-y-2">
      {Object.entries(value)
        .filter(([key]) => !["candidateKey", "key"].includes(key))
        .map(([key, item]) => {
          const field = fields?.find((field) => field.key === key);
          const option =
            field?.options?.find(([option]) => option === item)?.[1] ??
            enumLabels[key]?.[String(item)];
          let shown = item;
          if (field?.kind === "distance" && item instanceof Numeric) {
            try {
              shown = n(shiftDecimal(item.text, -1));
            } catch {
              /* Preserve unfinished input in the comparison. */
            }
          }
          return (
            <div key={key} className="grid gap-1 sm:grid-cols-2">
              <dt className="text-slate-400">
                {field?.label ?? factLabels[key] ?? fieldLabel(key)}
              </dt>
              <dd>
                {option ?? <InputFacts value={shown} fields={field?.fields} />}
              </dd>
            </div>
          );
        })}
    </dl>
  );
}
const factLabels: Record<string, string> = {
  isIncluded: "Ingår",
  vehicleLabel: "Bilens namn",
  ownershipPeriodMonths: "Ägandeperiod (månader)",
  annualDistanceKilometres: "Årskörsträcka (km)",
  expectedResaleValueSek: "Förväntat restvärde (kr)",
  purchasePriceSek: "Inköpspris (kr)",
  financing: "Finansiering",
  downPaymentSek: "Kontantinsats (kr)",
  energySources: "Energikällor",
  recurringCosts: "Återkommande kostnader",
  oneTimeCosts: "Engångskostnader",
  consumptionPer100Kilometres: "Förbrukning per 100 km",
  pricePerUnitSek: "Pris per enhet (kr)",
  distanceSharePercent: "Körandel (%)",
  amountSek: "Belopp (kr)",
  label: "Benämning",
  cadence: "Frekvens",
  vehicle: "Fordonsuppgifter",
  scenario: "Äldre kalkyl",
  acquisitionType: "Anskaffningsform",
};

const enumLabels: Record<string, Record<string, string>> = {
  fuel: Object.fromEntries(fuels),
  unit: Object.fromEntries(units),
  energyUnit: Object.fromEntries(units),
  cadence: { monthly: "Månad", annual: "År", once: "Engångskostnad" },
  acquisitionType: { purchase: "Köp", lease: "Leasing" },
  mode: {
    fixedAmount: "Fast restvärde",
    annualPercentage: "Årlig procentuell värdeminskning",
  },
  origin: {
    listing: "Annonsuppgift",
    user: "Användaruppgift",
    registry: "Registeruppgift",
  },
  extractionMethod: { manual: "Manuell", ai: "AI-extraktion" },
  verification: {
    unverified: "Ej verifierad",
    userConfirmed: "Användarbekräftad",
    registryVerified: "Registerverifierad",
  },
};

export function InputComparison({
  local,
  remote,
  fields,
}: {
  local: unknown;
  remote: unknown;
  fields: Field[];
}) {
  const differs = (() => {
    try {
      return stringifyExact(local) !== stringifyExact(remote);
    } catch {
      return true;
    }
  })();
  return (
    <details className={panelClass}>
      <summary className="cursor-pointer font-medium">
        Granska skillnader mot servern
        {differs ? " – uppgifterna skiljer sig" : " – samma uppgifter"}
      </summary>
      <div className="mt-4 grid gap-6 lg:grid-cols-2">
        <div>
          <h4 className="mb-3 font-semibold">Din redigering</h4>
          <InputFacts value={local} fields={fields} />
        </div>
        <div>
          <h4 className="mb-3 font-semibold">Aktuellt serverunderlag</h4>
          <InputFacts value={remote} fields={fields} />
        </div>
      </div>
    </details>
  );
}
