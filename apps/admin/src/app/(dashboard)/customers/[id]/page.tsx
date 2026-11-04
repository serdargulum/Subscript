"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { FormEvent, useCallback, useEffect, useState } from "react";
import {
  Card,
  EmptyState,
  ErrorBanner,
  PageHeader,
  StatusBadge,
  SuccessBanner
} from "@/components/ui";
import { api, apiPost, queryString } from "@/lib/api";
import { formatDate, formatDateTime } from "@/lib/format";
import type { Customer, Paged, Plan, Subscription } from "@/lib/types";

export default function CustomerDetailPage() {
  const params = useParams<{ id: string }>();
  const customerId = params.id;

  const [customer, setCustomer] = useState<Customer | null>(null);
  const [subscriptions, setSubscriptions] = useState<Subscription[]>([]);
  const [plans, setPlans] = useState<Plan[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const [planId, setPlanId] = useState("");
  const [startDate, setStartDate] = useState("");
  const [creating, setCreating] = useState(false);

  const [changingId, setChangingId] = useState<string | null>(null);
  const [newPlanId, setNewPlanId] = useState("");

  const [editing, setEditing] = useState(false);
  const [editName, setEditName] = useState("");
  const [editEmail, setEditEmail] = useState("");
  const [editCompany, setEditCompany] = useState("");
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const [customerResult, subscriptionResult, planResult] = await Promise.all([
        api<Customer>(`/customers/${customerId}`),
        api<Paged<Subscription>>(
          `/customers/${customerId}/subscriptions${queryString({ pageSize: 50 })}`
        ),
        api<Paged<Plan>>("/plans?page=1&pageSize=200")
      ]);

      setCustomer(customerResult);
      setSubscriptions(subscriptionResult.items);
      setPlans(planResult.items);
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "Could not load the customer.");
    } finally {
      setLoading(false);
    }
  }, [customerId]);

  useEffect(() => {
    void load();
  }, [load]);

  const planNames = new Map(plans.map((plan) => [plan.id, plan.name]));

  async function createSubscription(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!planId) {
      setError("Choose a plan first.");
      return;
    }

    setCreating(true);
    setError(null);
    setMessage(null);

    try {
      await apiPost<Subscription>("/subscriptions", {
        customerId,
        planId,
        startDate: startDate || null
      });
      setMessage("Subscription created. Trial runs for 30 days.");
      setPlanId("");
      setStartDate("");
      await load();
    } catch (createError) {
      setError(
        createError instanceof Error ? createError.message : "Could not create the subscription."
      );
    } finally {
      setCreating(false);
    }
  }

  async function cancelSubscription(subscription: Subscription) {
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

  function startEdit() {
    if (!customer) {
      return;
    }

    setEditName(customer.name);
    setEditEmail(customer.email);
    setEditCompany(customer.companyName ?? "");
    setEditing(true);
  }

  async function saveCustomer(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    setError(null);
    setMessage(null);

    try {
      await api(`/customers/${customerId}`, {
        method: "PUT",
        body: JSON.stringify({
          name: editName,
          email: editEmail,
          companyName: editCompany.trim() || null
        })
      });
      setMessage("Customer details updated.");
      setEditing(false);
      await load();
    } catch (updateError) {
      setError(
        updateError instanceof Error ? updateError.message : "Could not update the customer."
      );
    } finally {
      setSaving(false);
    }
  }

  async function changePlan(subscription: Subscription) {
    if (!newPlanId) {
      setError("Choose the plan to switch to.");
      return;
    }

    setError(null);
    setMessage(null);

    try {
      await api(`/subscriptions/${subscription.id}/change-plan`, {
        method: "POST",
        body: JSON.stringify({ planId: newPlanId })
      });
      setMessage("Plan change scheduled for the next billing date.");
      setChangingId(null);
      setNewPlanId("");
      await load();
    } catch (changeError) {
      setError(changeError instanceof Error ? changeError.message : "Could not change the plan.");
    }
  }

  if (loading) {
    return (
      <Card>
        <div className="loading">Loading customer…</div>
      </Card>
    );
  }

  if (!customer) {
    return (
      <>
        <ErrorBanner message={error ?? "Customer not found."} />
        <Link className="button button-ghost" href="/customers">
          Back to customers
        </Link>
      </>
    );
  }

  const activePlans = plans.filter((plan) => !plan.isDeleted);

  return (
    <>
      <div className="breadcrumb">
        <Link href="/customers">Customers</Link> / {customer.name}
      </div>

      <PageHeader
        title={customer.name}
        description={customer.companyName ?? "Customer details and subscriptions."}
        actions={
          <div className="page-actions">
            {!customer.isDeleted ? (
              <button className="button" type="button" onClick={startEdit}>
                Edit details
              </button>
            ) : null}
            <Link className="button button-ghost" href="/customers">
              All customers
            </Link>
          </div>
        }
      />

      <ErrorBanner message={error} />
      <SuccessBanner message={message} />

      <Card>
        <div className="detail-grid">
          <div className="detail-item">
            <span>Email</span>
            <strong>{customer.email}</strong>
          </div>
          <div className="detail-item">
            <span>Company</span>
            <strong>{customer.companyName ?? "—"}</strong>
          </div>
          <div className="detail-item">
            <span>Status</span>
            <strong>
              <StatusBadge status={customer.isDeleted ? "Inactive" : "Active"} />
            </strong>
          </div>
          <div className="detail-item">
            <span>Created</span>
            <strong>{formatDateTime(customer.createdAt)}</strong>
          </div>
        </div>
      </Card>

      {editing ? (
        <Card>
          <div className="card-header">
            <div>
              <h2>Edit customer</h2>
              <p>Emails stay unique across active and inactive customers.</p>
            </div>
            <button
              className="button button-ghost button-small"
              type="button"
              onClick={() => setEditing(false)}
            >
              Cancel
            </button>
          </div>

          <form className="form-grid" onSubmit={saveCustomer}>
            <div className="field">
              <label htmlFor="edit-customer-name">Name</label>
              <input
                id="edit-customer-name"
                value={editName}
                onChange={(event) => setEditName(event.target.value)}
                required
              />
            </div>
            <div className="field">
              <label htmlFor="edit-customer-email">Email</label>
              <input
                id="edit-customer-email"
                type="email"
                value={editEmail}
                onChange={(event) => setEditEmail(event.target.value)}
                required
              />
            </div>
            <div className="field">
              <label htmlFor="edit-customer-company">Company (optional)</label>
              <input
                id="edit-customer-company"
                value={editCompany}
                onChange={(event) => setEditCompany(event.target.value)}
              />
            </div>
            <div className="field-actions">
              <button className="button" type="submit" disabled={saving}>
                {saving ? "Saving…" : "Save changes"}
              </button>
            </div>
          </form>
        </Card>
      ) : null}

      <h2 className="section-title">Subscriptions</h2>

      <Card>
        {customer.isDeleted ? (
          <EmptyState>This customer is inactive. Reactivate is not supported in v1.</EmptyState>
        ) : (
          <form className="inline-form" onSubmit={createSubscription}>
            <div className="field">
              <label htmlFor="new-subscription-plan">Plan</label>
              <select
                id="new-subscription-plan"
                value={planId}
                onChange={(event) => setPlanId(event.target.value)}
                required
              >
                <option value="">Select a plan</option>
                {activePlans.map((plan) => (
                  <option key={plan.id} value={plan.id}>
                    {plan.name} — {plan.price.toFixed(2)} {plan.currency} / {plan.billingPeriod.toLowerCase()}
                  </option>
                ))}
              </select>
            </div>
            <div className="field">
              <label htmlFor="new-subscription-start">Start date (optional)</label>
              <input
                id="new-subscription-start"
                type="date"
                value={startDate}
                onChange={(event) => setStartDate(event.target.value)}
              />
            </div>
            <button className="button" type="submit" disabled={creating || activePlans.length === 0}>
              {creating ? "Creating…" : "Start subscription"}
            </button>
          </form>
        )}

        <div className="table-wrap" style={{ marginTop: 22 }}>
          {subscriptions.length === 0 ? (
            <EmptyState>No subscriptions for this customer yet.</EmptyState>
          ) : (
            <table>
              <thead>
                <tr>
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
                {subscriptions.map((subscription) => {
                  const terminal =
                    subscription.status === "Cancelled" || subscription.status === "Expired";

                  return (
                    <tr key={subscription.id}>
                      <td>{planNames.get(subscription.planId) ?? "Unknown plan"}</td>
                      <td>
                        <StatusBadge status={subscription.status} />
                      </td>
                      <td>{formatDate(subscription.startDate)}</td>
                      <td>{formatDate(subscription.trialEndDate)}</td>
                      <td>{formatDate(subscription.nextBillingDate)}</td>
                      <td className="table-muted">
                        {subscription.pendingPlanId
                          ? planNames.get(subscription.pendingPlanId) ?? "Unknown plan"
                          : "—"}
                      </td>
                      <td>
                        <div className="table-actions">
                          <Link
                            className="button button-ghost button-small"
                            href={`/invoices?subscriptionId=${subscription.id}`}
                          >
                            Invoices
                          </Link>
                          {!terminal && !subscription.isDeleted ? (
                            <>
                              <button
                                className="button button-ghost button-small"
                                type="button"
                                onClick={() => {
                                  setChangingId(
                                    changingId === subscription.id ? null : subscription.id
                                  );
                                  setNewPlanId("");
                                }}
                              >
                                Change plan
                              </button>
                              <button
                                className="button button-danger button-small"
                                type="button"
                                onClick={() => cancelSubscription(subscription)}
                              >
                                Cancel
                              </button>
                            </>
                          ) : null}
                        </div>

                        {changingId === subscription.id ? (
                          <div className="inline-form" style={{ marginTop: 8 }}>
                            <div className="field">
                              <label htmlFor={`plan-${subscription.id}`}>New plan</label>
                              <select
                                id={`plan-${subscription.id}`}
                                value={newPlanId}
                                onChange={(event) => setNewPlanId(event.target.value)}
                              >
                                <option value="">Select a plan</option>
                                {activePlans
                                  .filter((plan) => plan.id !== subscription.planId)
                                  .map((plan) => (
                                    <option key={plan.id} value={plan.id}>
                                      {plan.name}
                                    </option>
                                  ))}
                              </select>
                            </div>
                            <button
                              className="button button-small"
                              type="button"
                              onClick={() => changePlan(subscription)}
                            >
                              Schedule change
                            </button>
                          </div>
                        ) : null}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      </Card>
    </>
  );
}
