using System.Globalization;
using Kwarta.Api;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dataDirectory);
var databasePath = Path.Combine(dataDirectory, "kwarta.db");
builder.Services.AddDbContext<MoneyDb>(options => options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:3000", "http://127.0.0.1:3000")
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MoneyDb>();
    await db.Database.MigrateAsync();
    await SeedData.Initialize(db);
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", storage = "local SQLite" }));

app.MapGet("/api/settings", async (MoneyDb db) =>
{
    var settings = await db.Settings.SingleAsync();
    return Results.Ok(new { openingCapital = settings.OpeningCapital });
});

app.MapPut("/api/settings/capital", async (CapitalInput input, MoneyDb db) =>
{
    if (input.Amount < 0 || input.Amount > 1_000_000_000) return Results.BadRequest("Enter a valid opening capital amount.");
    var settings = await db.Settings.SingleAsync();
    settings.OpeningCapital = input.Amount;
    await db.SaveChangesAsync();
    return Results.Ok(new { openingCapital = settings.OpeningCapital });
});

app.MapPut("/api/settings/salary-schedule", async (SalaryScheduleInput input, MoneyDb db) =>
{
    if (input.Frequency is not ("Every15Days" or "Manual")) return Results.BadRequest("Choose a 15-day schedule or manual pay dates.");
    if (input.Frequency == "Every15Days" && input.StartDate is null) return Results.BadRequest("Choose the next payday.");
    if (input.StartDate is not null && input.StartDate.Value.Date < new DateTime(2026, 11, 1)) return Results.BadRequest("Tracking starts in November 2026.");
    var schedule = await db.SalarySchedules.SingleAsync();
    schedule.Frequency = input.Frequency;
    var nextPayDate = input.Frequency == "Every15Days" ? input.StartDate!.Value.Date : (DateTime?)null;
    while (nextPayDate is not null && nextPayDate.Value < DateTime.Today) nextPayDate = nextPayDate.Value.AddDays(15);
    schedule.NextPayDate = nextPayDate;
    await db.SaveChangesAsync();
    return Results.Ok(new { frequency = schedule.Frequency, nextPayDate = schedule.NextPayDate?.ToString("yyyy-MM-dd") });
});

app.MapGet("/api/transactions", async (string? from, string? to, MoneyDb db) =>
{
    var firstTrackedMonth = new DateTime(2026, 11, 1);
    var query = db.Transactions.Where(t => t.Date >= firstTrackedMonth);
    if (DateTime.TryParse(from, CultureInfo.InvariantCulture, out var fromDate)) query = query.Where(t => t.Date >= fromDate.Date);
    if (DateTime.TryParse(to, CultureInfo.InvariantCulture, out var toDate)) query = query.Where(t => t.Date < toDate.Date.AddDays(1));
    var list = await query.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).ToListAsync();
    return Results.Ok(list.Select(ApiShape.Transaction));
});

app.MapGet("/api/withdrawals", async (MoneyDb db) =>
{
    var withdrawals = await db.Transactions.AsNoTracking().Where(t => t.Kind == "Withdrawal")
        .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).ToListAsync();
    var spentByWithdrawal = await db.Transactions.AsNoTracking()
        .Where(t => t.Kind == "CashExpense" && t.WithdrawalId != null)
        .Select(t => new { t.WithdrawalId, t.Amount }).ToListAsync();
    return Results.Ok(withdrawals.Select(item => new
    {
        id = item.Id,
        description = item.Description,
        amount = item.Amount,
        date = item.Date.ToString("yyyy-MM-dd"),
        remainingAmount = Math.Max(0m, item.Amount - spentByWithdrawal
            .Where(spent => spent.WithdrawalId == item.Id).Sum(spent => spent.Amount))
    }));
});

app.MapGet("/api/transactions/{id:int}", async (int id, MoneyDb db) =>
{
    var transaction = await db.Transactions.FindAsync(id);
    return transaction is null ? Results.NotFound() : Results.Ok(ApiShape.Transaction(transaction));
});

