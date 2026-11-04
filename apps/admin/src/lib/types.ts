export type Paged<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type Customer = {
  id: string;
  name: string;
  email: string;
  companyName: string | null;
  isDeleted: boolean;
  createdAt: string;
  updatedAt: string;
};

export type BillingPeriod = "Monthly" | "Yearly";

export type Plan = {
  id: string;
  name: string;
  description: string | null;
  price: number;
  currency: string;
  billingPeriod: BillingPeriod;
  isDeleted: boolean;
  createdAt: string;
  updatedAt: string;
};

export type SubscriptionStatus = "Trialing" | "Active" | "Cancelled" | "Expired";

export type Subscription = {
  id: string;
  customerId: string;
  planId: string;
  status: SubscriptionStatus;
  startDate: string;
  trialEndDate: string | null;
  nextBillingDate: string;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  billingAnchorDay: number;
  pendingPlanId: string | null;
  cancelledAt: string | null;
  isDeleted: boolean;
  createdAt: string;
  updatedAt: string;
};

export type InvoiceStatus = "Open" | "Paid" | "Cancelled";

export type Invoice = {
  id: string;
  subscriptionId: string;
  invoiceNumber: string | null;
  invoiceDate: string;
  dueDate: string;
  periodStart: string;
  periodEnd: string;
  amount: number;
  currency: string;
  planNameSnapshot: string;
  billingPeriodSnapshot: BillingPeriod;
  status: InvoiceStatus;
  isDeleted: boolean;
  createdAt: string;
  updatedAt: string;
};

export type PaymentMethod = "Manual" | "Card" | "BankTransfer";
export type PaymentStatus = "Succeeded" | "Failed";

export type Payment = {
  id: string;
  invoiceId: string;
  amount: number;
  currency: string;
  method: PaymentMethod;
  status: PaymentStatus;
  paidAt: string;
  failureReason: string | null;
  createdAt: string;
};
