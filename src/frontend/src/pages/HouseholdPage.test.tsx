import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { afterEach, expect, it, vi } from "vitest";
import { householdApi } from "@/features/household/api";
import { HouseholdWorkspace } from "@/features/household/workspace";
import { WorkspaceContext } from "@/features/household/use-workspace";
import { id1, savedVehicle } from "@/features/household/test-fixtures";
import { HouseholdPage } from "./HouseholdPage";

let workspace: HouseholdWorkspace;
afterEach(() => {
  workspace?.dispose();
  vi.restoreAllMocks();
});

it("starts a new car without reopening the previous URL, and can return with Back", async () => {
  workspace = new HouseholdWorkspace();
  vi.spyOn(workspace, "start").mockImplementation(() => {});
  vi.spyOn(workspace, "onFocus").mockImplementation(() => {});
  const read = vi.spyOn(householdApi, "vehicle").mockResolvedValue(savedVehicle());
  const user = userEvent.setup();
  const router = createMemoryRouter(
    [{ path: "/manual", element: <HouseholdPage /> }],
    { initialEntries: [`/manual?vehicleId=${id1}`] },
  );
  render(
    <WorkspaceContext.Provider value={workspace}>
      <RouterProvider router={router} />
    </WorkspaceContext.Provider>,
  );
  const registration = screen.getByLabelText("Registreringsnummer", { exact: true });
  await waitFor(() => expect(registration).toHaveValue("ABC123"));
  expect(registration).toBeDisabled();
  expect(read).toHaveBeenCalledTimes(1);

  await user.click(screen.getByRole("button", { name: "Ny bil" }));
  await waitFor(() => expect(router.state.location.search).toBe(""));
  expect(registration).toBeEnabled();
  expect(registration).toHaveValue("");
  expect(read).toHaveBeenCalledTimes(1);

  await act(() => router.navigate(-1));
  await waitFor(() => expect(registration).toHaveValue("ABC123"));
  expect(registration).toBeDisabled();
  expect(read).toHaveBeenCalledTimes(2);
});
