using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Spendly.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecurringExpenseId",
                table: "Expenses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RecurringExpenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NextDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringExpenses", x => x.Id);
                    table.CheckConstraint("CK_RecurringExpenses_Amount_Positive", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_RecurringExpenses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecurringExpenses_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_RecurringExpenseId_SpentAt",
                table: "Expenses",
                columns: new[] { "RecurringExpenseId", "SpentAt" },
                unique: true,
                filter: "[RecurringExpenseId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringExpenses_CategoryId",
                table: "RecurringExpenses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringExpenses_UserId_IsActive_NextDueDate",
                table: "RecurringExpenses",
                columns: new[] { "UserId", "IsActive", "NextDueDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_RecurringExpenses_RecurringExpenseId",
                table: "Expenses",
                column: "RecurringExpenseId",
                principalTable: "RecurringExpenses",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_RecurringExpenses_RecurringExpenseId",
                table: "Expenses");

            migrationBuilder.DropTable(
                name: "RecurringExpenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_RecurringExpenseId_SpentAt",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "RecurringExpenseId",
                table: "Expenses");
        }
    }
}
