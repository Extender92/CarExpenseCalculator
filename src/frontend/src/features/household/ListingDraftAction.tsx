import { useRef, useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import type { ListingWorkspaceItem } from "@/features/url-analysis/review-model";
import { householdApi } from "./api";
import { reviewedListingForDraft } from "./listing-draft";
import { useOptionalWorkspace } from "./use-workspace";

export function ListingDraftAction({ item }: { item: ListingWorkspaceItem }) {
  const workspace = useOptionalWorkspace();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  async function save() {
    setBusy(true);
    setMessage(null);
    try {
      const { registrationNumber, listing } = reviewedListingForDraft(item);
      const [slot, vehicles] = await Promise.all([
        householdApi.draft(),
        householdApi.list(),
      ]);
      if (!mounted.current) return;
      const existing = vehicles.find(
        (vehicle) => vehicle.registrationNumber === registrationNumber,
      );
      if (
        existing &&
        (!item.saved ||
          item.saved.vehicleId !== existing.vehicleId ||
          (item.householdBaseRevision ?? String(item.saved.revision)) !==
            existing.revision.text)
      )
        throw new Error(
          "Bilen finns redan eller har ändrats. Öppna och granska dess aktuella annons innan du sparar ett utkast.",
        );
      if (item.saved && !existing)
        throw new Error(
          "Bilen har raderats. Detta äldre annonskort får inte återskapa den.",
        );
      const replacement =
        !!slot.input && slot.input.registrationNumber !== registrationNumber;
      if (
        slot.input &&
        !window.confirm(
          `Ersätta det gemensamma utkastet för ${slot.input.registrationNumber} med detta granskade annonsunderlag för ${registrationNumber}?`,
        )
      )
        return;
      const input = {
        registrationNumber,
        listing,
        cost: null,
        baseVehicleId: existing?.vehicleId ?? null,
        baseVehicleRevision: existing?.revision ?? null,
      };
      if (workspace)
        await workspace.saveListingDraft(input, slot.revision, replacement);
      else await householdApi.saveDraft(input, slot.revision, replacement);
      if (mounted.current)
        setMessage(
          "Annonsutkastet har sparats. Öppna det gemensamma utkastet i hushållskalkylen för att ta det i bruk.",
        );
    } catch (error) {
      if (mounted.current) setMessage((error as Error).message);
    } finally {
      if (mounted.current) setBusy(false);
    }
  }
  return (
    <div>
      <Button
        type="button"
        size="sm"
        variant="secondary"
        disabled={
          busy ||
          item.saving ||
          ["queued", "analyzing", "retrying"].includes(item.phase)
        }
        onClick={() => void save()}
      >
        {busy ? "Sparar utkast…" : "Spara gemensamt annonsutkast"}
      </Button>
      {message && (
        <p role="status" className="mt-2 max-w-lg text-sm text-amber-200">
          {message}
        </p>
      )}
    </div>
  );
}
