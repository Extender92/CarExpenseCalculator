import type { components } from "@/api/schema";
import { canonicalNumber, cloneExact, n, Numeric, type Exact } from "@/features/household/numbers";
import { manualProvenance, type ScalarDraftField, type CollectionMode } from "./review-model";
import type { FieldProvenance } from "@/api/client";

export const detailFields = [
  {"key": "title", "label": "Annonsrubrik", "kind": "text", "maxLength": 500},
  {"key": "subtitle", "label": "Underrubrik", "kind": "text", "maxLength": 500},
  {"key": "description", "label": "Hela beskrivningen", "kind": "text", "maxLength": 32000},
  {"key": "listingId", "label": "Annons-ID", "kind": "text", "maxLength": 100},
  {"key": "seats", "label": "Sittplatser", "kind": "integer", "maxLength": null},
  {"key": "doors", "label": "Dörrar", "kind": "integer", "maxLength": null},
  {"key": "luggageLitres", "label": "Bagagevolym (liter)", "kind": "decimal", "maxLength": null},
  {"key": "weightKilograms", "label": "Angiven vikt (kg)", "kind": "decimal", "maxLength": null},
  {"key": "weightLabel", "label": "Viktens beteckning", "kind": "text", "maxLength": 100},
  {"key": "weightCategory", "label": "Viktkategori", "kind": "text", "maxLength": 100},
  {"key": "trailerWeightKilograms", "label": "Angiven släpvagnsvikt (kg)", "kind": "decimal", "maxLength": null},
  {"key": "trailerWeightLabel", "label": "Släpvagnsviktens beteckning", "kind": "text", "maxLength": 100},
  {"key": "trailerWeightCategory", "label": "Släpvagnsviktens kategori", "kind": "text", "maxLength": 100},
  {"key": "postalCode", "label": "Postnummer", "kind": "text", "maxLength": 100},
  {"key": "country", "label": "Land", "kind": "text", "maxLength": 100},
  {"key": "feeClass", "label": "Avgiftsklass", "kind": "text", "maxLength": 100},
  {"key": "saleForm", "label": "Försäljningsform", "kind": "text", "maxLength": 100},
  {"key": "updatedLocalDateTime", "label": "Uppdaterad lokal datum och tid", "kind": "text", "maxLength": 100},
  {"key": "updatedTimeZone", "label": "Angiven tidszon", "kind": "text", "maxLength": 100},
  {"key": "updatedUtcOffsetMinutes", "label": "Angiven UTC-offset (minuter)", "kind": "integer", "maxLength": null},
] as const;
export type DetailKey = typeof detailFields[number]["key"];
export interface DetailEntry { id: string; first: string; second: string; provenance: FieldProvenance | null }
export interface DetailCollection { mode: CollectionMode; entries: DetailEntry[] }
export interface DetailsForm {
  fields: Record<DetailKey, ScalarDraftField>;
  specifications: DetailCollection;
  sellerAnswers: DetailCollection;
}
export type DetailsInput = Exact<components["schemas"]["ListingDetailsInput"]>;
export const weightCategories = [["", "Okänt"], ["unspecified", "Viktkategori inte angiven"], ["curb", "Uttryckligen tjänstevikt"], ["gross", "Uttryckligen totalvikt"]] as const;
export const trailerCategories = [["", "Okänt"], ["unspecified", "Viktkategori inte angiven"], ["braked", "Uttryckligen bromsad släpvagn"], ["unbraked", "Uttryckligen obromsad släpvagn"]] as const;
export function detailsDisplay(form: DetailsForm | undefined): string {
  if (!form) return "Okänt";
  const rows = detailFields.map(f => `${f.label}: ${form.fields[f.key].input || "Okänt"}`);
  for (const key of ["specifications", "sellerAnswers"] as const) {
    const c = form[key];
    rows.push(`${key === "specifications" ? "Övriga specifikationer" : "Säljarfrågor"}: ${c.mode === "unknown" ? "Okänt" : c.mode === "empty" ? "Bekräftat tomt" : ""}`);
    if (c.mode === "values") rows.push(...c.entries.map(x => `${x.first}: ${x.second}`));
  }
  return rows.join("\n");
}
type DetailsResponse = import("./exact").Preserved<components["schemas"]["ListingDetailsResponse"]>;
export function emptyDetails(): DetailsForm {
  return { fields: Object.fromEntries(detailFields.map(f => [f.key, { input: "", provenance: null }])) as DetailsForm["fields"],
    specifications: {mode: "unknown", entries: []}, sellerAnswers: {mode: "unknown", entries: []} };
}
export function detailsFromResponse(value: DetailsResponse | null | undefined): DetailsForm {
  const form = emptyDetails();
  for (const f of detailFields) {
    const field = value?.[f.key];
    form.fields[f.key] = { input: field?.value instanceof Numeric ? field.value.text : field?.value == null ? "" : String(field.value),
      provenance: field ? {...field.provenance} : null };
  }
  form.specifications = { mode: value?.specifications == null ? "unknown" : value.specifications.length ? "values" : "empty",
    entries: value?.specifications?.map((v,i) => ({id: `spec-${i}`, first: v.value.name, second: v.value.value, provenance: {...v.provenance}})) ?? [] };
  form.sellerAnswers = { mode: value?.sellerAnswers == null ? "unknown" : value.sellerAnswers.length ? "values" : "empty",
    entries: value?.sellerAnswers?.map((v,i) => ({id: `answer-${i}`, first: v.value.question, second: v.value.answer, provenance: {...v.provenance}})) ?? [] };
  return form;
}
export function detailsErrors(form: DetailsForm): Record<string,string> {
  const errors: Record<string,string> = {};
  for (const f of detailFields) {
    const text = form.fields[f.key].input;
    if (!text.trim()) continue;
    if (f.kind === "text") {
      if (text.trim().length > f.maxLength!) errors[`details.${f.key}`] = `Högst ${f.maxLength} tecken tillåts.`;
    } else try {
      const value = canonicalNumber(text);
      if (f.kind === "integer" && value.includes(".")) throw new Error("Ange ett heltal.");
    } catch (error) { errors[`details.${f.key}`] = (error as Error).message; }
  }
  for (const key of ["specifications","sellerAnswers"] as const) {
    const c = form[key];
    if (c.mode !== "values") continue;
    if (!c.entries.length || c.entries.length > 100) errors[`details.${key}`] = "Ange 1–100 poster eller välj okänt/bekräftat tomt.";
    c.entries.forEach((v,i) => {
      const max = key === "specifications" ? 100 : 1000;
      if (!v.first.trim() || v.first.trim().length > max) errors[`details.${key}[${i}].first`] = `Ange 1–${max} tecken.`;
      if (!v.second.trim() || v.second.trim().length > 1000) errors[`details.${key}[${i}].second`] = "Ange 1–1000 tecken.";
    });
  }
  return errors;
}
export function detailsToInput(form: DetailsForm | undefined, url: string): DetailsInput | null {
  if (!form) return null;
  const fields = Object.fromEntries(detailFields.map(f => {
    const x = form.fields[f.key]; const text = x.input.trim().normalize("NFC");
    return [f.key, text ? {value: f.kind === "text" ? text : n(canonicalNumber(text)), provenance: cloneExact(x.provenance ?? manualProvenance(url))} : null];
  }));
  const specs = form.specifications, answers = form.sellerAnswers;
  return {...fields,
    specifications: specs.mode === "unknown" ? null : specs.mode === "empty" ? [] : specs.entries.map(x => ({value: {name: x.first.trim(), value:x.second.trim()}, provenance: cloneExact(x.provenance ?? manualProvenance(url))})),
    sellerAnswers: answers.mode === "unknown" ? null : answers.mode === "empty" ? [] : answers.entries.map(x => ({value: {question:x.first.trim(), answer:x.second.trim()}, provenance: cloneExact(x.provenance ?? manualProvenance(url))})),
  };
}
