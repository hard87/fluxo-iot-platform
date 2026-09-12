import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it } from "vitest";
import { useWorkspaceSelection } from "./useWorkspaceSelection";

function SelectionConsumer({ name }: { name: string }) {
  const { workspaceId, setWorkspaceId } = useWorkspaceSelection();

  return (
    <div>
      <output aria-label={`${name} selection`}>{workspaceId ?? "none"}</output>
      <button type="button" onClick={() => setWorkspaceId("workspace-1")}>
        Select from {name}
      </button>
    </div>
  );
}

describe("useWorkspaceSelection", () => {
  beforeEach(() => sessionStorage.clear());

  it("sincroniza a seleção entre consumidores do shell e da página", async () => {
    render(
      <>
        <SelectionConsumer name="shell" />
        <SelectionConsumer name="page" />
      </>
    );

    await userEvent.click(screen.getByRole("button", { name: "Select from page" }));

    expect(screen.getByLabelText("shell selection")).toHaveTextContent("workspace-1");
    expect(screen.getByLabelText("page selection")).toHaveTextContent("workspace-1");
  });
});
