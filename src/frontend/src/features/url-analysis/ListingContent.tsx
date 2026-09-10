import {inputRows, reportLabel} from "@/features/comparison/report-format";
import {detailFields} from "./details";
import {Numeric} from "@/features/household/numbers";

const names: Record<string,string> = {
  ...Object.fromEntries(detailFields.map(f => [f.key, f.label])),
  listing: "Annons", details: "Kompletterande underlag", specifications:"Övriga specifikationer",
  sellerAnswers:"Säljarens frågor och svar", question:"Fråga", answer:"Svar", name:"Beteckning", value:"Värde",
  equipment:"Utrustning", sellerClaims:"Säljarpåståenden", conditionNotes:"Skickuppgifter", vin:"Chassinummer",
  firstRegistrationDate:"Första registreringsdatum", lastInspectionDate:"Senaste besiktningsdatum", nextInspectionDate:"Nästa besiktningsdatum",
  listingVersion:"Aktuell annonsversion", listingSchemaVersion:"Annonsens lagringsversion", sources:"Observerade sidkällor",
  sourcePageObserved:"Sidöppning observerad", submittedUrl:"Inskickad annonsadress", normalizedUrl:"Normaliserad annonsadress",
  requestedModel:"Begärd AI-modell", promptVersion:"Promptversion", schemaVersion:"Extraktionsversion", analyzedAtUtc:"Hämtningstid",
  provenance:"Källa", matchesSubmittedUrl:"Matchar inskickad annons", values:"Uppgifter",
  vehicleId:"Fordons-ID", registrationNumber:"Registreringsnummer", revision:"Fordonsrevision",
  make:"Märke",model:"Modell",variant:"Variant",vehicleLabel:"Egen benämning",imageCount:"Bildantal",
  horsepower:"Effekt (hk)",engineDisplacementCubicCentimetres:"Motorvolym (cm³)",colour:"Färg",
  publishedDate:"Publiceringsdatum",updatedDate:"Uppdateringsdatum",sellerType:"Säljartyp",
  status:"Underlagsstatus",hasSavedCostScenario:"Har äldre kalkyl",savedCostScenarioSourceListingVersion:"Äldre kalkylens granskade annonsversion",
  savedCostScenarioOutdated:"Äldre kalkyl behöver annonsgranskning",createdAtUtc:"Skapad",updatedAtUtc:"Senast sparad",
  missingFields:"Saknade uppgifter",label:"Beteckning",unit:"Enhet",consumptionPer100Kilometres:"Förbrukning per 100 km",
  annualVehicleTaxSek:"Årlig fordonsskatt (kr)",energyConsumptions:"Angiven energiförbrukning",url:"Annonsadress",
};
interface ContentRow {path:string; value:string; source?:string}
const valueNames: Record<string, Record<string, string>> = {
  unit: {litre:"liter",kilowattHour:"kWh",kilogram:"kg"},
  status: {complete:"Grunduppgifter kompletta",partial:"Delvis känt",unavailable:"Inga användbara annonsuppgifter"},
  weightCategory: {unspecified:"Viktkategori inte angiven",curb:"Tjänstevikt",gross:"Totalvikt"},
  trailerWeightCategory: {unspecified:"Viktkategori inte angiven",braked:"Bromsad släpvagn",unbraked:"Obromsad släpvagn"},
};
const translated = (key:string, value:string) => valueNames[key]?.[value] ?? value;
function contentRows(value: unknown): ContentRow[] {
  const rows: ContentRow[] = [];
  const visit = (v: unknown, path: string) => {
    if(v && typeof v === "object" && !Array.isArray(v) && !(v instanceof Numeric)) {
      const obj=v as Record<string,unknown>;
      if("provenance" in obj && ("value" in obj || "values" in obj)) {
        const field=path.split(".").at(-1)!.replace(/\[.*$/,""), item=obj.value ?? obj.values;
        const values=inputRows({[field]:item});
        rows.push({path,value:values.map(r=> {
          const key=r.path.split(".").at(-1)!.replace(/\[.*$/,"");
          const text=translated(key,r.value);
          return r.path === field || (Array.isArray(item) && item.every(x => typeof x === "string")) ? text : `${names[key] ?? reportLabel(key)}: ${text}`;
        }).join("\n"),source:inputRows(obj.provenance).map(r=>r.value).join(" · ")});
        return;
      }
      Object.entries(obj).forEach(([k,x])=>visit(x,path?`${path}.${k}`:k));
    } else if(Array.isArray(v) && v.length) v.forEach((x,i)=>visit(x,`${path}[${i+1}]`));
    else {
      const key=path.split(".").at(-1)!;
      rows.push({path,value:inputRows({[key]:v}).map(r=>path.startsWith("missingFields[") ? (names[r.value] ?? reportLabel(r.value)) : translated(key,r.value)).join("\n")});
    }
  };
  visit(value,""); return rows;
}
export function ListingContent({value, title = "Annonsunderlag"}: {value: unknown; title?: string}) {
  const rows = contentRows(value);
  const fieldLabel = (path: string) => path.split(".").map(p => {
    const key = p.replace(/\[.*$/, "");
    return (names[key] ?? reportLabel(key)) + p.slice(key.length);
  }).join(" · ");
  return <section className="listing-content min-w-0" aria-label={title}>
    <h4 className="font-semibold">{title}</h4>
    <p className="text-sm">Annonsuppgifter och säljarpåståenden är källunderlag. De innebär ingen registerverifiering.</p>
    {!!value && typeof value === "object" && "requestedModel" in value && !!value.requestedModel && "sourcePageObserved" in value && value.sourcePageObserved === false &&
      <p className="text-sm">Metadata om öppnad sida saknas. AI-hämtade uppgifter är obekräftade annonsuppgifter.</p>}
    <table className="w-full table-fixed text-left text-sm" aria-label={title}>
      <thead><tr><th colSpan={2} className="report-table-title">{title}</th></tr><tr><th scope="col" className="w-1/3">Uppgift</th><th scope="col">Värde och källa</th></tr></thead>
      <tbody>{rows.map((row, i) => <tr key={i}><th scope="row" className="align-top break-words p-2">{fieldLabel(row.path)}</th>
        <td className="whitespace-pre-wrap break-words p-2" style={{overflowWrap:"anywhere"}}>{row.value}
          {row.source && <p className="mt-1 text-xs">Källa: {row.source}</p>}
        </td></tr>)}</tbody>
    </table>
  </section>;
}
