import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { TelemetryExplorerPage } from "./TelemetryExplorerPage";
import * as deviceService from "../services/api/deviceService";
import * as telemetryService from "../services/api/telemetryService";
import type { MetricDefinitionResponse } from "../types";

vi.mock("../hooks/useAuth", () => ({ useAuth: () => ({ token: "token" }) }));
vi.mock("../services/api/deviceService", () => ({ listDevices: vi.fn() }));
vi.mock("../services/api/telemetryService", () => ({ listMetricDefinitions: vi.fn(), queryTelemetry: vi.fn() }));

const device = { id:"d1",tenantId:"tenant",workspaceId:"w1",name:"Motor 1",identifier:"motor-1",category:"Sensor",
  isActive:true,createdAtUtc:"2026-01-01T00:00:00Z",operationalStatus:"Online" as const };
const numeric: MetricDefinitionResponse = { id:"m1",metricKey:"temperature_c",displayName:"Temperatura",valueType:"Numeric",
  semanticType:"temperature",canonicalUnit:"°C",status:"Active",isQueryable:true };
const textMetric: MetricDefinitionResponse = { ...numeric,id:"m2",metricKey:"machine_state",displayName:"Estado",valueType:"Text",
  semanticType:"state",canonicalUnit:null };

function renderPage() { return render(<MemoryRouter initialEntries={["/workspaces/w1/explorer"]}><Routes>
  <Route path="/workspaces/:workspaceId/explorer" element={<TelemetryExplorerPage/>}/></Routes></MemoryRouter>); }
async function selectBase(metrics=[numeric]) { vi.mocked(telemetryService.listMetricDefinitions).mockResolvedValue(metrics);
  renderPage(); await screen.findByText("Motor 1"); await userEvent.click(screen.getByLabelText("Motor 1"));
  await screen.findByText(/temperature_c/); await userEvent.click(screen.getByLabelText(/temperature_c/)); }

describe("TelemetryExplorerPage", () => {
  beforeEach(() => { vi.clearAllMocks(); vi.mocked(deviceService.listDevices).mockResolvedValue([device]); });

  it("desabilita Executar consulta sem device e métrica", async () => {
    renderPage(); expect(await screen.findByRole("button",{name:"Executar consulta"})).toBeDisabled();
  });

  it("reduz aggregations à interseção dos ValueTypes", async () => {
    await selectBase([numeric,textMetric]); await userEvent.click(screen.getByLabelText(/machine_state/));
    const options = [...screen.getByLabelText("Agregação").querySelectorAll("option")].map(x=>x.textContent);
    expect(options).toEqual(["raw","count","last"]); expect(options).not.toContain("avg");
  });

  it("renderiza Sem dados neste período", async () => {
    vi.mocked(telemetryService.queryTelemetry).mockResolvedValue({workspaceId:"w1",fromUtc:"",toUtc:"",aggregation:"raw",bucket:null,
      series:[{deviceId:"d1",metricKey:"temperature_c",valueType:"Numeric",canonicalUnit:"°C",semanticType:null,points:[],truncated:false}],
      meta:{totalPoints:0,maxPointsAllowed:20000,executionTimeMs:1}});
    await selectBase(); await userEvent.click(screen.getByRole("button",{name:"Executar consulta"}));
    expect(await screen.findByText("Sem dados neste período")).toBeInTheDocument();
  });

  it("separa CanonicalUnit diferentes em painéis", async () => {
    const amp: MetricDefinitionResponse={...numeric,id:"m3",metricKey:"current_a",displayName:"Corrente",canonicalUnit:"A"};
    vi.mocked(telemetryService.queryTelemetry).mockResolvedValue({workspaceId:"w1",fromUtc:"",toUtc:"",aggregation:"raw",bucket:null,
      series:[numeric,amp].map((m,i)=>({deviceId:"d1",metricKey:m.metricKey,valueType:"Numeric",canonicalUnit:m.canonicalUnit,
        semanticType:null,points:[{timestampUtc:`2026-07-12T12:00:0${i}Z`,numericValue:i,booleanValue:null,textValue:null,sampleCount:null}],truncated:false})),
      meta:{totalPoints:2,maxPointsAllowed:20000,executionTimeMs:1}});
    await selectBase([numeric,amp]); await userEvent.click(screen.getByLabelText(/current_a/));
    await userEvent.click(screen.getByRole("button",{name:"Executar consulta"}));
    await waitFor(()=>expect(screen.getByRole("heading",{name:"Numeric · °C"})).toBeInTheDocument());
    expect(screen.getByRole("heading",{name:"Numeric · A"})).toBeInTheDocument();
  });

  it("renderiza banner quando Truncated=true", async () => {
    vi.mocked(telemetryService.queryTelemetry).mockResolvedValue({workspaceId:"w1",fromUtc:"",toUtc:"",aggregation:"raw",bucket:null,
      series:[{deviceId:"d1",metricKey:"temperature_c",valueType:"Numeric",canonicalUnit:"°C",semanticType:null,points:[],truncated:true}],
      meta:{totalPoints:0,maxPointsAllowed:20000,executionTimeMs:1}});
    await selectBase(); await userEvent.click(screen.getByRole("button",{name:"Executar consulta"}));
    expect(await screen.findByText(/Resultado truncado/)).toBeInTheDocument();
  });
});
