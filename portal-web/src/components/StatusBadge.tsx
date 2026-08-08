export type StatusTone = "success" | "warning" | "danger" | "info" | "neutral";

interface Props {
  label: string;
  tone: StatusTone;
  detail?: string;
}

export function StatusBadge({ label, tone, detail }: Props) {
  return (
    <span className={`status-badge ${tone}`} title={detail}>
      {label}
    </span>
  );
}