app.MapPost("/api/transactions", async (TransactionInput input, MoneyDb db) =>
{
    var validation = ValidateTransaction(input);
    if (validation is not null) return Results.BadRequest(validation);
    if (input.Kind == "Withdrawal")
    {
        var openingCapital = await db.Settings.Select(s => s.OpeningCapital).SingleAsync();
        var existing = await db.Transactions.AsNoTracking().ToListAsync();
        var bankBalance = openingCapital + existing.Sum(t => t.Kind switch
        {
            "Income" => t.Amount,
            "Expense" or "Withdrawal" => -t.Amount,
            _ => 0m
        });
        if (input.Amount > bankBalance) return Results.BadRequest("The withdrawal is more than your current bank balance.");
    }
    var transaction = new MoneyTransaction
    {
        Kind = input.Kind,
        Amount = decimal.Round(input.Amount, 2),
        Description = input.Description.Trim(),
        Category = input.Category.Trim(),
        NeedType = input.Kind is "Income" or "Withdrawal" ? null : (input.NeedType == "Want" ? "Want" : "Need"),
        Date = input.Date.Date
    };
    if (input.Kind == "CashExpense")
    {
        var withdrawal = input.WithdrawalId is int withdrawalId ? await db.Transactions.FindAsync(withdrawalId) : null;
        if (withdrawal is null || withdrawal.Kind != "Withdrawal") return Results.BadRequest("Choose a valid withdrawal to spend from.");
        var spentAmounts = await db.Transactions.AsNoTracking()
            .Where(t => t.WithdrawalId == withdrawal.Id && t.Kind == "CashExpense")
            .Select(t => t.Amount).ToListAsync();
        var spent = spentAmounts.Sum();
        if (spent + input.Amount > withdrawal.Amount) return Results.BadRequest("This spending is more than the cash left in that withdrawal.");
        transaction.WithdrawalId = withdrawal.Id;
    }
    db.Transactions.Add(transaction);
    if (transaction.Kind == "Income" && transaction.Category.Equals("Salary", StringComparison.OrdinalIgnoreCase))
    {
        var schedule = await db.SalarySchedules.SingleAsync();
        if (schedule.Frequency == "Every15Days" && schedule.NextPayDate is not null)
        {
            while (schedule.NextPayDate.Value <= transaction.Date) schedule.NextPayDate = schedule.NextPayDate.Value.AddDays(15);
        }
    }
    await db.SaveChangesAsync();
    return Results.Created($"/api/transactions/{transaction.Id}", ApiShape.Transaction(transaction));
});

app.MapPut("/api/transactions/{id:int}", async (int id, TransactionInput input, MoneyDb db) =>
{
    var validation = ValidateTransaction(input);
    if (validation is not null) return Results.BadRequest(validation);
    var transaction = await db.Transactions.FindAsync(id);
    if (transaction is null) return Results.NotFound();
    if (transaction.InstallmentPlanId is not null && (input.Kind != "Expense" || input.NeedType != "Installment"))
        return Results.BadRequest("Installment payments must remain expenses in their installment category.");
    var wasSalary = transaction.Kind == "Income" && transaction.Category.Equals("Salary", StringComparison.OrdinalIgnoreCase);

    transaction.Kind = input.Kind;
    transaction.Amount = decimal.Round(input.Amount, 2);
    transaction.Description = input.Description.Trim();
    transaction.Category = input.Category.Trim();
    transaction.Date = input.Date.Date;
    transaction.NeedType = transaction.InstallmentPlanId is not null
        ? "Installment"
        : input.Kind == "Income" || input.Kind == "Withdrawal" ? null : (input.NeedType == "Want" ? "Want" : "Need");
    await db.SaveChangesAsync();
    var isSalary = transaction.Kind == "Income" && transaction.Category.Equals("Salary", StringComparison.OrdinalIgnoreCase);
    if (wasSalary || isSalary)
    {
        var schedule = await db.SalarySchedules.SingleAsync();
        if (schedule.Frequency == "Every15Days")
        {
            var latestSalary = await db.Transactions
                .Where(item => item.Kind == "Income" && item.Category == "Salary")
                .OrderByDescending(item => item.Date).ThenByDescending(item => item.Id)
                .FirstOrDefaultAsync();
            var nextPayDate = (latestSalary?.Date ?? transaction.Date).AddDays(15);
            while (nextPayDate < DateTime.Today) nextPayDate = nextPayDate.AddDays(15);
            schedule.NextPayDate = nextPayDate;
            await db.SaveChangesAsync();
        }
    }
    return Results.Ok(ApiShape.Transaction(transaction));
});

