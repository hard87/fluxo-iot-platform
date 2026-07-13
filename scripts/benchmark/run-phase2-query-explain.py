#!/usr/bin/env python3
import argparse, uuid
from datetime import datetime, timezone
from pathlib import Path
import psycopg
def parse_connection(value):
    if ";" not in value: return value
    aliases={"host":"host","port":"port","database":"dbname","username":"user","password":"password"}; parts=[]
    for item in value.split(";"):
        if "=" in item:
            key,val=item.split("=",1); mapped=aliases.get(key.strip().lower())
            if mapped: parts.append(f"{mapped}={val.strip()}")
    return " ".join(parts)

def main():
    p=argparse.ArgumentParser(); p.add_argument("--connection-string",required=True); p.add_argument("--workspace-id",type=uuid.UUID,required=True)
    p.add_argument("--output",default="docs/benchmarks/phase2-query-explain-2026-07-12.md"); p.add_argument("--only",choices=[f"Q{i}" for i in range(1,8)]); a=p.parse_args()
    with psycopg.connect(parse_connection(a.connection_string)) as c, c.cursor() as cur:
        cur.execute('SELECT "Identifier" FROM devices WHERE "WorkspaceId"=%s ORDER BY "Identifier" LIMIT 10',(a.workspace_id,)); devices=[x[0] for x in cur]
        cur.execute('SELECT "Id" FROM metric_definitions WHERE "WorkspaceId"=%s ORDER BY "MetricKey" LIMIT 10',(a.workspace_id,)); metrics=[x[0] for x in cur]
        d1,m1=devices[0],metrics[0]
        now=datetime.now(timezone.utc)
        count,partitions,hot=5_115_083,5,500_000
        queries={
        "Q1":('SELECT * FROM telemetry_points WHERE "WorkspaceId"=%s AND "DeviceId"=%s AND "MetricDefinitionId"=%s AND "OccurredAtUtc">=%s-interval \'24 hours\' AND "OccurredAtUtc"<=%s ORDER BY "OccurredAtUtc" LIMIT 20000',(a.workspace_id,d1,m1,now,now)),
        "Q2":('SELECT * FROM telemetry_points WHERE "WorkspaceId"=%s AND "DeviceId"=ANY(%s) AND "MetricDefinitionId"=%s AND "OccurredAtUtc">=%s-interval \'24 hours\' AND "OccurredAtUtc"<=%s ORDER BY "OccurredAtUtc" LIMIT 20000',(a.workspace_id,devices[:5],m1,now,now)),
        "Q3":('SELECT * FROM telemetry_points WHERE "WorkspaceId"=%s AND "DeviceId"=%s AND "MetricDefinitionId"=ANY(%s) AND "OccurredAtUtc">=%s-interval \'24 hours\' AND "OccurredAtUtc"<=%s ORDER BY "OccurredAtUtc" LIMIT 20000',(a.workspace_id,d1,metrics[:5],now,now)),
        "Q4":('SELECT * FROM telemetry_points WHERE "WorkspaceId"=%s AND "DeviceId"=ANY(%s) AND "MetricDefinitionId"=ANY(%s) AND "OccurredAtUtc">=%s-interval \'24 hours\' AND "OccurredAtUtc"<=%s ORDER BY "OccurredAtUtc" LIMIT 20000',(a.workspace_id,devices,metrics,now,now)),
        "Q5":('SELECT date_bin(interval \'1 hour\',"OccurredAtUtc",timestamptz \'2001-01-01\') bucket,avg("NumericValue"),min("NumericValue"),max("NumericValue"),sum("NumericValue"),count(*) FROM telemetry_points WHERE "WorkspaceId"=%s AND "DeviceId"=%s AND "MetricDefinitionId"=%s AND "OccurredAtUtc">=%s-interval \'30 days\' AND "OccurredAtUtc"<=%s GROUP BY bucket ORDER BY bucket',(a.workspace_id,d1,m1,now,now)),
        "Q6":('SELECT "DeviceId","MetricDefinitionId",date_bin(interval \'6 hours\',"OccurredAtUtc",timestamptz \'2001-01-01\') bucket,avg("NumericValue") FROM telemetry_points WHERE "WorkspaceId"=%s AND "DeviceId"=ANY(%s) AND "MetricDefinitionId"=ANY(%s) AND "OccurredAtUtc">=%s-interval \'90 days\' AND "OccurredAtUtc"<=%s GROUP BY "DeviceId","MetricDefinitionId",bucket ORDER BY bucket',(a.workspace_id,devices,metrics,now,now)),
        "Q7":('SELECT DISTINCT ON ("DeviceId","MetricDefinitionId",bucket) "DeviceId","MetricDefinitionId",bucket,"NumericValue" FROM (SELECT "DeviceId","MetricDefinitionId",date_bin(interval \'15 minutes\',"OccurredAtUtc",timestamptz \'2001-01-01\') bucket,"OccurredAtUtc","NumericValue" FROM telemetry_points WHERE "WorkspaceId"=%s AND "DeviceId"=ANY(%s) AND "MetricDefinitionId"=ANY(%s) AND "OccurredAtUtc">=%s-interval \'7 days\' AND "OccurredAtUtc"<=%s) p ORDER BY "DeviceId","MetricDefinitionId",bucket,"OccurredAtUtc" DESC',(a.workspace_id,devices[:5],metrics[:5],now,now))}
        path=Path(a.output)
        out=[] if a.only and path.exists() else ["# Benchmark Fase 2 — Query API — 2026-07-12","",f"- Workspace: `{a.workspace_id}`",f"- TelemetryPoint: **{count}**",f"- Partições com dados: **{partitions}**",f"- Maior série: **{hot}** pontos","- Índices novos antes da medição: **nenhum**",""]
        selected={a.only:queries[a.only]} if a.only else queries
        for name,(query,params) in selected.items():
            print(f"running {name}",flush=True)
            cur.execute("EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT) "+query,params); plan="\n".join(x[0] for x in cur)
            print(f"completed {name}",flush=True)
            out += [f"## {name}","","```sql",plan,"```",""]
        mode="a" if a.only and path.exists() else "w"
        with path.open(mode,encoding="utf-8") as f: f.write("\n".join(out)+"\n")
        print(a.output)
if __name__=="__main__": main()
