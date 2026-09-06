import type {
  CostWrite,
  LegacyDecision,
  LegacyReview,
  ProfileInput,
  VehicleInput,
  VehicleResponse,
} from "./api";
import { Numeric, canonicalNumber, cloneExact, n } from "./numbers";
import { normalizeListingUrl } from "@/features/url-analysis/urls";

export interface Field {
  key: string;
  label: string;
  kind:
    | "number"
    | "integer"
    | "distance"
    | "text"
    | "select"
    | "boolean"
    | "object"
    | "array"
    | "sensitivity"
    | "category"
    | "month";
  options?: readonly (readonly [string, string])[];
  fields?: Field[];
  required?: boolean;
  maximum?: number;
  help?: string;
  singleOnly?: boolean;
  factory?: () => Record<string, unknown>;
}
const number = (key: string, label: string, help?: string): Field => ({
  key,
  label,
  kind: "number",
  help,
});
const integer = (key: string, label: string): Field => ({
  key,
  label,
  kind: "integer",
});
const text = (key: string, label: string, required = false): Field => ({
  key,
  label,
  kind: "text",
  required,
});
const select = (
  key: string,
  label: string,
  options: Field["options"],
  required = false,
): Field => ({ key, label, kind: "select", options, required });
const sensitivity = (key: string, label: string): Field => ({
  key,
  label,
  kind: "sensitivity",
});
export const fuels = [
  ["petrol", "Bensin"],
  ["diesel", "Diesel"],
  ["electricity", "El"],
  ["ethanol", "Etanol"],
  ["biogas", "Biogas"],
  ["naturalGas", "Naturgas"],
  ["liquefiedPetroleumGas", "Gasol"],
  ["hydrogen", "Vätgas"],
  ["other", "Annat"],
] as const;
export const units = [
  ["litre", "Liter"],
  ["kilowattHour", "kWh"],
  ["kilogram", "kg"],
] as const;
export const costFields: Field[] = [
  text("label", "Benämning", true),
  sensitivity("amountSek", "Belopp (kr)"),
  select("cadence", "Kostnadens frekvens", [
    ["monthly", "Månad"],
    ["annual", "År"],
    ["once", "Engångskostnad"],
  ]),
  integer("monthOffset", "Betalningsmånad från start (0 = startutgift)"),
  integer("dueMonthOfYear", "Förfallomånad under året (1–12)"),
  text("evidenceNote", "Underlag eller kommentar"),
  text("sourceUrl", "Källänk"),
];
const leaseChargeFields = costFields.filter(
  (field) => !["cadence", "dueMonthOfYear"].includes(field.key),
);

let keySequence = 0;
const keyPrefix = `item-${Date.now().toString(36)}`;
export const newKey = () => `${keyPrefix}-${++keySequence}`;
export const newCost = () => ({
  key: newKey(),
  label: "",
  amountSek: null,
  cadence: null,
  monthOffset: null,
  dueMonthOfYear: null,
  evidenceNote: null,
  sourceUrl: null,
});
const newCharge = () => ({
  key: newKey(),
  label: "",
  amountSek: null,
  monthOffset: null,
  evidenceNote: null,
  sourceUrl: null,
});

