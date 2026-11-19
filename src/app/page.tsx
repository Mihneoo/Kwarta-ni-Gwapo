"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { ArrowDown, ArrowLeftRight, ArrowUp, ArrowUpRight, CalendarDays, ChevronRight, Coffee, House, LayoutDashboard, Moon, Pencil, PieChart, Plus, Settings, ShieldCheck, ShoppingCart, Sparkles, Sun, Tag, Trash2, Utensils, Wallet, X, type LucideIcon } from "lucide-react";
import { createInstallment, createTransaction, deleteTransaction, getDashboard, getWithdrawals, payInstallment, updateCapital, updateSalarySchedule, updateTransaction } from "@/lib/api";
import type { CashWithdrawal, DashboardData, NeedType, Transaction } from "@/types";

type IconName = "grid" | "arrows" | "pie" | "calendar" | "settings" | "shield" | "plus" | "arrow" | "wallet" | "income" | "expense" | "chevron" | "close" | "food" | "home" | "cart" | "tag" | "coffee" | "spark" | "moon" | "sun" | "trash" | "edit";

const icons: Record<IconName, LucideIcon> = {
  grid: LayoutDashboard, arrows: ArrowLeftRight, pie: PieChart, calendar: CalendarDays,
  settings: Settings, shield: ShieldCheck, plus: Plus, arrow: ArrowUpRight, wallet: Wallet,
  income: ArrowUp, expense: ArrowDown, chevron: ChevronRight, close: X, food: Utensils,
  home: House, cart: ShoppingCart, tag: Tag, coffee: Coffee, spark: Sparkles,
  moon: Moon, sun: Sun, trash: Trash2, edit: Pencil,
};

function Icon({ name }: { name: IconName }) {
  const LucideIconComponent = icons[name];
  return <LucideIconComponent size={17} strokeWidth={1.7} aria-hidden="true"/>;
}

const money = (amount: number) => new Intl.NumberFormat("en-PH", { style: "currency", currency: "PHP", maximumFractionDigits: 0 }).format(amount);
const localDate = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
const monthNow = () => { const current = localDate(new Date()).slice(0, 7); return current < "2026-11" ? "2026-11" : current; };
const niceMonth = (value: string) => new Date(`${value}-01T00:00:00`).toLocaleDateString("en-PH", { month: "long", year: "numeric" });

function SpendingChart({ data }: { data: DashboardData["monthlyTrend"] }) {
  const width = 620, height = 205, left = 36, right = 8, top = 14, bottom = 28;
  const max = Math.max(10000, ...data.flatMap((point) => [point.income, point.needs + point.wants + point.installments]));
  const chartW = width - left - right, chartH = height - top - bottom;
  const pts = data.map((point, index) => ({ ...point, x: left + (data.length <= 1 ? chartW / 2 : index * chartW / (data.length - 1)), yIncome: top + chartH - point.income / max * chartH, ySpend: top + chartH - (point.needs + point.wants + point.installments) / max * chartH }));
  const line = (key: "yIncome" | "ySpend") => pts.map((point, index) => `${index ? "L" : "M"}${point.x},${point[key]}`).join(" ");
  const fmt = (n: number) => n >= 1000 ? `${Math.round(n / 1000)}k` : `${n}`;
  return <div className="chart-wrap"><svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Monthly income and spending trend">
    {[0, 1, 2, 3].map((i) => { const y = top + chartH * i / 3; return <g key={i}><line className="chart-grid" x1={left} x2={width - right} y1={y} y2={y}/><text className="chart-label" x="0" y={y + 3}>{fmt(max * (3 - i) / 3)}</text></g>; })}
    {pts.length > 1 && <><path className="chart-line-income" d={line("yIncome")}/><path className="chart-line-spend" d={line("ySpend")}/></>}
    {pts.map((point) => <g key={point.month}><circle className="chart-dot" cx={point.x} cy={point.yIncome} r="3.5"/><text className="chart-label" textAnchor="middle" x={point.x} y={height - 5}>{point.month}</text></g>)}
  </svg></div>;
}

type ModalKind = "transaction" | "editTransaction" | "installment" | "capital" | "settings" | null;

