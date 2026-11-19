using Microsoft.EntityFrameworkCore;

namespace Kwarta.Api;

public sealed class MoneyDb(DbContextOptions<MoneyDb> options) : DbContext(options)
{
    public DbSet<MoneyTransaction> Transactions => Set<MoneyTransaction>();
    public DbSet<InstallmentPlan> Installments => Set<InstallmentPlan>();
    public DbSet<MoneySettings> Settings => Set<MoneySettings>();
    public DbSet<SalarySchedule> SalarySchedules => Set<SalarySchedule>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<MoneySettings>().HasKey(x => x.Id);
        model.Entity<MoneySettings>().Property(x => x.OpeningCapital).HasPrecision(12, 2);
        model.Entity<SalarySchedule>().HasKey(x => x.Id);
        model.Entity<MoneyTransaction>().Property(x => x.Amount).HasPrecision(12, 2);
        model.Entity<MoneyTransaction>().Property(x => x.Kind).HasMaxLength(16);
        model.Entity<MoneyTransaction>().Property(x => x.Description).HasMaxLength(160);
        model.Entity<MoneyTransaction>().Property(x => x.Category).HasMaxLength(80);
        model.Entity<MoneyTransaction>().Property(x => x.NeedType).HasMaxLength(16);
        model.Entity<InstallmentPlan>().Property(x => x.TotalAmount).HasPrecision(12, 2);
        model.Entity<InstallmentPlan>().Property(x => x.Description).HasMaxLength(160);
        model.Entity<InstallmentPlan>().Property(x => x.Category).HasMaxLength(80);
        model.Entity<InstallmentPlan>().Property(x => x.NeedType).HasMaxLength(16);
        model.Entity<InstallmentPlan>().Property(x => x.Frequency).HasMaxLength(16);
        model.Entity<SalarySchedule>().Property(x => x.Frequency).HasMaxLength(16);
        model.Entity<MoneyTransaction>().HasOne<InstallmentPlan>().WithMany().HasForeignKey(x => x.InstallmentPlanId).OnDelete(DeleteBehavior.SetNull);
        model.Entity<MoneyTransaction>().HasOne<MoneyTransaction>().WithMany().HasForeignKey(x => x.WithdrawalId).OnDelete(DeleteBehavior.SetNull);
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
    public int? WithdrawalId { get; set; }
}

public sealed class InstallmentPlan
{
    public int Id { get; set; }
    public string Description { get; set; } = "";
    public string Category { get; set; } = "Other";
    public string NeedType { get; set; } = "Need";
    public decimal TotalAmount { get; set; }
    public int TermMonths { get; set; }
    public int PaidPayments { get; set; }
    public string Frequency { get; set; } = "Monthly";
    public DateTime StartDate { get; set; }
    public DateTime NextDueDate { get; set; }
    public int PaymentCount => TermMonths * (Frequency == "Every15Days" ? 2 : 1);
    public int IntervalDays => Frequency == "Every15Days" ? 15 : 0;
    public decimal PaymentAmount => decimal.Round(TotalAmount / PaymentCount, 2);
    public decimal RemainingAmount => Math.Max(0, TotalAmount - (PaidPayments >= PaymentCount
        ? TotalAmount
        : Math.Min(TotalAmount, PaymentAmount * PaidPayments)));
}

public sealed class SalarySchedule
{
    public int Id { get; set; } = 1;
    public string Frequency { get; set; } = "Every15Days";
    public DateTime? NextPayDate { get; set; }
}

public sealed record TransactionInput(string Kind, decimal Amount, string Description, string Category, string? NeedType, DateTime Date, int? WithdrawalId = null);
public sealed record InstallmentInput(string Description, string Category, string? NeedType, decimal TotalAmount, int TermMonths, string Frequency, DateTime StartDate);
public sealed record PaymentInput(DateTime Date);
public sealed record CapitalInput(decimal Amount);
public sealed record SalaryScheduleInput(string Frequency, DateTime? StartDate);

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
        withdrawalId = transaction.WithdrawalId,
        date = transaction.Date.ToString("yyyy-MM-dd")
    };

    public static object Installment(InstallmentPlan plan) => new
    {
        id = plan.Id,
        description = plan.Description,
        category = plan.Category,
        needType = plan.NeedType,
        totalAmount = plan.TotalAmount,
        paymentAmount = plan.PaymentAmount,
        frequency = plan.Frequency,
        termMonths = plan.TermMonths,
        paymentCount = plan.PaymentCount,
        paidPayments = plan.PaidPayments,
        startDate = plan.StartDate.ToString("yyyy-MM-dd"),
        nextDueDate = plan.NextDueDate.ToString("yyyy-MM-dd"),
        remainingAmount = plan.RemainingAmount
    };
}

public static class SeedData
{
    public static async Task Initialize(MoneyDb db)
    {
        if (!await db.Settings.AnyAsync()) db.Settings.Add(new MoneySettings { OpeningCapital = 0m });
        if (!await db.SalarySchedules.AnyAsync()) db.SalarySchedules.Add(new SalarySchedule { Frequency = "Manual", NextPayDate = null });
        await db.SaveChangesAsync();
    }
}
