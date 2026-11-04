"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import {
  Card,
  CustomerLink,
  EmptyState,
  ErrorBanner,
  PageHeader,
  Pager,
  StatusBadge,
  SuccessBanner
} from "@/components/ui";
import { api, queryString } from "@/lib/api";
import { formatDate } from "@/lib/format";
import type { Customer, Paged, Plan, Subscription, SubscriptionStatus } from "@/lib/types";

const pageSize = 20;

const statuses: Array<SubscriptionStatus | ""> = ["", "Trialing", "Active", "Cancelled", "Expired"];

export default function SubscriptionsPage() {
  const [data, setData] = useState<Paged<Subscription> | null>(null);
  const [customers, setCustomers] = useState<Map<string, Customer>>(new Map());
  const [plans, setPlans] = useState<Map<string, Plan>>(new Map());
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<SubscriptionStatus | "">("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const [subscriptions, customerList, planList] = await Promise.all([
        api<Paged<Subscription>>(
          `/subscriptions${queryString({ page, pageSize, status })}`
        ),
        api<Paged<Customer>>("/customers?page=1&pageSize=200"),
        api<Paged<Plan>>("/plans?page=1&pageSize=200")
      ]);

      setData(subscriptions);
      setCustomers(new Map(customerList.items.map((item) => [item.id, item])));
      setPlans(new Map(planList.items.map((item) => [item.id, item])));
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "Could not load subscriptions.");
    } finally {
      setLoading(false);
    }
  }, [page, status]);

  useEffect(() => {
    void load();
  }, [load]);

  async function cancel(subscription: Subscription) {
    if (!window.confirm("Cancel this subscription immediately?")) {
      return;
    }

    setError(null);
    setMessage(null);

    try {
      await api(`/subscriptions/${subscription.id}/cancel`, { method: "POST" });
      setMessage("Subscription cancelled.");
      await load();
    } catch (cancelError) {
      setError(cancelError instanceof Error ? cancelError.message : "Could not cancel the subscription.");
    }
  }

  return (
    <>
      <PageHeader
        title="Subscriptions"
        description="One active or trialing subscription per customer."
        actions={
          <div className="field">
            <label htmlFor="subscription-status">Status</label>
            <select
              id="subscription-status"
              value={status}
              onChange={(event) => {
                setPage(1);
                setStatus(event.target.value as SubscriptionStatus | "");
              }}
            >
              {statuses.map((value) => (
                <option key={value || "all"} value={value}>
                  {value || "All statuses"}
                </option>
              ))}
            </select>
          </div>
        }
      />

      <ErrorBanner message={error} />
      <SuccessBanner message={message} />

      <Card>
        {loading ? (
          <div className="loading">Loading subscriptions…</div>
        ) : !data || data.items.length === 0 ? (
          <EmptyState>No subscriptions match this filter.</EmptyState>
        ) : (
          <>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Customer</th>
                    <th>Plan</th>
                    <th>Status</th>
                    <th>Start</th>
                    <th>Trial ends</th>
                    <th>Next billing</th>
                    <th>Pending plan</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((subscription) => {
                    const customer = customers.get(subscription.customerId);
                    const plan = plans.get(subscription.planId);
                    const terminal =
                      subscription.status === "Cancelled" || subscription.status === "Expired";

                    return (
                      <tr key={subscription.id}>
                        <td>
                          <CustomerLink
                            id={subscription.customerId}
                            name={customer?.name ?? subscription.customerId.slice(0, 8)}
                          />
                        </td>
                        <td>{plan?.name ?? subscription.planId.slice(0, 8)}</td>
                        <td>
                          <StatusBadge status={subscription.status} />
                        </td>
                        <td>{formatDate(subscription.startDate)}</td>
                        <td>{formatDate(subscription.trialEndDate)}</td>
                        <td>{formatDate(subscription.nextBillingDate)}</td>
                        <td className="table-muted">
                          {subscription.pendingPlanId
                            ? plans.get(subscription.pendingPlanId)?.name ?? "Unknown plan"
                            : "—"}
                        </td>
                        <td>
                          <div className="table-actions">
                            <Link
                              className="button button-ghost button-small"
                              href={`/customers/${subscription.customerId}`}
                            >
                              Open
                            </Link>
                            {!terminal && !subscription.isDeleted ? (
                              <button
                                className="button button-danger button-small"
                                type="button"
                                onClick={() => cancel(subscription)}
                              >
                                Cancel
                              </button>
                            ) : null}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            <Pager page={data.page} totalPages={data.totalPages} onPageChange={setPage} />
          </>
        )}
      </Card>
    </>
  );
}
