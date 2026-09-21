export const guidePreferenceKey = "car-expense:first-start-dismissed";
export function guideDismissed() {
  try { return localStorage.getItem(guidePreferenceKey) === "yes"; } catch { return false; }
}
