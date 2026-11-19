import type { CashWithdrawal, DashboardData, Installment, Transaction } from "@/types";

// Use the same-origin Next.js proxy by default; this avoids browser CORS
// failures when the dashboard is opened through localhost or a LAN address.
export const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, {
    ...init,
    headers: { "Content-Type": "application/json", ...init?.headers },
    cache: "no-store",
  });
  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || `Request failed (${response.status})`);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const getDashboard = (month: string) => request<DashboardData>(`/api/dashboard?month=${month}`);
export const getWithdrawals = async (): Promise<CashWithdrawal[]> => {
  const transactions = await request<Transaction[]>("/api/transactions");
  const withdrawals = transactions.filter((item) => item.kind === "Withdrawal");
  return withdrawals.map((item) => ({
    id: item.id,
    description: item.description,
    amount: item.amount,
    date: item.date,
    remainingAmount: Math.max(0, item.amount - transactions
      .filter((entry) => entry.kind === "CashExpense" && entry.withdrawalId === item.id)
      .reduce((sum, entry) => sum + entry.amount, 0)),
  }));
};
export const updateCapital = (amount: number) => request<{ openingCapital: number }>("/api/settings/capital", { method: "PUT", body: JSON.stringify({ amount }) });
export const updateSalarySchedule = (frequency: "Every15Days" | "Manual", startDate: string | null) => request<{ frequency: string; nextPayDate: string | null }>("/api/settings/salary-schedule", { method: "PUT", body: JSON.stringify({ frequency, startDate }) });
export const createTransaction = (data: Omit<Transaction, "id">) => request<Transaction>("/api/transactions", { method: "POST", body: JSON.stringify(data) });
export const updateTransaction = (id: number, data: Omit<Transaction, "id">) => request<Transaction>(`/api/transactions/${id}`, { method: "PUT", body: JSON.stringify(data) });
export const deleteTransaction = (id: number) => request<void>(`/api/transactions/${id}`, { method: "DELETE" });
export const createInstallment = (data: { description: string; category: string; needType: "Need" | "Want"; totalAmount: number; termMonths: number; frequency: "Monthly" | "Every15Days"; startDate: string }) => request<Installment>("/api/installments", { method: "POST", body: JSON.stringify(data) });
export const payInstallment = (id: number, date: string) => request<Installment>(`/api/installments/${id}/payments`, { method: "POST", body: JSON.stringify({ date }) });
