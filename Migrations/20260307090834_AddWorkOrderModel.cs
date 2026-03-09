using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotTwo.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductionLineId = table.Column<int>(type: "INTEGER", nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EstimatedEndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrders_ProductionLines_ProductionLineId",
                        column: x => x.ProductionLineId,
                        principalTable: "ProductionLines",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrders_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionLines_CurrentWorkOrderId",
                table: "ProductionLines",
                column: "CurrentWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_ProductId",
                table: "WorkOrders",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_ProductionLineId",
                table: "WorkOrders",
                column: "ProductionLineId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionLines_WorkOrders_CurrentWorkOrderId",
                table: "ProductionLines",
                column: "CurrentWorkOrderId",
                principalTable: "WorkOrders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionLines_WorkOrders_CurrentWorkOrderId",
                table: "ProductionLines");

            migrationBuilder.DropTable(
                name: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionLines_CurrentWorkOrderId",
                table: "ProductionLines");
        }
    }
}
