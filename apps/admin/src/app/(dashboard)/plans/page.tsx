"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import {
  Card,
  EmptyState,
  ErrorBanner,
  PageHeader,
  Pager,
  StatusBadge,
  SuccessBanner
} from "@/components/ui";
import { api, apiPost } from "@/lib/api";
import { formatMoney } from "@/lib/format";
import type { BillingPeriod, Paged, Plan } from "@/lib/types";

const pageSize = 20;

type PlanForm = {
  name: string;
  description: string;
  price: string;
  billingPeriod: BillingPeriod;
};

const emptyForm: PlanForm = {
  name: "",
  description: "",
  price: "",
  billingPeriod: "Monthly"
};

export default function PlansPage() {
  const [data, setData] = useState<Paged<Plan> | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const [form, setForm] = useState<PlanForm>(emptyForm);
  const [creating, setCreating] = useState(false);

  const [editing, setEditing] = useState<Plan | null>(null);
  const [editForm, setEditForm] = useState<PlanForm>(emptyForm);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      setData(await api<Paged<Plan>>(`/plans?page=${page}&pageSize=${pageSize}`));
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "Could not load plans.");
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => {
    void load();
  }, [load]);

  async function createPlan(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setCreating(true);
    setError(null);
    setMessage(null);

    try {
      await apiPost<Plan>("/plans", {
        name: form.name,
        description: form.description.trim() || null,
        price: Number(form.price),
        billingPeriod: form.billingPeriod
      });
      setForm(emptyForm);
      setMessage("Plan created.");
      setPage(1);
      await load();
    } catch (createError) {
      setError(createError instanceof Error ? createError.message : "Could not create the plan.");
    } finally {
      setCreating(false);
    }
  }

  function startEdit(plan: Plan) {
    setEditing(plan);
    setEditForm({
      name: plan.name,
      description: plan.description ?? "",
      price: plan.price.toFixed(2),
      billingPeriod: plan.billingPeriod
    });
  }

  async function saveEdit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!editing) {
      return;
    }

    setError(null);
    setMessage(null);

    try {
      await api(`/plans/${editing.id}`, {
        method: "PUT",
        body: JSON.stringify({
          name: editForm.name,
          description: editForm.description.trim() || null,
          price: Number(editForm.price),
          billingPeriod: editForm.billingPeriod
        })
      });
      setEditing(null);
      setMessage("Plan updated. Existing invoices keep their price snapshot.");
      await load();
    } catch (updateError) {
      setError(updateError instanceof Error ? updateError.message : "Could not update the plan.");
    }
  }

  async function deactivate(plan: Plan) {
    if (!window.confirm(`Deactivate ${plan.name}? Existing subscriptions keep billing.`)) {
      return;
    }

    setError(null);
    setMessage(null);

    try {
      await api(`/plans/${plan.id}`, { method: "DELETE" });
      setMessage(`${plan.name} was deactivated.`);
      await load();
    } catch (deactivateError) {
      setError(deactivateError instanceof Error ? deactivateError.message : "Could not deactivate the plan.");
    }
  }

  return (
    <>
      <PageHeader
        title="Plans"
        description="The plan catalogue. Prices apply to future invoices only."
      />

      <ErrorBanner message={error} />
      <SuccessBanner message={message} />

      <Card>
        <div className="card-header">
          <div>
            <h2>{editing ? `Edit ${editing.name}` : "New plan"}</h2>
            <p>{editing ? "Update the plan details." : "Add a monthly or yearly plan."}</p>
          </div>
          {editing ? (
            <button className="button button-ghost button-small" type="button" onClick={() => setEditing(null)}>
              Cancel edit
            </button>
          ) : null}
        </div>

        <form className="form-grid" onSubmit={editing ? saveEdit : createPlan}>
          <div className="field">
            <label htmlFor="plan-name">Name</label>
            <input
              id="plan-name"
              value={editing ? editForm.name : form.name}
              onChange={(event) =>
                editing
                  ? setEditForm({ ...editForm, name: event.target.value })
                  : setForm({ ...form, name: event.target.value })
              }
              required
            />
          </div>
          <div className="field">
            <label htmlFor="plan-price">Price (USD)</label>
            <input
              id="plan-price"
              type="number"
              min="0"
              step="0.01"
              value={editing ? editForm.price : form.price}
              onChange={(event) =>
                editing
                  ? setEditForm({ ...editForm, price: event.target.value })
                  : setForm({ ...form, price: event.target.value })
              }
              required
            />
          </div>
          <div className="field">
            <label htmlFor="plan-period">Billing period</label>
            <select
              id="plan-period"
              value={editing ? editForm.billingPeriod : form.billingPeriod}
              onChange={(event) =>
                editing
                  ? setEditForm({
                      ...editForm,
                      billingPeriod: event.target.value as BillingPeriod
                    })
                  : setForm({ ...form, billingPeriod: event.target.value as BillingPeriod })
              }
            >
              <option value="Monthly">Monthly</option>
              <option value="Yearly">Yearly</option>
            </select>
          </div>
          <div className="field">
            <label htmlFor="plan-description">Description (optional)</label>
            <input
              id="plan-description"
              value={editing ? editForm.description : form.description}
              onChange={(event) =>
                editing
                  ? setEditForm({ ...editForm, description: event.target.value })
                  : setForm({ ...form, description: event.target.value })
              }
            />
          </div>
          <div className="field-actions">
            <button className="button" type="submit" disabled={creating}>
              {editing ? "Save changes" : creating ? "Creating…" : "Create plan"}
            </button>
          </div>
        </form>
      </Card>

      <Card>
        {loading ? (
          <div className="loading">Loading plans…</div>
        ) : !data || data.items.length === 0 ? (
          <EmptyState>No plans yet. Create the first plan above.</EmptyState>
        ) : (
          <>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Plan</th>
                    <th>Price</th>
                    <th>Period</th>
                    <th>Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((plan) => (
                    <tr key={plan.id} className={editing?.id === plan.id ? "row-selected" : undefined}>
                      <td>
                        <div className="cell-stack">
                          <strong>{plan.name}</strong>
                          {plan.description ? <small>{plan.description}</small> : null}
                        </div>
                      </td>
                      <td>{formatMoney(plan.price, plan.currency)}</td>
                      <td>{plan.billingPeriod}</td>
                      <td>
                        <StatusBadge status={plan.isDeleted ? "Inactive" : "Active"} />
                      </td>
                      <td>
                        <div className="table-actions">
                          {!plan.isDeleted ? (
                            <>
                              <button
                                className="button button-ghost button-small"
                                type="button"
                                onClick={() => startEdit(plan)}
                              >
                                Edit
                              </button>
                              <button
                                className="button button-danger button-small"
                                type="button"
                                onClick={() => deactivate(plan)}
                              >
                                Deactivate
                              </button>
                            </>
                          ) : (
                            <span className="table-muted">Deactivated</span>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
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
