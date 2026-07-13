#!/usr/bin/env python3
"""Seed deterministic TelemetryPoint history directly with PostgreSQL binary COPY."""
from __future__ import annotations
import argparse, calendar, random, uuid
from datetime import datetime, timedelta, timezone
import psycopg
from psycopg import sql

def parse_connection(value: str) -> str:
    if ";" not in value: return value
    aliases={"host":"host","port":"port","database":"dbname","username":"user","user id":"user","password":"password"}
    parts=[]
    for item in value.split(";"):
        if not item.strip(): continue
        key,val=item.split("=",1); mapped=aliases.get(key.strip().lower())
        if mapped: parts.append(f"{mapped}={val.strip()}")
    return " ".join(parts)

def subtract_months(value: datetime, months: int) -> datetime:
    total=value.year*12+value.month-1-months; year,month=divmod(total,12)
    return value.replace(year=year,month=month+1,day=min(value.day,calendar.monthrange(year,month+1)[1]))

def month_floor(value: datetime) -> datetime: return value.replace(day=1,hour=0,minute=0,second=0,microsecond=0)
def add_month(value: datetime) -> datetime: return (value.replace(day=28)+timedelta(days=4)).replace(day=1)

def main() -> None:
    p=argparse.ArgumentParser()
    p.add_argument("--connection-string",required=True); p.add_argument("--workspace-id",required=True,type=uuid.UUID)
    p.add_argument("--devices",type=int,required=True); p.add_argument("--metrics",type=int,required=True)
    p.add_argument("--months-back",type=int,required=True); p.add_argument("--points-per-day-per-series",type=int,required=True)
    p.add_argument("--seed",type=int,default=404); p.add_argument("--batch-size",type=int,default=50_000)
    args=p.parse_args()
    if min(args.devices,args.metrics,args.months_back,args.points_per_day_per_series,args.batch_size)<=0: p.error("numeric arguments must be positive")
    rng=random.Random(args.seed); now=datetime.now(timezone.utc); start=subtract_months(now,args.months_back); tenant="benchmark"
    with psycopg.connect(parse_connection(args.connection_string)) as conn:
        with conn.cursor() as cur:
            cur.execute('SELECT "TenantId" FROM workspaces WHERE "Id"=%s AND "IsActive"=true',(args.workspace_id,)); row=cur.fetchone()
            if not row: raise SystemExit("workspace does not exist or is inactive")
            tenant=row[0]
            cur.execute('SELECT "Id","Identifier" FROM devices WHERE "WorkspaceId"=%s ORDER BY "CreatedAtUtc","Id" LIMIT %s',(args.workspace_id,args.devices)); devices=cur.fetchall()
            if len(devices)!=args.devices: raise SystemExit(f"workspace has {len(devices)} devices; {args.devices} required")
            metric_keys=[f"query_benchmark_{i:02d}" for i in range(args.metrics)]
            for key in metric_keys:
                cur.execute('INSERT INTO metric_definitions ("Id","WorkspaceId","TenantId","MetricKey","DisplayName","ValueType","Status","IsQueryable","IsAlertable","CreatedAtUtc","UpdatedAtUtc") VALUES (%s,%s,%s,%s,%s,\'Numeric\',\'Active\',true,false,%s,%s) ON CONFLICT ("WorkspaceId","MetricKey") DO NOTHING',(uuid.uuid5(args.workspace_id,key),args.workspace_id,tenant,key,key,now,now))
            cur.execute('SELECT "Id","MetricKey" FROM metric_definitions WHERE "WorkspaceId"=%s AND "MetricKey"=ANY(%s) ORDER BY "MetricKey"',(args.workspace_id,metric_keys)); metrics=cur.fetchall()
            boundary=month_floor(start)
            while boundary<=month_floor(now):
                nxt=add_month(boundary); name=f"telemetry_points_{boundary:%Y_%m}"
                cur.execute(sql.SQL('CREATE TABLE IF NOT EXISTS {} PARTITION OF telemetry_points FOR VALUES FROM ({}) TO ({})').format(
                    sql.Identifier(name), sql.Literal(boundary), sql.Literal(nxt))); boundary=nxt
        conn.commit()
        span_seconds=(now-start).total_seconds(); days=max(1,(now.date()-start.date()).days+1)
        target_hot=500_000; baseline_hot=days*args.points_per_day_per_series; hot_extra=max(0,target_hot-baseline_hot)
        series=[(d[1],m[0]) for d in devices for m in metrics]
        total=days*args.points_per_day_per_series*len(series)+hot_extra
        print(f"seeding {total:,} points across {len(series)} series and {days} days; hot-series extra={hot_extra:,}")
        sequence=1; written=0
        def batches():
            nonlocal sequence
            batch=[]
            for series_index,(device_identifier,metric_id) in enumerate(series):
                count=days*args.points_per_day_per_series+(hot_extra if series_index==0 else 0)
                for i in range(count):
                    fraction=(i+0.5)/count; occurred=start+timedelta(seconds=span_seconds*fraction)
                    value=series_index*10+20+rng.random()*5
                    batch.append((device_identifier,metric_id,occurred,value))
                    if len(batch)>=args.batch_size: yield batch; batch=[]
            if batch: yield batch
        for batch in batches():
            ingestion_id=uuid.uuid4(); occurred=batch[0][2]
            with conn.cursor() as cur:
                cur.execute('INSERT INTO telemetry_ingestion_records ("Id","TenantId","WorkspaceId","DeviceId","MessageType","Topic","SchemaVersion","Sequence","OccurredAtUtc","ReceivedAtUtc","PayloadJson") VALUES (%s,%s,%s,%s,\'benchmark\',\'benchmark/direct\',\'2\',%s,%s,%s,\'{}\')',(ingestion_id,tenant,args.workspace_id,batch[0][0],sequence,occurred,now)); sequence+=1
                with cur.copy('COPY telemetry_points ("Id","TenantId","WorkspaceId","DeviceId","MetricDefinitionId","OccurredAtUtc","NumericValue","BooleanValue","TextValue","IngestionRecordId") FROM STDIN (FORMAT BINARY)') as copy:
                    copy.set_types(["uuid","varchar","uuid","varchar","uuid","timestamptz","float8","bool","text","uuid"])
                    for device_identifier,metric_id,at,value in batch:
                        copy.write_row((uuid.uuid4(),tenant,args.workspace_id,device_identifier,metric_id,at,value,None,None,ingestion_id))
            conn.commit(); written+=len(batch); print(f"{written:,}/{total:,}",flush=True)
    print("seed complete")

if __name__=="__main__": main()
