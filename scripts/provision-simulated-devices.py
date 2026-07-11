#!/usr/bin/env python3
"""Provision N simulated devices through the real Fluxo HTTP API and dump a credentials
manifest consumable by mqtt-device-simulator.py's --credentials-file option.

Uses only the Python standard library.
"""

from __future__ import annotations

import argparse
import json
import time
import urllib.error
import urllib.request


def call(method: str, url: str, body: dict | None = None, token: str | None = None) -> dict:
    data = json.dumps(body).encode("utf-8") if body is not None else None
    request = urllib.request.Request(url, data=data, method=method)
    request.add_header("Content-Type", "application/json")
    if token:
        request.add_header("Authorization", f"Bearer {token}")

    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            payload = response.read()
            return json.loads(payload) if payload else {}
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"{method} {url} -> HTTP {exc.code}: {detail}") from exc


def device_id_for(prefix: str, index: int) -> str:
    return f"{prefix}-{index:03d}"


def main() -> int:
    parser = argparse.ArgumentParser(description="Provision simulated Fluxo devices via the API.")
    parser.add_argument("--api", default="http://localhost:5000")
    parser.add_argument("--email", required=True)
    parser.add_argument("--password", required=True)
    parser.add_argument("--tenant-id", required=True)
    parser.add_argument("--workspace-name", required=True)
    parser.add_argument("--device-prefix", default="sim-device")
    parser.add_argument("--devices", type=int, default=10)
    parser.add_argument("--category", type=int, default=1)
    parser.add_argument("--output", default="simulated-devices.json")
    args = parser.parse_args()

    try:
        call("POST", f"{args.api}/api/auth/register", {"email": args.email, "password": args.password})
    except RuntimeError as exc:
        if "409" not in str(exc):
            raise

    login = call("POST", f"{args.api}/api/auth/login", {"email": args.email, "password": args.password})
    token = login["accessToken"]

    workspace = call(
        "POST",
        f"{args.api}/api/workspaces",
        {"name": f"{args.workspace_name}-{int(time.time())}", "tenantId": args.tenant_id},
        token,
    )
    workspace_id = workspace["id"]
    print(f"workspaceId={workspace_id} tenantId={workspace['tenantId']}")

    manifest = []
    for index in range(1, args.devices + 1):
        device_id = device_id_for(args.device_prefix, index)
        device = call(
            "POST",
            f"{args.api}/api/workspaces/{workspace_id}/devices/provision",
            {"name": device_id, "identifier": device_id, "category": args.category},
            token,
        )
        manifest.append(
            {
                "deviceId": device_id,
                "username": device["credentialUsername"],
                "password": device["provisioningSecret"],
                "topic": device["mqttPublishTopic"],
            }
        )
        print(f"provisioned {index}/{args.devices}: {device_id}")

    with open(args.output, "w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=2)

    print(f"wrote {len(manifest)} device credentials to {args.output}")
    print(
        "next: python scripts/mqtt-device-simulator.py "
        f"--tenant-id {workspace['tenantId']} --workspace-id {workspace_id} "
        f"--device-prefix {args.device_prefix} --devices {args.devices} "
        f"--credentials-file {args.output} --host localhost --port 1883 "
        "--interval-seconds 1 --messages-per-device 300"
    )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
