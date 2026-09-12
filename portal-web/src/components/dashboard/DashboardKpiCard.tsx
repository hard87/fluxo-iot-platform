type KpiTone = "neutral" | "success" | "warning" | "danger" | "info";

interface DashboardKpiCardProps {
  label: string;
  value: number;
  hint: string;
  tone?: KpiTone;
}

const numberFormatter = new Intl.NumberFormat("pt-BR");

export function DashboardKpiCard({ label, value, hint, tone = "neutral" }: DashboardKpiCardProps) {
  const formattedValue = numberFormatter.format(value);

  return (
    <article className="panel dashboard-kpi-card" data-tone={tone} aria-label={`${label}: ${formattedValue}`}>
      <div className="dashboard-kpi-heading">
        <p>{label}</p>
        <span className="dashboard-kpi-indicator" aria-hidden="true" />
      </div>
      <strong>{formattedValue}</strong>
      <small>{hint}</small>
    </article>
  );
}