export const profileFields: Field[] = [
  integer("periodMonths", "Ägandeperiod (månader, 1–120)"),
  { key: "startMonth", label: "Startmånad", kind: "month" },
  {
    key: "annualDistanceKilometres",
    label: "Årlig körsträcka (mil)",
    kind: "distance",
  },
  number("purchaseCashSek", "Kontanter till bilköpet (kr)"),
  {
    key: "loanTerms",
    label: "Gemensamma lånevillkor",
    kind: "object",
    fields: [
      sensitivity("annualNominalInterestRatePercent", "Nominell årsränta (%)"),
      integer("termMonths", "Lånets löptid (månader)"),
      number("setupFeeSek", "Uppläggningsavgift (kr)"),
      number("monthlyFeeSek", "Lånets månadsavgift (kr)"),
    ],
  },
  {
    key: "energyPrices",
    label: "Gemensamma bränslepriser",
    kind: "array",
    required: true,
    maximum: 27,
    factory: () => ({ fuel: "petrol", unit: "litre", pricePerUnitSek: null }),
    fields: [
      select("fuel", "Drivmedel", fuels, true),
      select("unit", "Prisenhet", units, true),
      sensitivity("pricePerUnitSek", "Pris per enhet (kr)"),
    ],
  },
  sensitivity("electricDrivingSharePercent", "Elandel av körsträckan (%)"),
  sensitivity("homeChargingSharePercent", "Andel hemmaladdning (%)"),
  sensitivity("homeChargingPricePerKilowattHourSek", "Elpris hemma (kr/kWh)"),
  sensitivity(
    "publicChargingPricePerKilowattHourSek",
    "Publikt elpris (kr/kWh)",
  ),
  sensitivity("chargingLossPercent", "Laddförluster (%)"),
  number(
    "startupBudgetSek",
    "Separat startbudget (kr)",
    "Tomt betyder ingen budgetgräns; 0 är en faktisk gräns.",
  ),
  number(
    "monthlyBudgetSek",
    "Löpande månadsbudget (kr)",
    "Avser genomsnittliga utbetalningar plus reparationssparande. Återbetalningar räknas inte av.",
  ),
  select(
    "activeSensitivityMode",
    "Aktivt osäkerhetsläge",
    [
      ["baseline", "Normalt"],
      ["favorable", "Gynnsamt"],
      ["cautious", "Försiktigt"],
    ],
    true,
  ),
];

export const leaseFields: Field[] = [
  integer("termMonths", "Avtalstid (månader)"),
  number("upfrontNonRefundableSek", "Förhöjd första avgift (kr)"),
  number("refundableDepositSek", "Deposition vid starten (kr)"),
  sensitivity("depositRefundSek", "Återbetalning av deposition (kr)"),
  {
    key: "includedDistanceKilometres",
    label: "Inkluderad körsträcka för hela avtalet (mil)",
    kind: "distance",
  },
  sensitivity(
    "excessDistancePricePerKilometreSek",
    "Övermilskostnad per kilometer (kr/km)",
  ),
  select("priceBasis", "Prisunderlag", [
    ["quoted", "Angivna avtalsvillkor"],
    ["estimated", "Egen uttrycklig uppskattning"],
    ["unresolved", "Oklara prisvillkor"],
  ]),
  {
    key: "energyIncluded",
    label: "All energi ingår utan extra energikostnad",
    kind: "boolean",
  },
  {
    key: "monthlyPayments",
    label: "Leasingens månadsbetalningar",
    kind: "array",
    maximum: 120,
    factory: () => ({ monthOffset: n(1), amountSek: null }),
    fields: [
      { ...integer("monthOffset", "Månad (1–120)"), required: true },
      number("amountSek", "Månadsbetalning (kr)"),
    ],
  },
  {
    key: "endFees",
    label: "Slutavgifter (i avtalets sista månad)",
    kind: "array",
    maximum: 50,
    factory: newCharge,
    fields: leaseChargeFields.filter((field) => field.key !== "monthOffset"),
  },
  {
    key: "otherPayments",
    label: "Övriga leasingbetalningar",
    kind: "array",
    maximum: 50,
    factory: newCharge,
    fields: leaseChargeFields,
  },
];
export const vehicleGroups: { label: string; fields: Field[] }[] = [
  {
    label: "Anskaffning och restvärde",
    fields: [
      number("priceSek", "Inköpspris (kr)"),
      {
        key: "residual",
        label: "Restvärdesunderlag",
        kind: "object",
        fields: [
          select(
            "mode",
            "Restvärdesmodell",
            [
              ["fixedAmount", "Fast belopp (kr)"],
              ["annualPercentage", "Årlig värdeminskning (%)"],
            ],
            true,
          ),
          sensitivity("value", "Restvärde eller årlig procent"),
          integer("periodMonths", "Period för fast restvärde (månader)"),
        ],
        factory: () => ({
          mode: "fixedAmount",
          value: null,
          periodMonths: null,
        }),
      },
    ],
  },
  {
    label: "Energi",
    fields: [
      {
        key: "energySources",
        label: "Energikällor",
        kind: "array",
        maximum: 2,
        factory: () => ({
          key: newKey(),
          fuel: null,
          unit: null,
          consumptionPer100Kilometres: null,
          consumptionBasis: null,
          electricityBasis: null,
        }),
        fields: [
          select("fuel", "Drivmedel", fuels),
          select("unit", "Förbrukningsenhet", units),
          sensitivity("consumptionPer100Kilometres", "Förbrukning per 100 km"),
          select("consumptionBasis", "Förbrukningsgrund", [
            ["wholeDistance", "Över hela körsträckan"],
            ["drivingMode", "Inom körläget"],
          ]),
          select("electricityBasis", "Elförbrukningens mätpunkt", [
            ["battery", "Batteriet (laddförluster tillkommer)"],
            ["metered", "Inköpt el (förluster ingår)"],
          ]),
        ],
      },
    ],
  },
  {
    label: "Skatt och försäkring",
    fields: [
      { key: "tax", label: "Fordonsskatt", kind: "category" },
      { key: "insurance", label: "Försäkring", kind: "category" },
    ],
  },
  {
    label: "Service, reparationer och sparande",
    fields: [
      { key: "service", label: "Planerad service", kind: "category" },
      { key: "repairs", label: "Kända reparationer", kind: "category" },
      sensitivity(
        "additionalRepairAllowancePerMonthSek",
        "Reserv för ytterligare reparationer (kr/månad)",
      ),
    ],
  },
  {
    label: "Egna kostnader och betalningstidpunkter",
    fields: [
      { key: "customCosts", label: "Egna kostnadsposter", kind: "category" },
    ],
  },
];

