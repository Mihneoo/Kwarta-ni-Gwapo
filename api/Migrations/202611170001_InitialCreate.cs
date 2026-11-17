using Kwarta.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kwarta.Api.Migrations;

[DbContext(typeof(MoneyDb))]
[Migration("202611170001_InitialCreate")]
public sealed class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Installments",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Description = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                NeedType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                TotalAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                TermMonths = table.Column<int>(type: "INTEGER", nullable: false),
                PaidMonths = table.Column<int>(type: "INTEGER", nullable: false),
                StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                NextDueDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Installments", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Settings",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false),
                OpeningCapital = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Settings", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Transactions",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                Amount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                NeedType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                InstallmentPlanId = table.Column<int>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Transactions", x => x.Id);
                table.ForeignKey("FK_Transactions_Installments_InstallmentPlanId", x => x.InstallmentPlanId, "Installments", "Id", onDelete: ReferentialAction.SetNull);
            });
        migrationBuilder.CreateIndex("IX_Transactions_InstallmentPlanId", "Transactions", "InstallmentPlanId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Settings");
        migrationBuilder.DropTable("Transactions");
        migrationBuilder.DropTable("Installments");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.16");
        modelBuilder.Entity("Kwarta.Api.InstallmentPlan", entity =>
        {
            entity.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER").HasAnnotation("Sqlite:Autoincrement", true);
            entity.Property<string>("Category").IsRequired().HasMaxLength(80).HasColumnType("TEXT");
            entity.Property<string>("Description").IsRequired().HasMaxLength(160).HasColumnType("TEXT");
            entity.Property<string>("NeedType").IsRequired().HasMaxLength(16).HasColumnType("TEXT");
            entity.Property<DateTime>("NextDueDate").HasColumnType("TEXT");
            entity.Property<int>("PaidMonths").HasColumnType("INTEGER");
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
