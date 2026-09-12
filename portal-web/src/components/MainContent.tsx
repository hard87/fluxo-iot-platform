import type { ReactNode } from "react";

export function MainContent({ children }: { children: ReactNode }) {
  return (
    <main id="main-content" className="main-content" tabIndex={-1}>
      <div className="main-content-container">{children}</div>
    </main>
  );
}
