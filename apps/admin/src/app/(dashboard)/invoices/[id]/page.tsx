"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { FormEvent, useCallback, useEffect, useState } from "react";
import {
  Card,
  CustomerLink,
  EmptyState,
  ErrorBanner,
  PageHeader,
  StatusBadge,
  SuccessBanner
} from "@/components/ui";
import { api, apiPost } from "@/lib/api";
import { formatDate, formatDateTime, formatMoney } from "@/lib/format";
import type {
  Customer,
  Invoice,
  Payment,
  PaymentMethod,
  PaymentStatus,
  Subscription
} from "@/lib/types";

export default function InvoiceDetailPage() {
  const params = useParams<{ id: string }>();
  const invoiceId = params.id;

  const [invoice, setInvoice] = useState<Invoice | null>(null);
  const [subscription, setSubscription] = useState<Subscription | null>(null);
  const [customer, setCustomer] = useState<Customer | null>(null);
  const [payments, setPayments] = useState<Payment[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const [method, setMethod] = useState<PaymentMethod>("Manual");
  const [status, setStatus] = useState<PaymentStatus>("Succeeded");
  const [failureReason, setFailureReason] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const invoiceResult = await api<Invoice>(`/invoices/${invoiceId}`);
      const paymentResult = await api<Payment[]>(`/invoices/${invoiceResult.id}/payments`);

      setInvoice(invoiceResult);
      setPayments(paymentResult);

      try {
        const subscriptionResult = await api<Subscription>(
          `/subscriptions/${invoiceResult.subscriptionId}`
        );
        setSubscription(subscriptionResult);
        setCustomer(await api<Customer>(`/customers/${subscriptionResult.customerId}`));
      } catch {
        setSubscription(null);
        setCustomer(null);
      }
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "Could not load the invoice.");
    } finally {
      setLoading(false);
    }
  }, [invoiceId]);

  useEffect(() => {
    void load();
  }, [load]);

  async function recordPayment(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!invoice) {
      return;
    }

    setSubmitting(true);
    setError(null);
    setMessage(null);

    try {
      await apiPost<Payment>("/payments", {
        invoiceId: invoice.id,
        method,
        status,
        paidAt: null,
        failureReason: status === "Failed" ? failureReason.trim() || "Payment failed" : null
      });

      setMessage(
        status === "Succeeded"
          ? "Payment recorded. The invoice is paid."
          : "Failed payment recorded. The subscription was expired."
      );
      setFailureReason("");
      await load();
    } catch (paymentError) {
      setError(paymentError instanceof Error ? paymentError.message : "Could not record the payment.");
    } finally {
      setSubmitting(false);
    }
  }

  async function cancelInvoice() {
    if (!invoice || !window.confirm("Cancel this invoice?")) {
      return;
    }

    setError(null);
    setMessage(null);

    try {
      await api(`/invoices/${invoice.id}/cancel`, { method: "POST" });
      setMessage("Invoice cancelled.");
      await load();
    } catch (cancelError) {
      setError(cancelError instanceof Error ? cancelError.message : "Could not cancel the invoice.");
    }
  }

  if (loading) {
    return (
      <Card>
        <div className="loading">Loading invoice…</div>
      </Card>
    );
  }

  if (!invoice) {
    return (
      <>
        <ErrorBanner message={error ?? "Invoice not found."} />
        <Link className="button button-ghost" href="/invoices">
          Back to invoices
        </Link>
      </>
    );
  }

  return (
    <>
      <div className="breadcrumb">
        <Link href="/invoices">Invoices</Link> / {invoice.invoiceNumber ?? invoice.id.slice(0, 8)}
      </div>

      <PageHeader
        title={`Invoice ${invoice.invoiceNumber ?? invoice.id.slice(0, 8)}`}
        description={`${invoice.planNameSnapshot} · ${formatMoney(invoice.amount, invoice.currency)}`}
        actions={
          <div className="page-actions">
            <Link className="button button-ghost" href={`/invoices?subscriptionId=${invoice.subscriptionId}`}>
              Subscription invoices
            </Link>
            {invoice.status === "Open" ? (
              <button className="button button-danger" type="button" onClick={cancelInvoice}>
                Cancel invoice
              </button>
            ) : null}
          </div>
        }
      />

      <ErrorBanner message={error} />
      <SuccessBanner message={message} />

      <Card>
        <div className="detail-grid">
          <div className="detail-item">
            <span>Status</span>
            <strong>
              <StatusBadge status={invoice.status} />
            </strong>
          </div>
          <div className="detail-item">
            <span>Customer</span>
            <strong>
              {customer ? (
                <CustomerLink id={customer.id} name={customer.name} />
              ) : (
                <span className="table-muted">—</span>
              )}
            </strong>
          </div>
          <div className="detail-item">
            <span>Amount</span>
            <strong>{formatMoney(invoice.amount, invoice.currency)}</strong>
          </div>
          <div className="detail-item">
            <span>Invoice date</span>
            <strong>{formatDate(invoice.invoiceDate)}</strong>
          </div>
          <div className="detail-item">
            <span>Due date</span>
            <strong>{formatDate(invoice.dueDate)}</strong>
          </div>
          <div className="detail-item">
            <span>Billing period</span>
            <strong>
              {formatDate(invoice.periodStart)} – {formatDate(invoice.periodEnd)}
            </strong>
          </div>
          <div className="detail-item">
            <span>Subscription</span>
            <strong>
              {subscription ? subscription.status : <span className="table-muted">—</span>}
            </strong>
          </div>
          <div className="detail-item">
            <span>Plan snapshot</span>
            <strong>
              {invoice.planNameSnapshot} ({invoice.billingPeriodSnapshot})
            </strong>
          </div>
        </div>
      </Card>

      <h2 className="section-title">Payments</h2>

      <Card>
        {payments.length === 0 ? (
          <EmptyState>No payments recorded for this invoice.</EmptyState>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Method</th>
                  <th>Amount</th>
                  <th>Status</th>
                  <th>Failure reason</th>
                </tr>
              </thead>
              <tbody>
                {payments.map((payment) => (
                  <tr key={payment.id}>
                    <td>{formatDateTime(payment.paidAt)}</td>
                    <td>{payment.method}</td>
                    <td>{formatMoney(payment.amount, payment.currency)}</td>
                    <td>
                      <StatusBadge status={payment.status} />
                    </td>
                    <td className="table-muted">{payment.failureReason ?? "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {invoice.status === "Open" ? (
          <form className="inline-form" style={{ marginTop: 22 }} onSubmit={recordPayment}>
            <div className="field">
              <label htmlFor="payment-method">Method</label>
              <select
                id="payment-method"
                value={method}
                onChange={(event) => setMethod(event.target.value as PaymentMethod)}
              >
                <option value="Manual">Manual</option>
                <option value="Card">Card</option>
                <option value="BankTransfer">Bank transfer</option>
              </select>
            </div>
            <div className="field">
              <label htmlFor="payment-status">Outcome</label>
              <select
                id="payment-status"
                value={status}
                onChange={(event) => setStatus(event.target.value as PaymentStatus)}
              >
                <option value="Succeeded">Succeeded</option>
                <option value="Failed">Failed</option>
              </select>
            </div>
            {status === "Failed" ? (
              <div className="field">
                <label htmlFor="payment-reason">Failure reason</label>
                <input
                  id="payment-reason"
                  value={failureReason}
                  onChange={(event) => setFailureReason(event.target.value)}
                  placeholder="Card declined"
                />
              </div>
            ) : null}
            <button className="button" type="submit" disabled={submitting}>
              {submitting ? "Recording…" : "Record payment"}
            </button>
          </form>
        ) : (
          <div className="empty-state">This invoice is {invoice.status.toLowerCase()}; no further payments can be recorded.</div>
        )}
      </Card>
    </>
  );
}