export const initialProfile = (): ProfileInput => ({
  energyPrices: [],
  activeSensitivityMode: "baseline",
});
export const initialVehicle = (): VehicleInput => ({
  candidateKey: "manual",
  acquisitionType: "purchase",
});
export type FormErrors = Record<string, string[]>;

export function validateFields(
  value: Record<string, unknown>,
  fields: Field[],
  prefix = "",
): FormErrors {
  const errors: FormErrors = {};
  const add = (path: string, message: string) => {
    (errors[path] ??= []).push(message);
  };
  function visit(
    data: Record<string, unknown>,
    definitions: Field[],
    base: string,
  ) {
    for (const field of definitions) {
      const path = base ? `${base}.${field.key}` : field.key;
      const entry = data[field.key];
      if (entry == null) {
        if (field.required) add(path, "Fyll i uppgiften.");
        continue;
      }
      if (["number", "integer", "distance"].includes(field.kind)) {
        if (!(entry instanceof Numeric)) {
          add(path, "Ange ett tal.");
          continue;
        }
        try {
          canonicalNumber(entry.text);
          if (field.kind === "integer" && !/^-?\d+$/.test(entry.text.trim()))
            add(path, "Ange ett heltal.");
          if (
            field.kind === "integer" &&
            /^-?\d+$/.test(entry.text.trim()) &&
            (BigInt(entry.text.trim()) < -2147483648n ||
              BigInt(entry.text.trim()) > 2147483647n)
          )
            add(path, "Heltalet är för stort för detta fält.");
        } catch (error) {
          add(path, (error as Error).message);
        }
      } else if (field.kind === "text") {
        if (field.required && !String(entry).trim())
          add(path, "Ange en benämning.");
        if (field.key === "label" && String(entry).trim().length > 120)
          add(path, "Benämningen får ha högst 120 tecken.");
        if (field.key === "evidenceNote" && String(entry).length > 1000)
          add(path, "Underlaget får ha högst 1 000 tecken.");
        if (
          field.key === "sourceUrl" &&
          (/\s/.test(String(entry).trim()) ||
            typeof normalizeListingUrl(String(entry)) === "string")
        )
          add(path, "Ange en giltig offentlig HTTP- eller HTTPS-länk.");
      } else if (field.kind === "select") {
        if (!field.options?.some(([option]) => option === entry))
          add(path, "Välj ett tillåtet alternativ.");
      } else if (field.kind === "sensitivity") {
        const trio = entry as Record<string, unknown>;
        const definitions =
          "single" in trio
            ? [number("single", "Värde")]
            : [
                number("favorable", "Gynnsamt"),
                number("baseline", "Normalt"),
                number("cautious", "Försiktigt"),
              ];
        visit(
          trio,
          definitions.map((item) => ({ ...item, required: true })),
          path,
        );
      } else if (field.kind === "month") {
        visit(
          entry as Record<string, unknown>,
          [integer("year", "År"), integer("month", "Månad")].map((item) => ({
            ...item,
            required: true,
          })),
          path,
        );
      } else if (field.kind === "object")
        visit(entry as Record<string, unknown>, field.fields ?? [], path);
      else if (field.kind === "array" || field.kind === "category") {
        const rows =
          field.kind === "category"
            ? (entry as { items: unknown[] }).items
            : (entry as unknown[]);
        if (rows.length > (field.maximum ?? 50)) add(path, "För många poster.");
        rows.forEach((row, index) =>
          visit(
            row as Record<string, unknown>,
            field.kind === "category" ? costFields : (field.fields ?? []),
            `${path}${field.kind === "category" ? ".items" : ""}[${index}]`,
          ),
        );
      }
    }
  }
  visit(value, fields, prefix);
  if (prefix === "profile" && Array.isArray(value.energyPrices)) {
    const pairs = new Set<string>();
    value.energyPrices.forEach((price, index) => {
      const pair = `${price.fuel}:${price.unit}`;
      if (pairs.has(pair))
        add(
          `profile.energyPrices[${index}].fuel`,
          "Det finns redan ett pris för detta drivmedel och denna enhet.",
        );
      pairs.add(pair);
    });
  }
  return errors;
}

