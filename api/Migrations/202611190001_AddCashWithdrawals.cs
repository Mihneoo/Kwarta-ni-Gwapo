using Kwarta.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kwarta.Api.Migrations;

[DbContext(typeof(MoneyDb))]
[Migration("202611190001_AddCashWithdrawals")]
public sealed class AddCashWithdrawals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<int>(name: "WithdrawalId", table: "Transactions", type: "INTEGER", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "WithdrawalId", table: "Transactions");

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.16");
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
            entity.Property<int?>("WithdrawalId").HasColumnType("INTEGER");
            entity.HasKey("Id");
            entity.HasIndex("InstallmentPlanId");
            entity.HasIndex("WithdrawalId");
            entity.ToTable("Transactions");
        });
    }
}
