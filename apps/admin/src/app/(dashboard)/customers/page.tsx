"use client";

import Link from "next/link";
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
import { api, apiPost, queryString } from "@/lib/api";
import { formatDate } from "@/lib/format";
import type { Customer, Paged } from "@/lib/types";

const pageSize = 10;

export default function CustomersPage() {
  const [data, setData] = useState<Paged<Customer> | null>(null);
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [companyName, setCompanyName] = useState("");
  const [creating, setCreating] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const result = await api<Paged<Customer>>(
        `/customers${queryString({ page, pageSize, search })}`
      );
      setData(result);
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "Could not load customers.");
    } finally {
      setLoading(false);
    }
  }, [page, search]);

  useEffect(() => {
    void load();
  }, [load]);

  async function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPage(1);
    setSearch(searchInput.trim());
  }

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setCreating(true);
    setError(null);
    setMessage(null);

    try {
      await apiPost<Customer>("/customers", {
        name,
        email,
        companyName: companyName.trim() || null
      });

      setName("");
      setEmail("");
      setCompanyName("");
      setMessage("Customer created.");
      setPage(1);
      await load();
    } catch (createError) {
      setError(createError instanceof Error ? createError.message : "Could not create the customer.");
    } finally {
      setCreating(false);
    }
  }

  async function deactivate(customer: Customer) {
    if (!window.confirm(`Deactivate ${customer.name}? Existing data is retained.`)) {
      return;
    }

    setError(null);
    setMessage(null);

    try {
      await api(`/customers/${customer.id}/deactivate`, { method: "POST" });
      setMessage(`${customer.name} was deactivated.`);
      await load();
    } catch (deactivateError) {
      setError(
        deactivateError instanceof Error ? deactivateError.message : "Could not deactivate the customer."
      );
    }
  }

  return (
    <>
      <PageHeader
        title="Customers"
        description="Create customers and manage their active status."
        actions={
          <form className="toolbar" onSubmit={handleSearch}>
            <div className="field">
              <label htmlFor="customer-search">Search</label>
              <input
                id="customer-search"
                className="search-input"
                placeholder="Name, email or company"
                value={searchInput}
                onChange={(event) => setSearchInput(event.target.value)}
              />
            </div>
            <button className="button button-ghost" type="submit">
              Search
            </button>
          </form>
        }
      />

      <ErrorBanner message={error} />
      <SuccessBanner message={message} />

      <Card>
        <div className="card-header">
          <div>
            <h2>New customer</h2>
            <p>Emails are unique and stored in lower case.</p>
          </div>
        </div>

        <form className="form-grid" onSubmit={handleCreate}>
          <div className="field">
            <label htmlFor="customer-name">Name</label>
            <input
              id="customer-name"
              value={name}
              onChange={(event) => setName(event.target.value)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="customer-email">Email</label>
            <input
              id="customer-email"
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="customer-company">Company (optional)</label>
            <input
              id="customer-company"
              value={companyName}
              onChange={(event) => setCompanyName(event.target.value)}
            />
          </div>
          <div className="field-actions">
            <button className="button" type="submit" disabled={creating}>
              {creating ? "Creating…" : "Create customer"}
            </button>
          </div>
        </form>
      </Card>

      <Card>
        {loading ? (
          <div className="loading">Loading customers…</div>
        ) : !data || data.items.length === 0 ? (
          <EmptyState>
            No customers found. {search ? "Try a different search." : "Create the first customer above."}
          </EmptyState>
        ) : (
          <>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Customer</th>
                    <th>Email</th>
                    <th>Status</th>
                    <th>Created</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((customer) => (
                    <tr key={customer.id}>
                      <td>
                        <div className="cell-stack">
                          <Link className="table-link" href={`/customers/${customer.id}`}>
                            {customer.name}
                          </Link>
                          {customer.companyName ? <small>{customer.companyName}</small> : null}
                        </div>
                      </td>
                      <td>{customer.email}</td>
                      <td>
                        <StatusBadge status={customer.isDeleted ? "Inactive" : "Active"} />
                      </td>
                      <td>{formatDate(customer.createdAt)}</td>
                      <td>
                        <div className="table-actions">
                          <Link className="button button-ghost button-small" href={`/customers/${customer.id}`}>
                            Open
                          </Link>
                          {!customer.isDeleted ? (
                            <button
                              className="button button-danger button-small"
                              type="button"
                              onClick={() => deactivate(customer)}
                            >
                              Deactivate
                            </button>
                          ) : null}
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
