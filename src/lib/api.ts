import type { DashboardData, Installment, Transaction } from "@/types";

export const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5055";

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
  return response.json() as Promise<T>;
}

export const getDashboard = (month: string) => request<DashboardData>(`/api/dashboard?month=${month}`);
export const updateCapital = (amount: number) => request<{ openingCapital: number }>("/api/settings/capital", { method: "PUT", body: JSON.stringify({ amount }) });
export const createTransaction = (data: Omit<Transaction, "id">) => request<Transaction>("/api/transactions", { method: "POST", body: JSON.stringify(data) });
export const createInstallment = (data: { description: string; category: string; needType: "Need" | "Want"; totalAmount: number; termMonths: number; startDate: string }) => request<Installment>("/api/installments", { method: "POST", body: JSON.stringify(data) });
export const payInstallment = (id: number, date: string) => request<Installment>(`/api/installments/${id}/payments`, { method: "POST", body: JSON.stringify({ date }) });
