import type { HouseholdWorkspace } from "@/features/household/workspace";
import type { ComparisonWorkspace } from "@/features/comparison/workspace";
import { sameNumber } from "@/features/household/numbers";
import type { EditingResource } from "./EditorDialog";

export function profileResource(workspace: HouseholdWorkspace): EditingResource {
  return { key: "profile", label: "Hushållsprofil", dirty: workspace.state.profileDirty, busy: !!workspace.state.busy,
    discard: () => workspace.discardProfile(), save: async () => {
      const revision = workspace.state.savedProfile.revision;
      await workspace.saveProfile();
      return !sameNumber(revision, workspace.state.savedProfile.revision);
    } };
}
export function costResource(workspace: HouseholdWorkspace): EditingResource {
  return { key: "cost", label: "Kalkyl", dirty: workspace.state.active.dirty, busy: !!workspace.state.busy,
    discard: () => workspace.discardActive(), save: async () => {
      const active = workspace.state.active;
      if (active.fromDraft) {
        await workspace.saveDraft();
        return !sameNumber(active.draftRevision, workspace.state.active.draftRevision);
      }
      await workspace.saveVehicle();
      return !sameNumber(active.baseRevision, workspace.state.active.baseRevision);
    } };
}
export function factsResource(workspace: ComparisonWorkspace, id: string): EditingResource {
  return { key: "facts", label: "Jämförelsefakta", dirty: !!workspace.state.facts[id]?.dirty, busy: !!workspace.state.busy,
    discard: () => workspace.discardFacts(id), save: async () => {
      const revision = workspace.state.facts[id]?.base.revision;
      await workspace.saveFacts(id);
      return !sameNumber(revision, workspace.state.facts[id]?.base.revision);
    } };
}
export function rulesResource(workspace: ComparisonWorkspace): EditingResource {
  return { key: "rules", label: "Köpkrav och prioriteringar", dirty: workspace.state.rulesDirty, busy: !!workspace.state.busy,
    discard: () => workspace.discardRules(), save: async () => {
      const revision = workspace.state.savedRules.revision;
      await workspace.saveRules();
      return !sameNumber(revision, workspace.state.savedRules.revision);
    } };
}
