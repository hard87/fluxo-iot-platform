interface Props {
  status: "Unknown" | "Online" | "Offline" | 1 | 2 | 3;
}

export function DeviceStatusBadge({ status }: Props) {
  const normalizedStatus = typeof status === "number"
    ? ({ 1: "Unknown", 2: "Online", 3: "Offline" } as const)[status]
    : status;
  const className =
    normalizedStatus === "Online"
      ? "status-badge online"
      : normalizedStatus === "Offline"
        ? "status-badge offline"
        : "status-badge unknown";

  return <span className={className}>{normalizedStatus.toLowerCase()}</span>;
}
