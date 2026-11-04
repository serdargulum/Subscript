import { redirect } from "next/navigation";
import { getSession } from "@/lib/session";
import { LoginForm } from "./LoginForm";

export default async function LoginPage() {
  const session = await getSession();

  if (session.isAuthenticated) {
    redirect("/");
  }

  return (
    <div className="login-shell">
      <div className="login-card">
        <div className="login-brand">
          <span className="brand-mark">S</span>
          Subscript
        </div>
        <p>Sign in to manage customers, plans, subscriptions and invoices.</p>
        <LoginForm />
        <div className="login-hint">Credentials come from ADMIN_USERNAME and ADMIN_PASSWORD.</div>
      </div>
    </div>
  );
}
