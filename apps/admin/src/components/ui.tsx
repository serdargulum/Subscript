import Link from "next/link";
import type { ReactNode } from "react";

export function PageHeader({
  title,
  description,
  actions
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
}) {
  return (
    <header className="page-header">
      <div>
        <h1>{title}</h1>
        {description ? <p>{description}</p> : null}
      </div>
      {actions ? <div className="page-actions">{actions}</div> : null}
    </header>
  );
}

export function Card({ children, className = "" }: { children: ReactNode; className?: string }) {
  return <section className={`card ${className}`.trim()}>{children}</section>;
}

export function StatCard({
  label,
  value,
  hint
}: {
  label: string;
  value: string | number;
  hint?: string;
}) {
  return (
    <div className="stat-card">
      <span className="stat-label">{label}</span>
      <strong className="stat-value">{value}</strong>
      {hint ? <span className="stat-hint">{hint}</span> : null}
    </div>
  );
}

const badgeTone: Record<string, string> = {
  Active: "success",
  Trialing: "info",
  Open: "warning",
  Paid: "success",
  Succeeded: "success",
  Cancelled: "muted",
  Expired: "danger",
  Failed: "danger"
};

export function StatusBadge({ status }: { status: string }) {
  return <span className={`badge badge-${badgeTone[status] ?? "muted"}`}>{status}</span>;
}

export function EmptyState({ children }: { children: ReactNode }) {
  return <div className="empty-state">{children}</div>;
}

export function ErrorBanner({ message }: { message: string | null }) {
  if (!message) {
    return null;
  }

  return <div className="alert alert-error">{message}</div>;
}

export function SuccessBanner({ message }: { message: string | null }) {
  if (!message) {
    return null;
  }

  return <div className="alert alert-success">{message}</div>;
}

export function Pager({
  page,
  totalPages,
  onPageChange
}: {
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}) {
  if (totalPages <= 1) {
    return null;
  }

  return (
    <div className="pager">
      <button
        type="button"
        className="button button-ghost"
        onClick={() => onPageChange(page - 1)}
        disabled={page <= 1}
      >
        Previous
      </button>
      <span>
        Page {page} of {totalPages}
      </span>
      <button
        type="button"
        className="button button-ghost"
        onClick={() => onPageChange(page + 1)}
        disabled={page >= totalPages}
      >
        Next
      </button>
    </div>
  );
}

export function CustomerLink({ id, name }: { id: string; name: string }) {
  return (
    <Link className="table-link" href={`/customers/${id}`}>
      {name}
    </Link>
  );
}
