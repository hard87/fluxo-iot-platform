import { formatHumanTimestamp } from "../../utils/formatTimestamp";

interface FormattedTimestamp {
  relative: string;
  absolute: string;
}

function capitalize(value: string) {
  return value.charAt(0).toLocaleUpperCase("pt-BR") + value.slice(1);
}

export function formatDashboardTimestamp(timestamp: string, now = Date.now()): FormattedTimestamp | null {
  const date = new Date(timestamp);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  const differenceInSeconds = (date.getTime() - now) / 1000;
  const absoluteDifference = Math.abs(differenceInSeconds);
  const relativeFormatter = new Intl.RelativeTimeFormat("pt-BR", { numeric: "always" });

  let unit: Intl.RelativeTimeFormatUnit = "second";
  let divisor = 1;

  if (absoluteDifference >= 86400) {
    unit = "day";
    divisor = 86400;
  } else if (absoluteDifference >= 3600) {
    unit = "hour";
    divisor = 3600;
  } else if (absoluteDifference >= 60) {
    unit = "minute";
    divisor = 60;
  }

  const relative = capitalize(relativeFormatter.format(Math.round(differenceInSeconds / divisor), unit));
  const absolute = formatHumanTimestamp(timestamp);

  return { relative, absolute };
}

export function TelemetryActivity({ timestamp }: { timestamp?: string | null }) {
  const formatted = timestamp ? formatDashboardTimestamp(timestamp) : null;

  return (
    <article className="panel dashboard-activity-card">
      <p className="dashboard-card-eyebrow">Atividade</p>
      <h2>Última telemetria</h2>
      {timestamp && formatted ? (
        <>
          <strong className="dashboard-activity-relative">{formatted.relative}</strong>
          <time dateTime={timestamp} title={timestamp}>
            {formatted.absolute}
          </time>
          <small>Horário local</small>
        </>
      ) : (
        <>
          <strong className="dashboard-activity-relative">Sem telemetria recebida</strong>
          <small>Ainda não há timestamp disponível.</small>
        </>
      )}
    </article>
  );
}
