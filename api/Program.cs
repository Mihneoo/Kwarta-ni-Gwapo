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

app.MapGet("/api/transactions", async (string? from, string? to, MoneyDb db) =>
{
    var query = db.Transactions.AsQueryable();
    if (DateTime.TryParse(from, CultureInfo.InvariantCulture, out var fromDate)) query = query.Where(t => t.Date >= fromDate.Date);
    if (DateTime.TryParse(to, CultureInfo.InvariantCulture, out var toDate)) query = query.Where(t => t.Date < toDate.Date.AddDays(1));
    var list = await query.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).ToListAsync();
    return Results.Ok(list.Select(ApiShape.Transaction));
});

app.MapPost("/api/transactions", async (TransactionInput input, MoneyDb db) =>
{
    var validation = ValidateTransaction(input);
    if (validation is not null) return Results.BadRequest(validation);
    var transaction = new MoneyTransaction
    {
        Kind = input.Kind,
        Amount = decimal.Round(input.Amount, 2),
        Description = input.Description.Trim(),
        Category = input.Category.Trim(),
        NeedType = input.Kind == "Income" ? null : (input.NeedType == "Want" ? "Want" : "Need"),
        Date = input.Date.Date
    };
    db.Transactions.Add(transaction);
    await db.SaveChangesAsync();
    return Results.Created($"/api/transactions/{transaction.Id}", ApiShape.Transaction(transaction));
});

app.MapGet("/api/installments", async (MoneyDb db) => Results.Ok(
    (await db.Installments.OrderBy(i => i.NextDueDate).ToListAsync()).Select(ApiShape.Installment)));

app.MapPost("/api/installments", async (InstallmentInput input, MoneyDb db) =>
{
    if (string.IsNullOrWhiteSpace(input.Description) || string.IsNullOrWhiteSpace(input.Category)) return Results.BadRequest("Add a description and category.");
    if (input.TotalAmount <= 0 || input.TotalAmount > 1_000_000_000) return Results.BadRequest("Enter a valid purchase amount.");
    if (input.TermMonths is < 1 or > 120) return Results.BadRequest("Payment term must be between 1 and 120 months.");
    if (input.StartDate == default) return Results.BadRequest("Enter a first payment date.");
    var plan = new InstallmentPlan
    {
        Description = input.Description.Trim(),
        Category = input.Category.Trim(),
        NeedType = input.NeedType == "Want" ? "Want" : "Need",
        TotalAmount = decimal.Round(input.TotalAmount, 2),
        TermMonths = input.TermMonths,
        PaidMonths = 0,
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
    if (plan.PaidMonths >= plan.TermMonths) return Results.BadRequest("This payment plan is already paid off.");
    if (input.Date == default) return Results.BadRequest("Enter a payment date.");
    var amount = plan.PaidMonths == plan.TermMonths - 1
        ? plan.TotalAmount - decimal.Round(plan.TotalAmount / plan.TermMonths, 2) * (plan.TermMonths - 1)
        : decimal.Round(plan.TotalAmount / plan.TermMonths, 2);
    plan.PaidMonths++;
    plan.NextDueDate = plan.PaidMonths < plan.TermMonths ? plan.StartDate.AddMonths(plan.PaidMonths) : plan.NextDueDate;
    db.Transactions.Add(new MoneyTransaction
    {
        Kind = "Expense", Amount = amount, Description = $"{plan.Description} · payment {plan.PaidMonths}",
        Category = plan.Category, NeedType = "Installment", Date = input.Date.Date, InstallmentPlanId = plan.Id
    });
    await db.SaveChangesAsync();
    return Results.Ok(ApiShape.Installment(plan));
});

app.MapGet("/api/dashboard", async (string? month, MoneyDb db) =>
{
    if (!DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var selectedMonth))
        selectedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    else selectedMonth = new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
    var allTransactions = await db.Transactions.AsNoTracking().OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).ToListAsync();
    var plans = await db.Installments.AsNoTracking().Where(i => i.PaidMonths < i.TermMonths).OrderBy(i => i.NextDueDate).ToListAsync();
    var settings = await db.Settings.AsNoTracking().SingleAsync();
    var selectedTransactions = allTransactions.Where(t => t.Date.Year == selectedMonth.Year && t.Date.Month == selectedMonth.Month).ToList();
    var income = selectedTransactions.Where(t => t.Kind == "Income").Sum(t => t.Amount);
    var expenses = selectedTransactions.Where(t => t.Kind == "Expense").ToList();
    var needs = expenses.Where(t => t.NeedType == "Need").Sum(t => t.Amount);
    var wants = expenses.Where(t => t.NeedType == "Want").Sum(t => t.Amount);
    var installmentSpend = expenses.Where(t => t.NeedType == "Installment").Sum(t => t.Amount);
    var commitments = installmentSpend + plans
        .Where(plan => plan.NextDueDate.Year == selectedMonth.Year && plan.NextDueDate.Month == selectedMonth.Month)
        .Sum(plan => plan.MonthlyAmount);
    var balance = settings.OpeningCapital + allTransactions.Sum(t => t.Kind == "Income" ? t.Amount : -t.Amount);
    var start = selectedMonth.AddMonths(-5);
    var trend = Enumerable.Range(0, 6).Select(offset =>
    {
        var monthDate = start.AddMonths(offset);
        var monthTransactions = allTransactions.Where(t => t.Date.Year == monthDate.Year && t.Date.Month == monthDate.Month).ToList();
        return new
        {
            month = monthDate.ToString("MMM", CultureInfo.InvariantCulture),
            income = monthTransactions.Where(t => t.Kind == "Income").Sum(t => t.Amount),
            needs = monthTransactions.Where(t => t.Kind == "Expense" && t.NeedType == "Need").Sum(t => t.Amount),
            wants = monthTransactions.Where(t => t.Kind == "Expense" && t.NeedType == "Want").Sum(t => t.Amount),
            installments = monthTransactions.Where(t => t.Kind == "Expense" && t.NeedType == "Installment").Sum(t => t.Amount)
        };
    }).ToList();
    var categoryTotals = expenses.GroupBy(t => new { t.Category, t.NeedType })
        .Select(group => new { category = group.Key.Category, needType = group.Key.NeedType!, amount = group.Sum(t => t.Amount) })
        .OrderByDescending(group => group.amount).ToList();
    return Results.Ok(new
    {
        openingCapital = settings.OpeningCapital,
        currentBalance = balance,
        monthIncome = income,
        monthExpenses = expenses.Sum(t => t.Amount),
        monthNeeds = needs,
        monthWants = wants,
        monthInstallments = commitments,
        monthLabel = selectedMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
        monthlyTrend = trend,
        categoryBreakdown = categoryTotals,
        recentTransactions = allTransactions.Take(8).Select(ApiShape.Transaction),
        upcomingInstallments = plans.Select(ApiShape.Installment)
    });
});

app.Run("http://localhost:5055");

static string? ValidateTransaction(TransactionInput input)
{
    if (input.Kind is not ("Income" or "Expense")) return "Entry type must be Income or Expense.";
    if (input.Amount <= 0 || input.Amount > 1_000_000_000) return "Enter a valid amount.";
    if (string.IsNullOrWhiteSpace(input.Description) || string.IsNullOrWhiteSpace(input.Category)) return "Add a description and category.";
    if (input.Date == default || input.Date > DateTime.Today.AddYears(2)) return "Enter a valid date.";
    return null;
}
