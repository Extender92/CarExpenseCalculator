export function vehicleDeleted(vehicleId: string) {
  window.dispatchEvent(new CustomEvent("vehicle-deleted", { detail: vehicleId }));
}
export function vehicleChanged(vehicleId: string) {
  window.dispatchEvent(new CustomEvent("vehicle-changed", { detail: vehicleId }));
}
export interface AcknowledgedRevision {
  vehicleId: string;
  previous: import("@/features/household/numbers").Numeric | null;
  current: import("@/features/household/numbers").Numeric;
}
export function acknowledgeRevision(detail: AcknowledgedRevision) {
  window.dispatchEvent(new CustomEvent("vehicle-revision-acknowledged", { detail }));
}
