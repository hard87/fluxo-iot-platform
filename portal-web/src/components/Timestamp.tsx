import { formatHumanTimestamp } from "../utils/formatTimestamp";

interface Props {
  value?: string | null;
  emptyLabel?: string;
}

export function Timestamp({ value, emptyLabel = "—" }: Props) {
  if (!value) {
    return <span className="muted">{emptyLabel}</span>;
  }

  return (
    <time dateTime={value} title={value}>
      {formatHumanTimestamp(value)}
    </time>
  );
}
