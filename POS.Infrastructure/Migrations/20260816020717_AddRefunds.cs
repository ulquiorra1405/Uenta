using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    CashSessionId = table.Column<long>(type: "INTEGER", nullable: true),
                    OriginalSaleId = table.Column<long>(type: "INTEGER", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Refunds_Sales_OriginalSaleId",
                        column: x => x.OriginalSaleId,
                        principalTable: "Sales",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Refunds_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefundItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RefundId = table.Column<long>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<long>(type: "INTEGER", nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefundItems_Refunds_RefundId",
                        column: x => x.RefundId,
                        principalTable: "Refunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefundPayments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RefundId = table.Column<long>(type: "INTEGER", nullable: false),
                    Method = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefundPayments_Refunds_RefundId",
                        column: x => x.RefundId,
                        principalTable: "Refunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefundItems_RefundId",
                table: "RefundItems",
                column: "RefundId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundPayments_RefundId",
                table: "RefundPayments",
                column: "RefundId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_Number",
                table: "Refunds",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_OriginalSaleId",
                table: "Refunds",
                column: "OriginalSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_UserId",
                table: "Refunds",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefundItems");

            migrationBuilder.DropTable(
                name: "RefundPayments");

            migrationBuilder.DropTable(
                name: "Refunds");
        }
    }
}
