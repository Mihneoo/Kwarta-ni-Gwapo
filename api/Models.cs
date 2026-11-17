using Microsoft.EntityFrameworkCore;

namespace Kwarta.Api;

public sealed class MoneyDb(DbContextOptions<MoneyDb> options) : DbContext(options)
{
    public DbSet<MoneyTransaction> Transactions => Set<MoneyTransaction>();
    public DbSet<InstallmentPlan> Installments => Set<InstallmentPlan>();
    public DbSet<MoneySettings> Settings => Set<MoneySettings>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<MoneySettings>().HasKey(x => x.Id);
        model.Entity<MoneySettings>().Property(x => x.OpeningCapital).HasPrecision(12, 2);
        model.Entity<MoneyTransaction>().Property(x => x.Amount).HasPrecision(12, 2);
        model.Entity<MoneyTransaction>().Property(x => x.Kind).HasMaxLength(16);
        model.Entity<MoneyTransaction>().Property(x => x.Description).HasMaxLength(160);
        model.Entity<MoneyTransaction>().Property(x => x.Category).HasMaxLength(80);
        model.Entity<MoneyTransaction>().Property(x => x.NeedType).HasMaxLength(16);
        model.Entity<InstallmentPlan>().Property(x => x.TotalAmount).HasPrecision(12, 2);
        model.Entity<InstallmentPlan>().Property(x => x.Description).HasMaxLength(160);
        model.Entity<InstallmentPlan>().Property(x => x.Category).HasMaxLength(80);
        model.Entity<InstallmentPlan>().Property(x => x.NeedType).HasMaxLength(16);
        model.Entity<MoneyTransaction>().HasOne<InstallmentPlan>().WithMany().HasForeignKey(x => x.InstallmentPlanId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class MoneySettings
{
    public int Id { get; set; } = 1;
    public decimal OpeningCapital { get; set; }
}

public sealed class MoneyTransaction
{
    public int Id { get; set; }
    public string Kind { get; set; } = "Expense";
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public string Category { get; set; } = "Other";
    public string? NeedType { get; set; }
    public DateTime Date { get; set; }
    public int? InstallmentPlanId { get; set; }
}

public sealed class InstallmentPlan
{
    public int Id { get; set; }
    public string Description { get; set; } = "";
    public string Category { get; set; } = "Other";
    public string NeedType { get; set; } = "Need";
    public decimal TotalAmount { get; set; }
    public int TermMonths { get; set; }
    public int PaidMonths { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime NextDueDate { get; set; }
    public decimal MonthlyAmount => decimal.Round(TotalAmount / TermMonths, 2);
    public decimal RemainingAmount => Math.Max(0, TotalAmount - (PaidMonths == TermMonths
        ? TotalAmount
        : Math.Min(TotalAmount, decimal.Round(TotalAmount / TermMonths, 2) * PaidMonths)));
}

public sealed record TransactionInput(string Kind, decimal Amount, string Description, string Category, string? NeedType, DateTime Date);
public sealed record InstallmentInput(string Description, string Category, string? NeedType, decimal TotalAmount, int TermMonths, DateTime StartDate);
public sealed record PaymentInput(DateTime Date);
public sealed record CapitalInput(decimal Amount);

public static class ApiShape
{
    public static object Transaction(MoneyTransaction transaction) => new
    {
        id = transaction.Id,
        kind = transaction.Kind,
        amount = transaction.Amount,
        description = transaction.Description,
        category = transaction.Category,
        needType = transaction.NeedType,
        date = transaction.Date.ToString("yyyy-MM-dd")
    };

    public static object Installment(InstallmentPlan plan) => new
    {
        id = plan.Id,
        description = plan.Description,
        category = plan.Category,
        needType = plan.NeedType,
        totalAmount = plan.TotalAmount,
        monthlyAmount = plan.MonthlyAmount,
        termMonths = plan.TermMonths,
        paidMonths = plan.PaidMonths,
        startDate = plan.StartDate.ToString("yyyy-MM-dd"),
        nextDueDate = plan.NextDueDate.ToString("yyyy-MM-dd"),
        remainingAmount = plan.RemainingAmount
    };
}

public static class SeedData
{
    public static async Task Initialize(MoneyDb db)
    {
        if (!await db.Settings.AnyAsync()) db.Settings.Add(new MoneySettings { OpeningCapital = 85000m });
        if (await db.Transactions.AnyAsync())
        {
            await db.SaveChangesAsync();
            return;
        }

        var today = DateTime.Today;
        var firstDay = new DateTime(today.Year, today.Month, 1);
        db.Transactions.AddRange(
            new MoneyTransaction { Kind = "Income", Amount = 62000m, Description = "Monthly paycheck", Category = "Salary", Date = firstDay.AddDays(Math.Min(4, DateTime.DaysInMonth(today.Year, today.Month) - 1)) },
            new MoneyTransaction { Kind = "Expense", Amount = 18500m, Description = "Rent & utilities", Category = "Housing & bills", NeedType = "Need", Date = firstDay.AddDays(1) },
            new MoneyTransaction { Kind = "Expense", Amount = 6420m, Description = "Weekly groceries", Category = "Food & dining", NeedType = "Need", Date = firstDay.AddDays(3) },
            new MoneyTransaction { Kind = "Expense", Amount = 1850m, Description = "Grab rides", Category = "Transport", NeedType = "Need", Date = firstDay.AddDays(5) },
            new MoneyTransaction { Kind = "Expense", Amount = 2480m, Description = "Dinner with friends", Category = "Food & dining", NeedType = "Want", Date = firstDay.AddDays(8) },
            new MoneyTransaction { Kind = "Expense", Amount = 3190m, Description = "Weekend shopping", Category = "Shopping", NeedType = "Want", Date = firstDay.AddDays(11) }
        );
        for (var offset = 5; offset >= 0; offset--)
        {
            var month = firstDay.AddMonths(-offset);
            if (month.Year == today.Year && month.Month == today.Month) continue;
            db.Transactions.AddRange(
                new MoneyTransaction { Kind = "Income", Amount = 62000m, Description = "Monthly paycheck", Category = "Salary", Date = month.AddDays(4) },
                new MoneyTransaction { Kind = "Expense", Amount = 23000m, Description = "Home & groceries", Category = "Housing & bills", NeedType = "Need", Date = month.AddDays(8) },
                new MoneyTransaction { Kind = "Expense", Amount = 5200m + ((offset * 930) % 3200), Description = "Everyday spending", Category = "Food & dining", NeedType = "Need", Date = month.AddDays(14) },
                new MoneyTransaction { Kind = "Expense", Amount = 2800m + ((offset * 410) % 1400), Description = "Personal treats", Category = "Shopping", NeedType = "Want", Date = month.AddDays(19) }
            );
        }
        db.Installments.Add(new InstallmentPlan
        {
            Description = "Laptop", Category = "Electronics", NeedType = "Need", TotalAmount = 54000m,
            TermMonths = 12, PaidMonths = 4, StartDate = firstDay.AddMonths(-4), NextDueDate = firstDay
        });
        await db.SaveChangesAsync();
        // Seed four historical payments so the initial balance and chart match the plan's progress.
        var laptop = db.Installments.Local.Single();
        for (var payment = 1; payment <= laptop.PaidMonths; payment++)
        {
            var amount = payment == laptop.TermMonths ? laptop.TotalAmount - decimal.Round(laptop.TotalAmount / laptop.TermMonths, 2) * (laptop.TermMonths - 1) : laptop.MonthlyAmount;
            db.Transactions.Add(new MoneyTransaction { Kind = "Expense", Amount = amount, Description = $"Laptop · payment {payment}", Category = "Electronics", NeedType = "Installment", Date = laptop.StartDate.AddMonths(payment - 1), InstallmentPlanId = laptop.Id });
        }
        await db.SaveChangesAsync();
    }
}
