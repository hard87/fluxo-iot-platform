interface Props {
  status: "Unknown" | "Online" | "Offline";
}

export function DeviceStatusBadge({ status }: Props) {
  const className =
    status === "Online"
      ? "status-badge online"
      : status === "Offline"
        ? "status-badge offline"
        : "status-badge unknown";

  return <span className={className}>{status.toLowerCase()}</span>;
}
