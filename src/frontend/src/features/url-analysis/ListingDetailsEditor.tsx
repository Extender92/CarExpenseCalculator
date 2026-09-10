import {useId} from "react";
import {Button} from "@/components/ui/button";
import {detailFields, weightCategories, trailerCategories, type DetailsForm} from "./details";
import {manualProvenance, type CollectionMode} from "./review-model";
import {inputClassName, textareaClassName, provenanceLabel} from "./presentation";

export function ListingDetailsEditor({value, url, disabled, errors, onChange}: {
  value: DetailsForm; url: string; disabled: boolean; errors: Record<string,string>; onChange: (value: DetailsForm) => void;
}) {
  const id = useId();
  return <section className="space-y-4" aria-label="Kompletterande annonsunderlag">
    <h3 className="font-semibold">Beskrivning och kompletterande annonsunderlag</h3>
    <p className="text-sm">Annonsuppgifter är obekräftade tills du uttryckligen granskar och bekräftar dem. Viktbeteckningar och säljarpåståenden behåller sin ursprungliga betydelse.</p>
    <div className="grid gap-4 md:grid-cols-2">{detailFields.map(f => {
      const field = value.fields[f.key], error = errors[`details.${f.key}`], fieldId = `${id}-details.${f.key}`;
      const options = f.key === "weightCategory" ? weightCategories : f.key === "trailerWeightCategory" ? trailerCategories : null;
      const update = (input: string) => onChange({...value, fields: {...value.fields, [f.key]: {input, provenance: input ? manualProvenance(url) : null}}});
      return <div key={f.key} className={f.key === "description" ? "md:col-span-2" : ""}>
        <label htmlFor={fieldId}>{f.label}</label>
        {options ? <select id={fieldId} className={inputClassName} value={field.input} disabled={disabled} onChange={e => update(e.target.value)}>
          {options.map(([value,label]) => <option key={value} value={value}>{label}</option>)}
        </select> : <textarea id={fieldId} className={textareaClassName} rows={f.key === "description" ? 8 : 2}
          value={field.input} disabled={disabled} aria-invalid={!!error} aria-describedby={error ? `${fieldId}-error` : undefined}
          onChange={e => update(e.target.value)} />}
        <p className="text-xs text-slate-500">{field.provenance ? provenanceLabel(field.provenance) : "Okänt"}</p>
        {error && <p id={`${fieldId}-error`} className="text-sm text-red-700">{error}</p>}
      </div>;
    })}</div>
    {(["specifications", "sellerAnswers"] as const).map(key => {
      const c = value[key];
      const change = (entries: typeof c.entries) => onChange({...value, [key]: {...c, entries}});
      return <fieldset key={key} className="space-y-3" disabled={disabled}>
        <legend className="font-semibold">{key === "specifications" ? "Övriga specifikationer" : "Säljarens frågor och svar"}</legend>
        <label htmlFor={`${id}-${key}-mode`}>Uppgifter</label>
        <select id={`${id}-${key}-mode`} className={inputClassName} value={c.mode} onChange={e => onChange({...value, [key]: {...c, mode: e.target.value as CollectionMode}})}>
          <option value="unknown">Okänt</option><option value="empty">Bekräftat tomt</option><option value="values">Angivna poster</option>
        </select>
        {errors[`details.${key}`] && <p role="alert">{errors[`details.${key}`]}</p>}
        {c.mode === "values" && <>{c.entries.map((entry,i) => <div key={entry.id} className="grid gap-2 md:grid-cols-2">
          {(["first", "second"] as const).map(part => <div key={part}>
            <label htmlFor={`${id}-details.${key}[${i}].${part}`}>
            {key === "specifications" ? part === "first" ? "Beteckning" : "Värde" : part === "first" ? "Fråga" : "Svar"} {i+1}
            </label>
            <textarea id={`${id}-details.${key}[${i}].${part}`} aria-invalid={!!errors[`details.${key}[${i}].${part}`]} className={textareaClassName} value={entry[part]} onChange={e => change(c.entries.map((x,j) => j === i ? {...x,[part]:e.target.value,provenance:manualProvenance(url)} : x))} />
            {errors[`details.${key}[${i}].${part}`] && <span role="alert">{errors[`details.${key}[${i}].${part}`]}</span>}
          </div>)}
          <p className="text-xs">{entry.provenance ? provenanceLabel(entry.provenance) : "Manuellt underlag"}</p>
          <Button variant="secondary" onClick={() => change(c.entries.filter((_,j) => j !== i))}>Ta bort post {i+1}</Button>
        </div>)}<Button variant="secondary" onClick={() => change([...c.entries,{id:crypto.randomUUID(),first:"",second:"",provenance:null}])}>Lägg till {key === "specifications" ? "specifikation" : "fråga och svar"}</Button></>}
      </fieldset>;
    })}
  </section>;
}
