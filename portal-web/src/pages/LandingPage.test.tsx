import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, it, expect, vi } from "vitest";
import { LandingPage } from "./LandingPage";

const auth = vi.hoisted(() => ({ isAuthenticated: false }));
vi.mock("../hooks/useAuth", () => ({ useAuth: () => auth }));

describe("LandingPage", () => {
  it("direciona visitantes para login e cadastro", () => {
    auth.isAuthenticated = false;
    render(<MemoryRouter><LandingPage /></MemoryRouter>);
    expect(screen.getByRole("link", { name: "Entrar" })).toHaveAttribute("href", "/login");
    for (const name of ["Criar conta", "Começar com o Fluxo", "Criar minha conta"]) {
      expect(screen.getByRole("link", { name })).toHaveAttribute("href", "/register");
    }
    expect(screen.getByRole("link", { name: "Conhecer a plataforma" })).toHaveAttribute("href", "#fl-platform");
  });

  it("direciona os três CTAs para workspaces quando autenticado", () => {
    auth.isAuthenticated = true;
    render(<MemoryRouter><LandingPage /></MemoryRouter>);
    expect(screen.queryByRole("link", { name: "Entrar" })).not.toBeInTheDocument();
    const links = screen.getAllByRole("link", { name: "Abrir plataforma" });
    expect(links).toHaveLength(3);
    links.forEach(link => expect(link).toHaveAttribute("href", "/workspaces"));
  });

  it("alterna os exemplos de aplicação e a regra correspondente", () => {
    auth.isAuthenticated = false;
    render(<MemoryRouter><LandingPage /></MemoryRouter>);
    fireEvent.click(screen.getByRole("button", { name: "Energia" }));
    expect(screen.getByRole("heading", { name: "Entenda a evolução das medições." })).toBeInTheDocument();
    expect(screen.getByText("Acima de 12 kW")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Energia" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "Ambiente" })).toHaveAttribute("aria-pressed", "false");
    fireEvent.click(screen.getByRole("button", { name: "Equipamentos" }));
    expect(screen.getByText("Acima de 75 °C")).toBeInTheDocument();
  });
});
