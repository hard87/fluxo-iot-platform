import { describe, expect, it } from "vitest";
import type { TelemetrySeriesResponse } from "../../types";
import { summarizeSeries, timeWindow } from "./operationalMetrics";
function series(values: Array<number|null>): TelemetrySeriesResponse { return { deviceId:"d", metricKey:"gateway.load_1m", valueType:"Numeric", canonicalUnit:null, semanticType:null, truncated:false, points:values.map((numericValue,index)=>({timestampUtc:new Date(index*1000).toISOString(),numericValue,booleanValue:null,textValue:null,sampleCount:null})) }; }
describe("operationalMetrics",()=>{
 it("cria janelas previsíveis",()=>{const now=new Date("2026-09-08T12:00:00Z");expect(timeWindow("6h",now)).toEqual({fromUtc:"2026-09-08T06:00:00.000Z",toUtc:"2026-09-08T12:00:00.000Z"});});
 it("resume valores sem transformar ausência em zero",()=>{expect(summarizeSeries(series([null,10,12,null,14]))).toMatchObject({current:14,min:10,max:14,average:12,trend:"subindo",samples:3});});
 it("mantém série constante como estável",()=>{expect(summarizeSeries(series([7,7,7]))?.trend).toBe("estável");});
 it("retorna nulo quando não há valores",()=>{expect(summarizeSeries(series([null,null]))).toBeNull();});
});
