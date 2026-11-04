"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import {
  Card,
  CustomerLink,
  EmptyState,
  ErrorBanner,
  PageHeader,
  StatCard,
  StatusBadge,
  SuccessBanner
} from "@/components/ui";
import { api, apiPost } from "@/lib/api";
import { formatDate, formatMoney } from "@/lib/format";
import type { Customer, Invoice, Paged, Plan, Subscription } from "@/lib/types";

type DashboardData = {
  customers: number;
  plans: number;
  activeSubscriptions: number;
  openInvoices: number;
  recentSubscriptions: Subscription[];
  openInvoiceItems: Invoice[];
  customerNames: Map<string, string>;
  planNames: Map<string, string>;
};

export default function DashboardPage() {
  const [data, setData] = useState<DashboardData | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [billing, setBilling] = useState(false);

  const load = useCallback(async () => {
    setError(null);

    try {
      const [customers, plans, active, open, recent, customerList, planList] = await Promise.all([
        api<Paged<Customer>>("/customers?page=1&pageSize=1"),
        api<Paged<Plan>>("/plans?page=1&pageSize=1"),
        api<Paged<Subscription>>("/subscriptions?page=1&pageSize=1&status=Active"),
        api<Paged<Invoice>>("/invoices?page=1&pageSize=5&status=Open"),
        api<Paged<Subscription>>("/subscriptions?page=1&pageSize=6"),
        api<Paged<Customer>>("/customers?page=1&pageSize=200"),
        api<Paged<Plan>>("/plans?page=1&pageSize=200")
      ]);

      setData({
        customers: customers.totalCount,
        plans: plans.totalCount,
        activeSubscriptions: active.totalCount,
        openInvoices: open.totalCount,
        recentSubscriptions: recent.items,
        openInvoiceItems: open.items,
        customerNames: new Map(customerList.items.map((item) => [item.id, item.name])),
        planNames: new Map(planList.items.map((item) => [item.id, item.name]))
      });
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "Could not load the dashboard.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function runBilling() {
    setBilling(true);
    setError(null);
    setMessage(null);

    try {
      const result = await apiPost<{ processed: number }>("/billing/run");
      setMessage(`Billing run finished: ${result.processed} subscription(s) processed.`);
      await load();
    } catch (runError) {
      setError(runError instanceof Error ? runError.message : "Billing run failed.");
    } finally {
      setBilling(false);
    }
  }

  return (
    <>
      <PageHeader
        title="Dashboard"
        description="Subscription health at a glance."
        actions={
          <button className="button button-secondary" type="button" onClick={runBilling} disabled={billing}>
            {billing ? "Running billing…" : "Run billing now"}
          </button>
        }
      />

      <ErrorBanner message={error} />
      <SuccessBanner message={message} />

      {loading || !data ? (
        <Card>
          <div className="loading">Loading dashboard…</div>
        </Card>
      ) : (
        <>
          <div className="stats">
            <StatCard label="Customers" value={data.customers} hint="Active and inactive" />
            <StatCard label="Plans" value={data.plans} hint="Plan catalogue" />
            <StatCard label="Active subscriptions" value={data.activeSubscriptions} hint="Currently billing" />
            <StatCard label="Open invoices" value={data.openInvoices} hint="Awaiting payment" />
          </div>

          <Card>
            <div className="card-header">
              <div>
                <h2>Recent subscriptions</h2>
                <p>Latest subscriptions across all customers.</p>
              </div>
              <Link className="button button-ghost" href="/subscriptions">
                View all
              </Link>
            </div>

            {data.recentSubscriptions.length === 0 ? (
              <EmptyState>No subscriptions yet. Create a customer and a plan to get started.</EmptyState>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Customer</th>
                      <th>Plan</th>
                      <th>Status</th>
                      <th>Next billing</th>
                      <th>Period</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.recentSubscriptions.map((subscription) => (
                      <tr key={subscription.id}>
                        <td>
                          <CustomerLink
                            id={subscription.customerId}
                            name={data.customerNames.get(subscription.customerId) ?? "Unknown customer"}
                          />
                        </td>
                        <td>{data.planNames.get(subscription.planId) ?? "Unknown plan"}</td>
                        <td>
                          <StatusBadge status={subscription.status} />
                        </td>
                        <td>{formatDate(subscription.nextBillingDate)}</td>
                        <td className="table-muted">
                          {formatDate(subscription.currentPeriodStart)} – {formatDate(subscription.currentPeriodEnd)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>

          <Card>
            <div className="card-header">
              <div>
                <h2>Open invoices</h2>
                <p>Invoices waiting for a successful payment.</p>
              </div>
              <Link className="button button-ghost" href="/invoices?status=Open">
                View all
              </Link>
            </div>

            {data.openInvoiceItems.length === 0 ? (
              <EmptyState>No open invoices. Everything is settled.</EmptyState>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Invoice</th>
                      <th>Plan</th>
                      <th>Amount</th>
                      <th>Due date</th>
                      <th>Status</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {data.openInvoiceItems.map((invoice) => (
                      <tr key={invoice.id}>
                        <td>
                          <Link className="table-link" href={`/invoices/${invoice.id}`}>
                            {invoice.invoiceNumber ?? invoice.id.slice(0, 8)}
                          </Link>
                        </td>
                        <td>{invoice.planNameSnapshot}</td>
                        <td>{formatMoney(invoice.amount, invoice.currency)}</td>
                        <td>{formatDate(invoice.dueDate)}</td>
                        <td>
                          <StatusBadge status={invoice.status} />
                        </td>
                        <td>
                          <Link className="button button-ghost button-small" href={`/invoices/${invoice.id}`}>
                            View
                          </Link>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        </>
      )}
    </>
  );
}