export default function Home() {
  const [month, setMonth] = useState(monthNow());
  const [data, setData] = useState<DashboardData | null>(null);
  const [online, setOnline] = useState(false);
  const [loading, setLoading] = useState(true);
  const [modal, setModal] = useState<ModalKind>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [activityError, setActivityError] = useState("");
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [editingTransaction, setEditingTransaction] = useState<Transaction | null>(null);
  const [theme, setTheme] = useState<"light" | "dark">("light");
  const [entryKind, setEntryKind] = useState<"Expense" | "Income" | "Withdrawal" | "CashExpense">("Expense");
  const [withdrawalSource, setWithdrawalSource] = useState<number | null>(null);
  const [withdrawals, setWithdrawals] = useState<CashWithdrawal[]>([]);
  const [selectedCategory, setSelectedCategory] = useState("Housing");
  const [salaryFrequency, setSalaryFrequency] = useState<"Every15Days" | "Manual">("Every15Days");
  const [salaryStartDate, setSalaryStartDate] = useState("2026-11-01");

  useEffect(() => {
    const storedTheme = window.localStorage.getItem("kwarta-theme");
    const nextTheme = storedTheme === "dark" ? "dark" : "light";
    setTheme(nextTheme);
    document.documentElement.dataset.theme = nextTheme;
  }, []);
  useEffect(() => {
    if (!data) return;
    setSalaryFrequency(data.salaryFrequency);
    setSalaryStartDate(data.nextPayDate ?? localDate(new Date()));
  }, [data]);

  const refresh = useCallback(async () => {
    setLoading(true);
    try { setData(await getDashboard(month)); setOnline(true); setError(""); }
    catch { setOnline(false); setActivityError("Could not refresh the saved activity. Check that the local API is running, then refresh the page."); }
    finally { setLoading(false); }
  }, [month]);
  useEffect(() => { void refresh(); }, [refresh]);
  useEffect(() => {
    if (modal !== "transaction" || entryKind !== "CashExpense") return;
    getWithdrawals().then((items) => {
      setWithdrawals(items);
      setWithdrawalSource((current) => current && items.some((item) => item.id === current && item.remainingAmount > 0)
        ? current : items.find((item) => item.remainingAmount > 0)?.id ?? null);
    }).catch((e) => setError(e instanceof Error ? e.message : "Could not load withdrawals."));
  }, [modal, entryKind]);

  const monthOptions = useMemo(() => {
    const first = new Date(2026, 10, 1), now = new Date(); now.setDate(1);
    const count = Math.max(1, (now.getFullYear() - first.getFullYear()) * 12 + now.getMonth() - first.getMonth() + 1);
    return Array.from({ length: count }, (_, index) => { const d = new Date(first); d.setMonth(first.getMonth() + index); return localDate(d).slice(0, 7); }).reverse();
  }, []);

  function changeTheme() {
    const nextTheme = theme === "dark" ? "light" : "dark";
    setTheme(nextTheme);
    document.documentElement.dataset.theme = nextTheme;
    window.localStorage.setItem("kwarta-theme", nextTheme);
  }

  async function submitTransaction(form: FormData) {
    setSaving(true); setError(""); setActivityError("");
    try {
      const kind = String(form.get("kind") ?? editingTransaction?.kind ?? entryKind) as "Income" | "Expense" | "Withdrawal" | "CashExpense";
      const transaction = { kind, amount: Number(form.get("amount")), description: String(form.get("description")), category: String(form.get("category")), needType: (editingTransaction?.needType === "Installment" ? "Installment" : kind === "Expense" || kind === "CashExpense" ? String(form.get("needType")) : null) as NeedType | null, date: String(form.get("date")), withdrawalId: kind === "CashExpense" ? Number(form.get("withdrawalId")) : null };
      const saved = editingTransaction
        ? await updateTransaction(editingTransaction.id, transaction)
        : await createTransaction(transaction);
      setData((current) => current ? {
        ...current,
        recentTransactions: [saved, ...current.recentTransactions.filter((item) => item.id !== saved.id)]
          .sort((a, b) => b.date.localeCompare(a.date) || b.id - a.id),
      } : current);
      setModal(null); setEditingTransaction(null); await refresh();
    } catch (e) { setError(e instanceof Error ? e.message : "Could not save entry. Check that the local API is running."); }
    finally { setSaving(false); }
  }

  async function submitInstallment(form: FormData) {
    setSaving(true); setError("");
    try {
      await createInstallment({ description: String(form.get("description")), category: String(form.get("category")), needType: String(form.get("needType")) as "Need" | "Want", totalAmount: Number(form.get("totalAmount")), termMonths: Number(form.get("termMonths")), frequency: String(form.get("frequency")) as "Monthly" | "Every15Days", startDate: String(form.get("startDate")) });
      setModal(null); await refresh();
    } catch (e) { setError(e instanceof Error ? e.message : "Could not save plan. Check that the local API is running."); }
    finally { setSaving(false); }
  }

  async function submitCapital(form: FormData) {
    setSaving(true); setError("");
    try {
      await updateCapital(Number(form.get("amount")));
      setModal(null); await refresh();
    } catch (e) { setError(e instanceof Error ? e.message : "Could not update opening capital. Check that the local API is running."); }
    finally { setSaving(false); }
  }

  async function submitSettings(form: FormData) {
    setSaving(true); setError("");
    try {
      await updateCapital(Number(form.get("openingCapital")));
      const frequency = String(form.get("salaryFrequency")) as "Every15Days" | "Manual";
      await updateSalarySchedule(frequency, frequency === "Every15Days" ? String(form.get("salaryStartDate")) : null);
      setModal(null); await refresh();
    } catch (e) { setError(e instanceof Error ? e.message : "Could not save settings. Check that the local API is running."); }
    finally { setSaving(false); }
  }

  async function removeTransaction(id: number, description: string) {
    if (!window.confirm(`Delete “${description}” from your activity?`)) return;
    setDeletingId(id); setActivityError("");
    try { await deleteTransaction(id); await refresh(); }
    catch (e) {
      const message = e instanceof Error ? e.message : "Could not delete this entry.";
      setActivityError(message.includes("(404)") || message.includes("(405)")
        ? "The API is still running an older build. Stop it with Ctrl+C, then run dotnet run --project api again."
        : message);
    }
    finally { setDeletingId(null); }
  }

  function openEditTransaction(transaction: Transaction) {
    setEditingTransaction(transaction);
    setEntryKind(transaction.kind);
    setSelectedCategory(transaction.category);
    setError("");
    setModal("editTransaction");
  }

  const categories = data?.categoryBreakdown ?? [];
  const maxCat = Math.max(1, ...categories.map((cat) => cat.amount));

  return <div className="app-shell">
    <aside className="sidebar">
      <div className="brand"><span className="brand-mark"><Icon name="wallet"/></span> kwarta<span style={{ color: "#aaa", fontWeight: 400 }}>.</span></div>
      <div className="nav-label">Workspace</div>
      <a className="nav-link active" href="#overview"><Icon name="grid"/> Overview</a>
      <div className="nav-label" style={{ marginTop: 29 }}>Preferences</div>
      <button className="nav-link" onClick={() => { setError(""); setModal("settings"); }} style={{ width: "100%", border: 0, background: "transparent", textAlign: "left" }}><Icon name="settings"/> Settings</button>
      <div className="sidebar-spacer"/>
      <div className="privacy-card"><div className="privacy-title"><Icon name="shield"/> Your money stays yours</div><p>Private by design. Your data is saved on this laptop and never sent to a cloud.</p></div>
      <div className="profile"><div className="avatar">G</div><div><div className="profile-name">My wallet</div><div className="profile-sub">Personal account</div></div></div>
    </aside>
    <main className="main"><div className="main-inner">
      <div className="mobile-top"><div className="brand"><span className="brand-mark"><Icon name="wallet"/></span> kwarta<span style={{ color: "#aaa", fontWeight: 400 }}>.</span></div><div className="top-actions"><span className="api-status"><i className={`status-dot ${online ? "online" : ""}`}/>{online ? "Local database" : "API offline"}</span><button className="theme-toggle" aria-label={`Switch to ${theme === "dark" ? "light" : "dark"} mode`} onClick={changeTheme}><Icon name={theme === "dark" ? "sun" : "moon"}/></button></div></div>
      <header className="topbar" id="overview"><div><div className="eyebrow">Your money, made clear · since November 2026</div><h1>Good day, Gwapo<span style={{ color: "#aaa" }}>.</span></h1></div><div className="top-actions"><select className="month-select" aria-label="Select month" value={month} onChange={(event) => setMonth(event.target.value)}>{monthOptions.map((option) => <option key={option} value={option}>{niceMonth(option)}</option>)}</select><button className="theme-toggle" aria-label={`Switch to ${theme === "dark" ? "light" : "dark"} mode`} onClick={changeTheme}><Icon name={theme === "dark" ? "sun" : "moon"}/></button><button className="button" onClick={() => { setEditingTransaction(null); setError(""); setEntryKind("Expense"); setSelectedCategory("Housing"); setModal("transaction"); }}><Icon name="plus"/><span className="button-label">Add entry</span></button></div></header>
      {!online && <div className="notice"><strong>Connect your local database to get started.</strong> Start the API with <code>dotnet run --project api</code> and this dashboard will load your private data. The database file is created on your laptop automatically.</div>}
      {loading && !data ? <div className="card" style={{ padding: 30, textAlign: "center", color: "#999", fontSize: 11 }}>Loading your money overview…</div> : <>
        <section className="summary-grid">
          <button className="card summary-card featured" onClick={() => { setError(""); setModal("capital"); }} style={{ textAlign: "left", cursor: "pointer" }} title="Update opening capital"><div className="summary-label">Bank balance <Icon name="wallet"/></div><div className="summary-value">{money(data?.bankBalance ?? 0)}</div><div className="summary-note">Total money {money(data?.totalBalance ?? 0)} · edit opening balance</div></button>
          <div className="card summary-card"><div className="summary-label">Cash on hand <Icon name="wallet"/></div><div className="summary-value">{money(data?.cashOnHand ?? 0)}</div><div className="summary-note">Withdrawn, not yet spent</div></div>
          <div className="card summary-card"><div className="summary-label">Income this month <Icon name="income"/></div><div className="summary-value">{money(data?.monthIncome ?? 0)}</div><div className="summary-note">{data?.salaryFrequency === "Every15Days" && data.nextPayDate ? `Next payday · ${new Date(`${data.nextPayDate}T00:00:00`).toLocaleDateString("en-PH", { month: "short", day: "numeric" })}` : "Add salary whenever it arrives"}</div></div>
          <div className="card summary-card"><div className="summary-label">Spent this month <Icon name="expense"/></div><div className="summary-value">{money(data?.monthExpenses ?? 0)}</div><div className="summary-note">Across needs, wants & plans</div></div>
          <div className="card summary-card"><div className="summary-label">Monthly commitments <Icon name="calendar"/></div><div className="summary-value">{money(data?.monthInstallments ?? 0)}</div><div className="summary-note">Installment payments due</div></div>
        </section>
        <section className="trend-row">
          <div className="card panel"><div className="panel-head"><div><div className="panel-title">Your money over time</div><div className="panel-subtitle">Income compared with what went out · from November 2026</div></div><div className="legend"><span><i/>Income</span><span><i className="legend-needs"/>Spending</span></div></div><SpendingChart data={data?.monthlyTrend ?? []}/></div>
          <div className="card panel" id="categories"><div className="panel-head"><div><div className="panel-title">Where it went</div><div className="panel-subtitle">{niceMonth(month)} · {money(data?.monthExpenses ?? 0)} total</div></div><span className="api-status">Needs & wants</span></div>
            {categories.length ? <div className="category-list">{categories.slice(0, 5).map((cat) => <div className="category-row" key={cat.category}><span className="category-icon"><Icon name={cat.category.toLowerCase().includes("food") || cat.category.toLowerCase().includes("dining") ? "food" : cat.category.toLowerCase().includes("home") || cat.category.toLowerCase().includes("rent") || cat.category.toLowerCase().includes("bill") ? "home" : cat.category.toLowerCase().includes("shop") ? "cart" : cat.needType === "Want" ? "spark" : "tag"}/></span><div><div className="category-name">{cat.category}</div><div className="category-meta">{cat.needType === "Want" ? "Want" : cat.needType === "Installment" ? "Installment" : "Need"}</div></div><div><div className="category-amount">{money(cat.amount)}</div><div className="category-bar"><i style={{ width: `${Math.round(cat.amount / maxCat * 100)}%` }}/></div></div></div>)}</div> : <div className="empty-state">No spending recorded this month yet.</div>}
            <div className="category-summary"><span>Needs <strong>{money(data?.monthNeeds ?? 0)}</strong></span><span>Wants <strong>{money(data?.monthWants ?? 0)}</strong></span></div>
          </div>
        </section>
        <section className="bottom-row">
          <div className="card panel" id="recent-activity"><div className="panel-head"><div><div className="panel-title">Recent activity</div><div className="panel-subtitle">Your latest ins and outs</div></div><button className="button secondary" style={{ minHeight: 29, fontSize: 9 }} onClick={() => { setEditingTransaction(null); setError(""); setEntryKind("Expense"); setSelectedCategory("Housing"); setModal("transaction"); }}><Icon name="plus"/> Add entry</button></div>
            {activityError && <div className="form-error">{activityError}</div>}
            {data?.recentTransactions.length ? <div className="transaction-list">{data.recentTransactions.map((item) => <div className="transaction-row" key={item.id}>
              <span className="transaction-icon"><Icon name={item.kind === "Income" ? "income" : item.category.toLowerCase().includes("food") ? "food" : "tag"}/></span>
              <div><div className="transaction-title">{item.description}</div><div className="transaction-category">{item.category} · {new Date(`${item.date.slice(0, 10)}T00:00:00`).toLocaleDateString("en-PH", { month: "short", day: "numeric" })}</div></div>
              <span className="pill">{item.kind === "Income" ? "Income" : item.needType === "Want" ? "Want" : item.needType === "Installment" ? "Installment" : "Need"}</span>
              <div className={`transaction-amount ${item.kind === "Income" ? "income" : ""}`}>{item.kind === "Income" ? "+" : "−"}{money(item.amount)}</div>
              <button type="button" className="row-action edit-entry" aria-label={`Edit ${item.description}`} title="Edit entry" onClick={() => openEditTransaction(item)}><Icon name="edit"/></button>
              <button type="button" className="row-action delete-entry" aria-label={`Delete ${item.description}`} title="Delete entry" disabled={deletingId === item.id} onClick={() => void removeTransaction(item.id, item.description)}><Icon name="trash"/><span>Delete</span></button>
            </div>)}</div> : <div className="empty-state">Your entries will show up here.</div>}
          </div>
          <div className="card panel" id="installments"><div className="panel-head"><div><div className="panel-title">Paying over time</div><div className="panel-subtitle">Keep track of what’s still owed</div></div><button className="button secondary" style={{ minHeight: 29, fontSize: 9 }} onClick={() => { setError(""); setModal("installment"); }}><Icon name="plus"/> New plan</button></div>
            {data?.upcomingInstallments.length ? <div className="installment-list">{data.upcomingInstallments.map((plan) => <div className="installment-item" key={plan.id}><div className="installment-top"><div><div className="installment-name">{plan.description}</div><div className="installment-next">Next · {new Date(`${plan.nextDueDate.slice(0, 10)}T00:00:00`).toLocaleDateString("en-PH", { month: "short", day: "numeric", year: "numeric" })} · {plan.paidPayments}/{plan.paymentCount} paid</div></div><div className="installment-amount">{money(plan.paymentAmount)}<small>{plan.frequency === "Every15Days" ? "every 15 days" : "per month"}</small></div></div><div className="progress-track"><span style={{ width: `${Math.min(100, plan.paidPayments / plan.paymentCount * 100)}%` }}/></div><div className="installment-foot"><span>{money(plan.remainingAmount)} remaining</span>{plan.nextDueDate <= localDate(new Date()) ? <button onClick={async () => { try { await payInstallment(plan.id, localDate(new Date())); await refresh(); } catch { setError("Could not record payment. Check that the local API is running."); } }} style={{ border: 0, padding: 0, background: "transparent", color: "#666", fontSize: 8, textDecoration: "underline" }}>Record payment</button> : <span>Upcoming</span>}</div></div>)}</div> : <div className="empty-state">No active payment plans. Add one when you buy something on credit.</div>}
          </div>
        </section>
      </>}
      <div className="privacy-mobile"><Icon name="shield"/> Your data is stored locally on this device.</div>
    </div></main>
    {modal && <div className="modal-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) setModal(null); }}><div className="modal" role="dialog" aria-modal="true" aria-labelledby="modal-title"><div className="modal-head"><div><div className="modal-title" id="modal-title">{modal === "transaction" ? "Add an entry" : modal === "editTransaction" ? "Edit entry" : modal === "installment" ? "New payment plan" : modal === "settings" ? "Settings" : "Set opening capital"}</div><div className="modal-desc">{modal === "transaction" ? "Keep your money picture up to date." : modal === "editTransaction" ? "Change the details of this activity." : modal === "installment" ? "Spread the purchase into scheduled payments." : modal === "settings" ? "Set your opening balance and paydays." : "Enter the money you had when you started tracking."}</div></div><button className="icon-button" aria-label="Close" onClick={() => { setModal(null); setEditingTransaction(null); }}><Icon name="close"/></button></div>
      <form key={editingTransaction?.id ?? "new-entry"} action={async (form) => { if (modal === "transaction" || modal === "editTransaction") await submitTransaction(form); else if (modal === "installment") await submitInstallment(form); else if (modal === "settings") await submitSettings(form); else await submitCapital(form); }}>
        <div className="form-grid">
          {modal === "capital" ? <div className="field wide"><label htmlFor="amount">Opening capital (PHP)</label><input name="amount" id="amount" type="number" min="0" step="0.01" defaultValue={data?.openingCapital ?? 0} required/></div> : null}
          {modal === "settings" ? <>
            <div className="field wide"><label htmlFor="openingCapital">Opening capital (PHP)</label><input name="openingCapital" id="openingCapital" type="number" min="0" step="0.01" defaultValue={data?.openingCapital ?? 0} required/></div>
            <div className="field wide setting-group"><p className="setting-title">Salary schedule</p><label htmlFor="salaryFrequency">Payday timing</label><select name="salaryFrequency" id="salaryFrequency" value={salaryFrequency} onChange={(event) => setSalaryFrequency(event.target.value as "Every15Days" | "Manual")}><option value="Every15Days">Every 15 days</option><option value="Manual">I’ll record each payday</option></select>{salaryFrequency === "Every15Days" ? <div className="field" style={{ marginTop: 10 }}><label htmlFor="salaryStartDate">Next payday</label><input name="salaryStartDate" id="salaryStartDate" type="date" min="2026-11-01" value={salaryStartDate} onChange={(event) => setSalaryStartDate(event.target.value)} required/></div> : null}<p className="setting-help">Salary amounts are always entered when received, so the take-home amount can change with deductions.</p></div>
          </> : null}
          {modal === "transaction" || modal === "editTransaction" ? <>
            {editingTransaction?.needType === "Installment" ? <div className="field"><label>Entry type</label><div className="readonly-field">Installment payment</div><input type="hidden" name="kind" value="Expense"/></div> : <div className="field"><label htmlFor="kind">Entry type</label><select name="kind" id="kind" value={entryKind} onChange={(event) => { const next = event.target.value as "Expense" | "Income" | "Withdrawal" | "CashExpense"; setEntryKind(next); setSelectedCategory(next === "Income" ? "Salary" : "Housing"); }}><option value="Expense">Expense from bank</option><option value="Income">Income / paycheck</option><option value="Withdrawal">Withdraw cash</option><option value="CashExpense">Spend withdrawn cash</option></select></div>}
            <div className="field"><label htmlFor="amount">Amount (PHP)</label><input name="amount" id="amount" type="number" min="0.01" step="0.01" defaultValue={editingTransaction?.amount} placeholder="0.00" required/></div>
            <div className="field wide"><label htmlFor="description">Description</label><input name="description" id="description" defaultValue={editingTransaction?.description} placeholder={entryKind === "Income" ? "e.g. Salary after deductions" : "e.g. Grocery run"} required/></div>
            {entryKind === "CashExpense" ? <div className="field wide"><label htmlFor="withdrawalId">Spend from withdrawal</label><select name="withdrawalId" id="withdrawalId" value={withdrawalSource ?? ""} onChange={(event) => setWithdrawalSource(Number(event.target.value) || null)} required><option value="">{withdrawals.some((item) => item.remainingAmount > 0) ? "Choose a withdrawal" : "No unspent withdrawals found"}</option>{withdrawals.filter((item) => item.remainingAmount > 0).map((item) => <option key={item.id} value={item.id}>{item.description} · {money(item.remainingAmount)} left</option>)}</select></div> : null}
            {editingTransaction?.needType === "Installment" ? <div className="field wide"><label>Category</label><div className="readonly-field">{editingTransaction.category}</div><input type="hidden" name="category" value={editingTransaction.category}/><input type="hidden" name="needType" value="Installment"/></div> : <div className="field wide"><label>Quick category</label><input type="hidden" name="category" value={selectedCategory}/><div className="category-buttons">{(entryKind === "Income" ? [["Salary", "Salary"], ["Other", "Other"]] : [["Housing", "Housing"], ["Food", "Food"], ["Transport", "Transport"], ["Bills", "Bills"], ["Shopping", "Shopping"], ["Health", "Health"], ["Entertainment", "Entertainment"], ["Other", "Other"]]).map(([label, value]) => <button key={value} type="button" className={`category-choice ${selectedCategory === value ? "selected" : ""}`} onClick={() => setSelectedCategory(value)}>{label}</button>)}</div></div>}
            {editingTransaction?.needType === "Installment" ? <div className="field"><label>Type</label><div className="readonly-field">Installment</div></div> : entryKind === "Expense" || entryKind === "CashExpense" ? <div className="field"><label htmlFor="needType">Type</label><select name="needType" id="needType" defaultValue={editingTransaction?.needType ?? "Need"}><option value="Need">Need</option><option value="Want">Want</option></select></div> : null}
            <div className="field wide"><label htmlFor="date">{entryKind === "Income" ? "Payday" : "Date"}</label><input name="date" id="date" type="date" min="2026-11-01" defaultValue={editingTransaction?.date ?? localDate(new Date())} required/></div>
          </> : null}
          {modal === "installment" ? <>
            <div className="field wide"><label htmlFor="description">What did you buy?</label><input name="description" id="description" placeholder="e.g. Laptop" required/></div>
            <div className="field"><label htmlFor="totalAmount">Total price (PHP)</label><input name="totalAmount" id="totalAmount" type="number" min="0.01" step="0.01" placeholder="0.00" required/></div>
            <div className="field"><label htmlFor="termMonths">Months to pay</label><input name="termMonths" id="termMonths" type="number" min="1" max="120" defaultValue="12" required/></div>
            <div className="field"><label htmlFor="frequency">Payment cadence</label><select name="frequency" id="frequency" defaultValue="Every15Days"><option value="Every15Days">Every 15 days</option><option value="Monthly">Monthly</option></select></div>
            <div className="field"><label htmlFor="category">Category</label><select name="category" id="category"><option>Electronics</option><option>Home</option><option>Shopping</option><option>Health</option><option>Other</option></select></div>
            <div className="field"><label htmlFor="needType">Type</label><select name="needType" id="needType"><option value="Need">Need</option><option value="Want">Want</option></select></div>
            <div className="field wide"><label htmlFor="startDate">First payment date</label><input name="startDate" id="startDate" type="date" min="2026-11-01" defaultValue={localDate(new Date())} required/></div>
          </> : null}
        </div>
        {error && <div className="form-error">{error}</div>}
        <div className="form-actions"><button type="button" className="button secondary" onClick={() => { setModal(null); setEditingTransaction(null); }}>Cancel</button><button type="submit" className="button" disabled={saving}>{saving ? "Saving…" : modal === "transaction" ? "Save entry" : modal === "editTransaction" ? "Update entry" : modal === "installment" ? "Create plan" : modal === "settings" ? "Save settings" : "Save capital"}</button></div>
      </form>
    </div></div>}
  </div>;
}
