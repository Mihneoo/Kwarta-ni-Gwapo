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
  paymentAmount: number;
  frequency: "Monthly" | "Every15Days";
  termMonths: number;
  paymentCount: number;
  paidPayments: number;
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
  salaryFrequency: "Every15Days" | "Manual";
  nextPayDate: string | null;
  monthlyTrend: MonthPoint[];
  categoryBreakdown: CategoryTotal[];
  recentTransactions: Transaction[];
  upcomingInstallments: Installment[];
};
