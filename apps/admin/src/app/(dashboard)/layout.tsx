import { redirect } from "next/navigation";
import type { ReactNode } from "react";
import { LogoutButton } from "@/components/LogoutButton";
import { Nav } from "@/components/Nav";
import { getSession } from "@/lib/session";

export default async function DashboardLayout({ children }: { children: ReactNode }) {
  const session = await getSession();

  if (!session.isAuthenticated) {
    redirect("/login");
  }

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-mark">S</span>
          Subscript
        </div>
        <Nav />
        <div className="sidebar-footer">
          <div className="sidebar-user">
            Signed in as
            <strong>{session.username ?? "admin"}</strong>
          </div>
          <LogoutButton />
        </div>
      </aside>
      <main className="main">
        <div className="content">{children}</div>
      </main>
    </div>
  );
}