export function validateVehicle(input: VehicleInput): FormErrors {
  const fields = vehicleGroups
    .flatMap((group) => group.fields)
    .filter(
      (field) =>
        input.acquisitionType !== "lease" ||
        !["priceSek", "residual"].includes(field.key),
    );
  if (input.acquisitionType === "lease")
    fields.push({
      key: "lease",
      label: "Leasing",
      kind: "object",
      fields: leaseFields,
    });
  const errors = validateFields(
    input as Record<string, unknown>,
    fields,
    "input",
  );
  const keys = new Set<string>();
  const checkKey = (key: string, path: string) => {
    if (!key?.trim() || key.trim().length > 120 || keys.has(key.trim()))
      errors[path] = ["Posten måste ha en egen nyckel med 1–120 tecken."];
    keys.add(key?.trim());
  };
  if (!input.candidateKey?.trim() || input.candidateKey.trim().length > 120)
    errors["input.candidateKey"] = ["Ange en giltig bilidentitet."];
  for (const category of [
    "tax",
    "insurance",
    "service",
    "repairs",
    "customCosts",
  ] as const)
    input[category]?.items.forEach((item, index) => {
      checkKey(item.key, `input.${category}.items[${index}].label`);
      if (
        ["tax", "insurance"].includes(category) &&
        item.amountSek &&
        item.amountSek.single == null
      )
        errors[`input.${category}.items[${index}].amountSek`] = [
          "Skatt och försäkring använder ett angivet offertbelopp.",
        ];
    });
  // Energy keys have their own namespace in Core.
  const energyKeys = new Set<string>();
  input.energySources?.forEach((item, index) => {
    if (
      !item.key?.trim() ||
      item.key.trim().length > 120 ||
      energyKeys.has(item.key.trim())
    )
      errors[`input.energySources[${index}].fuel`] = [
        "Energikällorna måste ha egna nycklar.",
      ];
    energyKeys.add(item.key.trim());
  });
  for (const category of ["endFees", "otherPayments"] as const)
    input.lease?.[category]?.forEach((item, index) => {
      checkKey(item.key, `input.lease.${category}[${index}].label`);
      if (category === "endFees" && item.monthOffset != null)
        errors[`input.lease.endFees[${index}].monthOffset`] = [
          "Slutavgifter betalas i avtalets sista månad.",
        ];
    });
  const months = new Set<string>();
  input.lease?.monthlyPayments?.forEach((payment, index) => {
    const month = payment.monthOffset?.text;
    if (months.has(month))
      errors[`input.lease.monthlyPayments[${index}].monthOffset`] = [
        "Det finns redan en betalning för månaden.",
      ];
    months.add(month);
  });
  if (
    input.acquisitionType === "lease"
      ? input.priceSek != null || input.residual != null
      : input.lease != null
  )
    errors["input.acquisitionType"] = [
      "Köp- och leasingunderlag får inte anges samtidigt.",
    ];
  return errors;
}

export function allCostKeys(input: VehicleInput): string[] {
  return [
    input.tax,
    input.insurance,
    input.service,
    input.repairs,
    input.customCosts,
  ]
    .flatMap((group) => group?.items.map((item) => item.key) ?? [])
    .concat(
      input.energySources?.map((item) => item.key) ?? [],
      input.lease?.endFees?.map((item) => item.key) ?? [],
      input.lease?.otherPayments?.map((item) => item.key) ?? [],
    );
}