app.MapDelete("/api/transactions/{id:int}", async (int id, MoneyDb db) =>
{
    var transaction = await db.Transactions.FindAsync(id);
    if (transaction is null) return Results.NotFound();

    if (transaction.InstallmentPlanId is int planId)
    {
        var plan = await db.Installments.FindAsync(planId);
        if (plan is not null)
        {
            var remainingPayments = await db.Transactions
                .Where(item => item.InstallmentPlanId == planId && item.Id != id)
                .OrderBy(item => item.Date).ThenBy(item => item.Id).ToListAsync();
            db.Transactions.Remove(transaction);
            for (var index = 0; index < remainingPayments.Count; index++)
                remainingPayments[index].Description = $"{plan.Description} · payment {index + 1}";
            plan.PaidPayments = remainingPayments.Count;
            if (plan.PaidPayments < plan.PaymentCount)
                plan.NextDueDate = plan.Frequency == "Every15Days"
                    ? plan.StartDate.AddDays(plan.PaidPayments * 15)
                    : plan.StartDate.AddMonths(plan.PaidPayments);
        }
        else db.Transactions.Remove(transaction);
    }
    else
    {
        db.Transactions.Remove(transaction);
        if (transaction.Kind == "Income" && transaction.Category.Equals("Salary", StringComparison.OrdinalIgnoreCase))
        {
            var schedule = await db.SalarySchedules.SingleAsync();
            if (schedule.Frequency == "Every15Days")
            {
                var latestSalary = await db.Transactions
                    .Where(item => item.Id != id && item.Kind == "Income" && item.Category == "Salary")
                    .OrderByDescending(item => item.Date).ThenByDescending(item => item.Id)
                    .FirstOrDefaultAsync();
                var nextPayDate = (latestSalary?.Date ?? transaction.Date).AddDays(15);
                while (nextPayDate < DateTime.Today) nextPayDate = nextPayDate.AddDays(15);
                schedule.NextPayDate = nextPayDate;
            }
        }
    }

    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapGet("/api/installments", async (MoneyDb db) => Results.Ok(
    (await db.Installments.OrderBy(i => i.NextDueDate).ToListAsync()).Select(ApiShape.Installment)));

app.MapPost("/api/installments", async (InstallmentInput input, MoneyDb db) =>
{
    if (string.IsNullOrWhiteSpace(input.Description) || string.IsNullOrWhiteSpace(input.Category)) return Results.BadRequest("Add a description and category.");
    if (input.TotalAmount <= 0 || input.TotalAmount > 1_000_000_000) return Results.BadRequest("Enter a valid purchase amount.");
    if (input.TermMonths is < 1 or > 120) return Results.BadRequest("Payment term must be between 1 and 120 months.");
    if (input.Frequency is not ("Monthly" or "Every15Days")) return Results.BadRequest("Choose monthly or every 15 days.");
    if (input.StartDate == default) return Results.BadRequest("Enter a first payment date.");
    if (input.StartDate.Date < new DateTime(2026, 11, 1)) return Results.BadRequest("Tracking starts in November 2026.");
    var plan = new InstallmentPlan
    {
        Description = input.Description.Trim(),
        Category = input.Category.Trim(),
        NeedType = input.NeedType == "Want" ? "Want" : "Need",
        TotalAmount = decimal.Round(input.TotalAmount, 2),
        TermMonths = input.TermMonths,
        PaidPayments = 0,
        Frequency = input.Frequency,
        StartDate = input.StartDate.Date,
        NextDueDate = input.StartDate.Date
    };
    db.Installments.Add(plan);
    await db.SaveChangesAsync();
    return Results.Created($"/api/installments/{plan.Id}", ApiShape.Installment(plan));
});

app.MapPost("/api/installments/{id:int}/payments", async (int id, PaymentInput input, MoneyDb db) =>
{
    var plan = await db.Installments.FindAsync(id);
    if (plan is null) return Results.NotFound("Payment plan not found.");
    if (plan.PaidPayments >= plan.PaymentCount) return Results.BadRequest("This payment plan is already paid off.");
    if (input.Date == default) return Results.BadRequest("Enter a payment date.");
    if (input.Date.Date < new DateTime(2026, 11, 1)) return Results.BadRequest("Tracking starts in November 2026.");
    var amount = plan.PaidPayments == plan.PaymentCount - 1
        ? plan.TotalAmount - plan.PaymentAmount * (plan.PaymentCount - 1)
        : plan.PaymentAmount;
    plan.PaidPayments++;
    plan.NextDueDate = plan.PaidPayments < plan.PaymentCount
        ? plan.Frequency == "Every15Days" ? plan.StartDate.AddDays(plan.PaidPayments * 15) : plan.StartDate.AddMonths(plan.PaidPayments)
        : plan.NextDueDate;
    db.Transactions.Add(new MoneyTransaction
    {
        Kind = "Expense", Amount = amount, Description = $"{plan.Description} · payment {plan.PaidPayments}",
        Category = plan.Category, NeedType = "Installment", Date = input.Date.Date, InstallmentPlanId = plan.Id
    });
    await db.SaveChangesAsync();
    return Results.Ok(ApiShape.Installment(plan));
});

app.MapGet("/api/dashboard", async (string? month, MoneyDb db) =>
{
    var firstTrackedMonth = new DateTime(2026, 11, 1);
    if (!DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var selectedMonth))
        selectedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    else selectedMonth = new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
    if (selectedMonth < firstTrackedMonth) selectedMonth = firstTrackedMonth;
    var allTransactions = (await db.Transactions.AsNoTracking().OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).ToListAsync())
        .Where(t => t.Date >= firstTrackedMonth).ToList();
    var plans = (await db.Installments.AsNoTracking().OrderBy(i => i.NextDueDate).ToListAsync())
        .Where(i => i.PaidPayments < i.PaymentCount).ToList();
    var settings = await db.Settings.AsNoTracking().SingleAsync();
    var selectedTransactions = allTransactions.Where(t => t.Date.Year == selectedMonth.Year && t.Date.Month == selectedMonth.Month).ToList();
    var income = selectedTransactions.Where(t => t.Kind == "Income").Sum(t => t.Amount);
    var expenses = selectedTransactions.Where(t => t.Kind is "Expense" or "CashExpense").ToList();
    var needs = expenses.Where(t => t.NeedType == "Need").Sum(t => t.Amount);
    var wants = expenses.Where(t => t.NeedType == "Want").Sum(t => t.Amount);
    var installmentSpend = expenses.Where(t => t.NeedType == "Installment").Sum(t => t.Amount);
    var selectedEnd = selectedMonth.AddMonths(1);
    var commitments = installmentSpend + plans.Sum(plan =>
    {
        var due = plan.NextDueDate.Date;
        var count = 0;
        while (due < selectedEnd && count < plan.PaymentCount - plan.PaidPayments)
        {
            if (due >= selectedMonth) count++;
            due = plan.Frequency == "Every15Days" ? due.AddDays(15) : due.AddMonths(1);
        }
        return plan.PaymentAmount * count;
    });
    var bankBalance = settings.OpeningCapital + allTransactions.Sum(t => t.Kind switch
    {
        "Income" => t.Amount,
        "Expense" or "Withdrawal" => -t.Amount,
        _ => 0m
    });
    var cashOnHand = allTransactions.Where(t => t.Kind == "Withdrawal").Sum(t => t.Amount) - allTransactions.Where(t => t.Kind == "CashExpense").Sum(t => t.Amount);
    var totalBalance = bankBalance + cashOnHand;
    var start = selectedMonth.AddMonths(-5);
    if (start < firstTrackedMonth) start = firstTrackedMonth;
    var trendCount = (selectedMonth.Year - start.Year) * 12 + selectedMonth.Month - start.Month + 1;
    var trend = Enumerable.Range(0, trendCount).Select(offset =>
    {
        var monthDate = start.AddMonths(offset);
        var monthTransactions = allTransactions.Where(t => t.Date.Year == monthDate.Year && t.Date.Month == monthDate.Month).ToList();
        return new
        {
            month = monthDate.ToString("MMM", CultureInfo.InvariantCulture),
            income = monthTransactions.Where(t => t.Kind == "Income").Sum(t => t.Amount),
            needs = monthTransactions.Where(t => (t.Kind is "Expense" or "CashExpense") && t.NeedType == "Need").Sum(t => t.Amount),
            wants = monthTransactions.Where(t => (t.Kind is "Expense" or "CashExpense") && t.NeedType == "Want").Sum(t => t.Amount),
            installments = monthTransactions.Where(t => (t.Kind is "Expense" or "CashExpense") && t.NeedType == "Installment").Sum(t => t.Amount)
        };
    }).ToList();
    var categoryTotals = expenses.GroupBy(t => new { t.Category, t.NeedType })
        .Select(group => new { category = group.Key.Category, needType = group.Key.NeedType!, amount = group.Sum(t => t.Amount) })
        .OrderByDescending(group => group.amount).ToList();
    var salarySchedule = await db.SalarySchedules.AsNoTracking().SingleAsync();
    return Results.Ok(new
    {
        openingCapital = settings.OpeningCapital,
        bankBalance,
        cashOnHand,
        totalBalance,
        monthIncome = income,
        monthExpenses = expenses.Sum(t => t.Amount),
        monthNeeds = needs,
        monthWants = wants,
        monthInstallments = commitments,
        monthLabel = selectedMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
        salaryFrequency = salarySchedule.Frequency,
        nextPayDate = salarySchedule.NextPayDate?.ToString("yyyy-MM-dd"),
        monthlyTrend = trend,
        categoryBreakdown = categoryTotals,
        recentTransactions = allTransactions.Select(ApiShape.Transaction),
        upcomingInstallments = plans.Select(ApiShape.Installment)
    });
});

app.Run("http://localhost:5055");

static string? ValidateTransaction(TransactionInput input)
{
    if (input.Kind is not ("Income" or "Expense" or "Withdrawal" or "CashExpense")) return "Choose a valid entry type.";
    if (input.Amount <= 0 || input.Amount > 1_000_000_000) return "Enter a valid amount.";
    if (string.IsNullOrWhiteSpace(input.Description) || (input.Kind is "Expense" or "CashExpense") && string.IsNullOrWhiteSpace(input.Category)) return "Add a description and category.";
    if (input.Date.Date < new DateTime(2026, 11, 1)) return "Tracking starts in November 2026.";
    if (input.Date == default || input.Date > DateTime.Today.AddYears(2)) return "Enter a valid date.";
    return null;
}
