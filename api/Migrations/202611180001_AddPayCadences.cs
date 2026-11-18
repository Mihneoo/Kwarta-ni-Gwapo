using Kwarta.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kwarta.Api.Migrations;

[DbContext(typeof(MoneyDb))]
[Migration("202611180001_AddPayCadences")]
public sealed class AddPayCadences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(name: "PaidMonths", table: "Installments", newName: "PaidPayments");
        migrationBuilder.AddColumn<string>(name: "Frequency", table: "Installments", type: "TEXT", maxLength: 16, nullable: false, defaultValue: "Monthly");
        migrationBuilder.CreateTable(
            name: "SalarySchedules",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false),
                Frequency = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                NextPayDate = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_SalarySchedules", x => x.Id));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("SalarySchedules");
        migrationBuilder.DropColumn(name: "Frequency", table: "Installments");
        migrationBuilder.RenameColumn(name: "PaidPayments", table: "Installments", newName: "PaidMonths");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.16");
        modelBuilder.Entity("Kwarta.Api.InstallmentPlan", entity =>
        {
            entity.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER").HasAnnotation("Sqlite:Autoincrement", true);
            entity.Property<string>("Category").IsRequired().HasMaxLength(80).HasColumnType("TEXT");
            entity.Property<string>("Description").IsRequired().HasMaxLength(160).HasColumnType("TEXT");
            entity.Property<string>("Frequency").IsRequired().HasMaxLength(16).HasColumnType("TEXT");
            entity.Property<string>("NeedType").IsRequired().HasMaxLength(16).HasColumnType("TEXT");
            entity.Property<DateTime>("NextDueDate").HasColumnType("TEXT");
            entity.Property<int>("PaidPayments").HasColumnType("INTEGER");
            entity.Property<DateTime>("StartDate").HasColumnType("TEXT");
            entity.Property<int>("TermMonths").HasColumnType("INTEGER");
            entity.Property<decimal>("TotalAmount").HasPrecision(12, 2).HasColumnType("TEXT");
            entity.HasKey("Id");
            entity.ToTable("Installments");
        });
        modelBuilder.Entity("Kwarta.Api.MoneySettings", entity =>
        {
            entity.Property<int>("Id").HasColumnType("INTEGER");
            entity.Property<decimal>("OpeningCapital").HasPrecision(12, 2).HasColumnType("TEXT");
            entity.HasKey("Id");
            entity.ToTable("Settings");
        });
        modelBuilder.Entity("Kwarta.Api.SalarySchedule", entity =>
        {
            entity.Property<int>("Id").HasColumnType("INTEGER");
            entity.Property<string>("Frequency").IsRequired().HasMaxLength(16).HasColumnType("TEXT");
            entity.Property<DateTime?>("NextPayDate").HasColumnType("TEXT");
            entity.HasKey("Id");
            entity.ToTable("SalarySchedules");
        });
        modelBuilder.Entity("Kwarta.Api.MoneyTransaction", entity =>
        {
            entity.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER").HasAnnotation("Sqlite:Autoincrement", true);
            entity.Property<decimal>("Amount").HasPrecision(12, 2).HasColumnType("TEXT");
            entity.Property<string>("Category").IsRequired().HasMaxLength(80).HasColumnType("TEXT");
            entity.Property<DateTime>("Date").HasColumnType("TEXT");
            entity.Property<string>("Description").IsRequired().HasMaxLength(160).HasColumnType("TEXT");
            entity.Property<int?>("InstallmentPlanId").HasColumnType("INTEGER");
            entity.Property<string>("Kind").IsRequired().HasMaxLength(16).HasColumnType("TEXT");
            entity.Property<string>("NeedType").HasMaxLength(16).HasColumnType("TEXT");
            entity.HasKey("Id");
            entity.HasIndex("InstallmentPlanId");
            entity.ToTable("Transactions");
        });
        modelBuilder.Entity("Kwarta.Api.MoneyTransaction", entity =>
        {
            entity.HasOne("Kwarta.Api.InstallmentPlan", null).WithMany().HasForeignKey("InstallmentPlanId").OnDelete(DeleteBehavior.SetNull);
        });
    }
}
