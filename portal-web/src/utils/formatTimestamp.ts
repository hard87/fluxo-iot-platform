const absoluteFormatter = new Intl.DateTimeFormat("pt-BR", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  second: "2-digit",
  hour12: false
});

/** Formats as "dd/mm/aaaa hh:mm:ss" — pt-BR's default Intl output inserts a
 * comma between date and time, which reads worse than a plain space. */
export function formatHumanTimestamp(timestamp: string): string {
  return absoluteFormatter.format(new Date(timestamp)).replace(",", "");
}
