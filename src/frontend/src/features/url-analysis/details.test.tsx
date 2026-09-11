import {describe, expect, it} from "vitest";
import {render, screen, within} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import {fromOrdinary, n, stringifyExact} from "@/features/household/numbers";
import {completeListingAnalysisResponse, savedListingResponse} from "@/test/listing-analysis";
import {captureReport} from "@/features/comparison/report-model";
import {manualRequest, response} from "@/features/comparison/test-fixtures";
import {analysisResponseToDraft} from "./review-model";
import {buildSavedListingRequest, compareListingDrafts} from "./saved-listings";
import {detailsErrors, detailsFromResponse, detailsToInput, emptyDetails} from "./details";
import {ListingDetailsEditor} from "./ListingDetailsEditor";
import {ListingContent} from "./ListingContent";

const url = completeListingAnalysisResponse.normalizedUrl;
const provenance = completeListingAnalysisResponse.listing.priceSek!.provenance;
describe("complete listing details", () => {
  it.each([
    ["dealer", "Handlare"],
    ["private", "Privat"],
    [null, "Okänt / ej angivet"],
  ] as const)("presents seller type %s in Swedish without changing source content or evidence", (sellerType, label) => {
    const listing = {sellerType: sellerType === null ? null : {value: sellerType, provenance},
      details: {description: {value: "Originaltext: dealer och private.", provenance},
        specifications: [{value: {name: "Originalbeteckning", value: "dealer"}, provenance}]}};
    const before = stringifyExact(listing);
    render(<ListingContent value={listing} />);
    const row = screen.getByRole("rowheader", {name: "Säljartyp"}).closest("tr")!;
    expect(within(row).getByRole("cell")).toHaveTextContent(label);
    if (sellerType) expect(within(row).getByRole("cell")).toHaveTextContent("Obekräftat");
    expect(screen.getByText("Originaltext: dealer och private.")).toBeVisible();
    expect(screen.getByText(/Värde: dealer/)).toBeVisible();
    expect(stringifyExact(listing)).toBe(before);
  });
  it("retains exact numbers, paragraphs, absent timezones, negative answers and entry provenance through save", () => {
    const source = fromOrdinary(completeListingAnalysisResponse);
    source.sources = [];
    source.listing.priceSek!.value = n("28888.12345678901234567890123");
    source.listing.odometerKilometres!.value = n("130000.12345678901234567890123");
    source.listing.details = {description:{value:"Stycke ett.\n\nStycke två.",provenance},
      weightKilograms:{value:n("1370.123456789012345678901234"),provenance}, doors:{value:n(0),provenance},
      updatedLocalDateTime:{value:"2026-09-08T16:35:00",provenance}, updatedTimeZone:null,
      specifications:[],sellerAnswers:[{value:{question:"Har bilen några skulder?",answer:"Nej"},provenance}]};
    const draft = analysisResponseToDraft(source);
    const built = buildSavedListingRequest(url,url,source,draft);
    expect(built.errors).toEqual({});
    const input = built.request!.listing;
    expect(input.sources).toEqual([]);
    expect(input.draft.priceSek!.value).toEqual(source.listing.priceSek!.value);
    expect(input.draft.odometerKilometres!.value).toEqual(source.listing.odometerKilometres!.value);
    expect(input.draft.details).toMatchObject(source.listing.details!);
    expect(stringifyExact(input)).toContain('"value":1370.123456789012345678901234');
    expect(input.draft.details!.description!.provenance.verification).toBe("unverified");
  });
  it("distinguishes unknown and explicitly empty collections without promoting evidence", () => {
    const form = emptyDetails();
    expect(detailsToInput(form,url)!.specifications).toBeNull();
    form.specifications.mode = "empty";
    expect(detailsToInput(form,url)!.specifications).toEqual([]);
    expect(detailsFromResponse({specifications:[],sellerAnswers:null}).specifications.mode).toBe("empty");
    expect(detailsFromResponse(null).sellerAnswers.mode).toBe("unknown");
  });
  it("rejects oversized multibyte content and malformed values without truncation", () => {
    const form = emptyDetails();
    form.fields.description.input = "å".repeat(32000);
    expect(detailsErrors(form)).toEqual({});
    form.fields.description.input += "ö";
    form.fields.seats.input = "fel";
    form.specifications = {mode:"values",entries:Array.from({length:101},(_,i)=>({id:String(i),first:"Namn",second:"Värde",provenance}))};
    expect(Object.keys(detailsErrors(form))).toEqual(expect.arrayContaining(["details.description","details.seats","details.specifications"]));
    expect(form.fields.description.input).toHaveLength(32001);
  });
  it("shows Swedish explicit weight choices and changes only the edited field provenance", async () => {
    const form = detailsFromResponse({description:{value:"Annonsens ord",provenance}});
    let changed = form;
    render(<ListingDetailsEditor value={form} url={url} disabled={false} errors={{}} onChange={value=>{changed=value;}} />);
    await userEvent.selectOptions(screen.getByLabelText("Viktkategori"),"curb");
    expect(changed.fields.weightCategory.provenance!.verification).toBe("userConfirmed");
    expect(changed.fields.description.provenance!.verification).toBe("unverified");
  });
  it("does not lose a difference beyond JavaScript precision during listing replacement review", () => {
    const a = analysisResponseToDraft(completeListingAnalysisResponse);
    const b = analysisResponseToDraft(completeListingAnalysisResponse);
    a.fields.priceSek.input = "28888.000000000000001";
    b.fields.priceSek.input = "28888.000000000000002";
    expect(compareListingDrafts(a,b).map(x=>x.key)).toContain("priceSek");
  });
  it("captures all listing content independently and renders source text safely", () => {
    const source = response(manualRequest());
    source.mode = "stored";
    const listing = fromOrdinary(savedListingResponse);
    listing.vehicleId = source.views.baseline.candidates[0].vehicleId;
    listing.revision = n("9007199254740993");
    listing.listing.details = {description:{value:"<script>Text som aldrig körs</script>\n\nÅäö",provenance}};
    source.listings = [listing];
    const report = captureReport(source,"cost");
    listing.listing.details.description!.value = "Senare annons";
    expect(report.response.listings![0].listing.details!.description!.value).toContain("Åäö");
    expect(report.response.listings![0].revision.text).toBe("9007199254740993");
    const {container} = render(<ListingContent value={report.response.listings![0]} />);
    expect(container.querySelector("script")).toBeNull();
    expect(screen.getByText(/Text som aldrig körs/)).toBeVisible();
  });
});
