#!/usr/bin/env python3
"""Minimal MQTT telemetry simulator for Fluxo.

Uses only the Python standard library and publishes MQTT 3.1.1 QoS 0 messages.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import json
import os
import random
import socket
import ssl
import struct
import threading
import time
from dataclasses import dataclass
from datetime import datetime, timezone


@dataclass
class Counters:
    published: int = 0
    errors: int = 0
    reconnects: int = 0


class SafeStats:
    def __init__(self) -> None:
        self._lock = threading.Lock()
        self.counters = Counters()

    def add_published(self) -> None:
        with self._lock:
            self.counters.published += 1

    def add_error(self) -> None:
        with self._lock:
            self.counters.errors += 1

    def add_reconnect(self) -> None:
        with self._lock:
            self.counters.reconnects += 1

    def snapshot(self) -> Counters:
        with self._lock:
            return Counters(
                published=self.counters.published,
                errors=self.counters.errors,
                reconnects=self.counters.reconnects,
            )


def encode_string(value: str) -> bytes:
    data = value.encode("utf-8")
    return struct.pack("!H", len(data)) + data


def encode_remaining_length(length: int) -> bytes:
    encoded = bytearray()
    while True:
        digit = length % 128
        length //= 128
        if length > 0:
            digit |= 0x80
        encoded.append(digit)
        if length == 0:
            return bytes(encoded)


def mqtt_packet(packet_type: int, payload: bytes) -> bytes:
    return bytes([packet_type]) + encode_remaining_length(len(payload)) + payload


def build_connect_packet(client_id: str, username: str | None, password: str | None) -> bytes:
    flags = 0x02
    payload = encode_string(client_id)

    if username:
        flags |= 0x80
        payload += encode_string(username)

    if password:
        flags |= 0x40
        payload += encode_string(password)

    variable_header = encode_string("MQTT") + bytes([4, flags]) + struct.pack("!H", 60)
    return mqtt_packet(0x10, variable_header + payload)


def build_publish_packet(topic: str, message: dict) -> bytes:
    payload = json.dumps(message, separators=(",", ":")).encode("utf-8")
    variable_header = encode_string(topic)
    return mqtt_packet(0x30, variable_header + payload)


def connect_socket(args: argparse.Namespace, client_id: str, username: str | None, password: str | None) -> socket.socket:
    raw_socket = socket.create_connection((args.host, args.port), timeout=args.timeout_seconds)

    if args.tls:
        context = ssl.create_default_context(cafile=args.ca_file)
        if args.insecure_tls:
            context.check_hostname = False
            context.verify_mode = ssl.CERT_NONE

        server_hostname = args.tls_server_name or args.host
        sock = context.wrap_socket(raw_socket, server_hostname=server_hostname)
    else:
        sock = raw_socket

    sock.settimeout(args.timeout_seconds)
    sock.sendall(build_connect_packet(client_id, username, password))
    connack = sock.recv(4)

    if len(connack) < 4 or connack[0] != 0x20 or connack[3] != 0:
        raise RuntimeError(f"MQTT CONNACK rejected for {client_id}: {connack!r}")

    return sock


def device_id_for(args: argparse.Namespace, index: int) -> str:
    return f"{args.device_prefix}-{index:03d}"


def topic_for(args: argparse.Namespace, device_id: str) -> str:
    return (
        f"fluxo/tenants/{args.tenant_id}/workspaces/{args.workspace_id}"
        f"/devices/{device_id}/telemetry"
    )


def payload_for(args: argparse.Namespace, device_id: str, sequence: int) -> dict:
    now = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    base_temperature = 23.0 + random.random() * 4.0
    base_humidity = 55.0 + random.random() * 12.0

    return {
        "schemaVersion": "1.0",
        "tenantId": args.tenant_id,
        "workspaceId": args.workspace_id,
        "deviceId": device_id,
        "messageType": "telemetry",
        "timestampUtc": now,
        "sequence": sequence,
        "firmwareVersion": "fluxo-simulator-0.1.0",
        "metrics": {
            "temperature": round(base_temperature, 2),
            "humidity": round(base_humidity, 2),
            "battery": round(3.75 + random.random() * 0.35, 2),
            "rssi": random.randint(-78, -42),
            "uptimeSec": sequence * int(max(args.interval_seconds, 1)),
        },
    }


def format_template(template: str | None, args: argparse.Namespace, index: int, device_id: str) -> str | None:
    if not template:
        return None

    return template.format(
        index=index,
        device=device_id,
        tenant=args.tenant_id,
        workspace=args.workspace_id,
    )


def simulate_device(
    index: int,
    args: argparse.Namespace,
    stats: SafeStats,
    credentials: dict[str, tuple[str, str]] | None = None,
) -> None:
    device_id = device_id_for(args, index)
    topic = topic_for(args, device_id)

    if credentials is not None:
        if device_id not in credentials:
            stats.add_error()
            print(f"error device={device_id}: no entry in --credentials-file")
            return
        username, password = credentials[device_id]
    else:
        username = format_template(args.username_template, args, index, device_id)
        password = args.password or (os.getenv(args.password_env) if args.password_env else None)

    client_id = f"{args.client_id_prefix}-{device_id}"
    sock: socket.socket | None = None

    for sequence in range(1, args.messages_per_device + 1):
        try:
            if sock is None:
                sock = connect_socket(args, client_id, username, password)

            sock.sendall(build_publish_packet(topic, payload_for(args, device_id, sequence)))
            stats.add_published()

            if args.verbose:
                print(f"published device={device_id} sequence={sequence}")

            time.sleep(args.interval_seconds)
        except Exception as exc:  # noqa: BLE001 - CLI simulator should keep running.
            stats.add_error()
            print(f"error device={device_id} sequence={sequence}: {exc}")

            if sock is not None:
                try:
                    sock.close()
                except OSError:
                    pass

            sock = None
            stats.add_reconnect()
            time.sleep(min(max(args.interval_seconds, 1.0), 5.0))

    if sock is not None:
        try:
            sock.close()
        except OSError:
            pass


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Simulate Fluxo MQTT devices.")
    parser.add_argument("--host", default="localhost")
    parser.add_argument("--port", type=int, default=1883)
    parser.add_argument("--tls", action="store_true")
    parser.add_argument("--ca-file")
    parser.add_argument("--tls-server-name")
    parser.add_argument("--insecure-tls", action="store_true")
    parser.add_argument("--tenant-id", required=True)
    parser.add_argument("--workspace-id", required=True)
    parser.add_argument("--device-prefix", default="sim-device")
    parser.add_argument("--devices", type=int, default=10)
    parser.add_argument("--interval-seconds", type=float, default=1.0)
    parser.add_argument("--messages-per-device", type=int, default=10)
    parser.add_argument("--username-template")
    parser.add_argument("--password")
    parser.add_argument("--password-env")
    parser.add_argument(
        "--credentials-file",
        help=(
            "Path to a JSON file with a list of {deviceId, username, password} objects, "
            "one per simulated device (e.g. produced by provision-simulated-devices.py). "
            "Overrides --username-template/--password/--password-env when set."
        ),
    )
    parser.add_argument("--client-id-prefix", default="fluxo-simulator")
    parser.add_argument("--timeout-seconds", type=float, default=10.0)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--verbose", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()

    if args.devices < 1:
        raise SystemExit("--devices must be >= 1")

    if args.messages_per_device < 1:
        raise SystemExit("--messages-per-device must be >= 1")

    first_device = device_id_for(args, 1)
    first_topic = topic_for(args, first_device)
    first_payload = payload_for(args, first_device, 1)

    if args.dry_run:
        print(first_topic)
        print(json.dumps(first_payload, indent=2))
        return 0

    credentials: dict[str, tuple[str, str]] | None = None
    if args.credentials_file:
        with open(args.credentials_file, "r", encoding="utf-8") as handle:
            entries = json.load(handle)
        credentials = {entry["deviceId"]: (entry["username"], entry["password"]) for entry in entries}

    stats = SafeStats()
    started_at = time.monotonic()
    print(
        "starting simulator "
        f"devices={args.devices} messages_per_device={args.messages_per_device} "
        f"host={args.host} port={args.port} tls={args.tls} "
        f"credentials_file={args.credentials_file or '(template)'}"
    )

    with concurrent.futures.ThreadPoolExecutor(max_workers=min(args.devices, 100)) as executor:
        futures = [
            executor.submit(simulate_device, index, args, stats, credentials)
            for index in range(1, args.devices + 1)
        ]
        for future in concurrent.futures.as_completed(futures):
            future.result()

    elapsed = time.monotonic() - started_at
    counters = stats.snapshot()
    print(
        "summary "
        f"published={counters.published} errors={counters.errors} "
        f"reconnects={counters.reconnects} elapsed_seconds={elapsed:.2f}"
    )
    return 0 if counters.errors == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