export function validateCostWrite(
  cost: CostWrite,
  reviews: LegacyReview[],
): FormErrors {
  const errors = validateVehicle(cost.input);
  if (cost.legacyDecisions == null) return errors;
  const keys = new Set(allCostKeys(cost.input));
  const used = new Set<string>();
  for (const source of reviews) {
    const matches = cost.legacyDecisions.filter(
      (item) => item.key === source.input.key,
    );
    const decision = matches[0];
    const path = `review.${source.input.key}.targetKey`;
    if (matches.length !== 1)
      errors[path] = ["Ange ett beslut för varje äldre post."];
    else if (decision.disposition === "map") {
      if (
        !decision.targetKey ||
        !legacyTargetKeys(cost.input, source).includes(decision.targetKey) ||
        used.has(decision.targetKey) ||
        (decision.targetKey !== source.input.key && keys.has(source.input.key))
      )
        errors[path] = [
          "Välj en befintlig målpost som bara används för detta underlag.",
        ];
      if (decision.targetKey) used.add(decision.targetKey);
    } else if (keys.has(source.input.key))
      errors[path] = [
        "Ta bort originalposten ur kalkylen innan den behålls för granskning eller tas bort.",
      ];
  }
  if (cost.legacyDecisions.length !== reviews.length)
    errors.legacyDecisions = [
      "Granskningsbesluten måste omfatta hela det aktuella underlaget. Läs och granska serveruppgifterna igen.",
    ];
  return errors;
}

export function recoveredCost(vehicle: VehicleResponse): CostWrite {
  if (!vehicle.legacy)
    return {
      input: cloneExact(vehicle.input ?? initialVehicle()),
      vehicleLabel: vehicle.vehicleLabel,
      listingLinkMode: "preserve",
    };
  const input = cloneExact(vehicle.legacy.suggestedInput);
  const targets = new Set(allCostKeys(input));
  return {
    input,
    vehicleLabel: vehicle.vehicleLabel,
    listingLinkMode: "preserve",
    legacyDecisions: vehicle.legacy.items.map((item) =>
      targets.has(item.input.key)
        ? { key: item.input.key, disposition: "map", targetKey: item.input.key }
        : {
            key: item.input.key,
            disposition: "keepForReview",
            targetKey: null,
          },
    ),
  };
}

export function remainingReviews(
  reviews: LegacyReview[],
  decisions?: LegacyDecision[] | null,
  input?: VehicleInput,
): LegacyReview[] {
  if (decisions == null) return reviews;
  const targets = input ? new Set(allCostKeys(input)) : null;
  return reviews.filter(
    (item) =>
      !decisions.some(
        (decision) =>
          decision.key === item.input.key &&
          (decision.disposition === "discard" ||
            (decision.disposition === "map" &&
              !!decision.targetKey &&
              (!input ||
                (legacyTargetKeys(input, item).includes(decision.targetKey) &&
                  (decision.targetKey === item.input.key ||
                    !targets!.has(item.input.key)))) &&
              decisions.filter(
                (other) =>
                  other.disposition === "map" &&
                  other.targetKey === decision.targetKey,
              ).length === 1)),
      ),
  );
}

export function legacyTargetKeys(
  input: VehicleInput,
  review: LegacyReview,
): string[] {
  const energyKeys = input.energySources?.map((item) => item.key) ?? [];
  if (review.input.kind === "energy") return energyKeys;
  return allCostKeys({ ...input, energySources: [] });
}

export function reviewSet(vehicle: VehicleResponse): LegacyReview[] {
  return vehicle.legacy?.items ?? vehicle.unresolvedLegacyItems;
}

export function removeCostKey(input: VehicleInput, key: string): VehicleInput {
  const next = cloneExact(input);
  for (const category of [
    next.tax,
    next.insurance,
    next.service,
    next.repairs,
    next.customCosts,
  ]) {
    if (category)
      category.items = category.items.filter((item) => item.key !== key);
  }
  if (next.energySources)
    next.energySources = next.energySources.filter((item) => item.key !== key);
  if (next.lease?.endFees)
    next.lease.endFees = next.lease.endFees.filter((item) => item.key !== key);
  if (next.lease?.otherPayments)
    next.lease.otherPayments = next.lease.otherPayments.filter(
      (item) => item.key !== key,
    );
  return next;
}
