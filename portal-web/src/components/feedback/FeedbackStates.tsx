import type { ReactNode } from "react";

interface FeedbackStateProps {
  title: string;
  description?: string;
  action?: ReactNode;
  compact?: boolean;
}

function FeedbackState({
  kind,
  title,
  description,
  action,
  compact
}: FeedbackStateProps & { kind: "loading" | "empty" | "error" }) {
  const role = kind === "error" ? "alert" : kind === "loading" ? "status" : undefined;

  return (
    <div className={`feedback-state feedback-state-${kind}${compact ? " is-compact" : ""}`} role={role}>
      <span className="feedback-state-indicator" aria-hidden="true" />
      <div className="feedback-state-copy">
        <strong>{title}</strong>
        {description ? <p>{description}</p> : null}
      </div>
      {action ? <div className="feedback-state-action">{action}</div> : null}
    </div>
  );
}

export function LoadingState(props: FeedbackStateProps) {
  return <FeedbackState {...props} kind="loading" />;
}

export function EmptyState(props: FeedbackStateProps) {
  return <FeedbackState {...props} kind="empty" />;
}

export function ErrorState(props: FeedbackStateProps) {
  return <FeedbackState {...props} kind="error" />;
}
