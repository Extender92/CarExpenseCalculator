export function vehicleDeleted(vehicleId: string) {
  window.dispatchEvent(new CustomEvent("vehicle-deleted", { detail: vehicleId }));
}
export function vehicleChanged(vehicleId: string) {
  window.dispatchEvent(new CustomEvent("vehicle-changed", { detail: vehicleId }));
}
