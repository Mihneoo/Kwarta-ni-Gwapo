export type EntryKind = "Income" | "Expense";
export type NeedType = "Need" | "Want" | "Installment";

export type Transaction = {
  id: number;
  kind: EntryKind;
  amount: number;
  description: string;
  category: string;
  needType?: NeedType | null;
  date: string;
};

export type Installment = {
  id: number;
  description: string;
  category: string;
  needType: "Need" | "Want";
  totalAmount: number;
  monthlyAmount: number;
  termMonths: number;
  paidMonths: number;
  startDate: string;
  nextDueDate: string;
  remainingAmount: number;
};

export type MonthPoint = { month: string; income: number; needs: number; wants: number; installments: number };
export type CategoryTotal = { category: string; amount: number; needType: NeedType };

export type DashboardData = {
  openingCapital: number;
  currentBalance: number;
  monthIncome: number;
  monthExpenses: number;
  monthNeeds: number;
  monthWants: number;
  monthInstallments: number;
  monthLabel: string;
  monthlyTrend: MonthPoint[];
  categoryBreakdown: CategoryTotal[];
  recentTransactions: Transaction[];
  upcomingInstallments: Installment[];
};
