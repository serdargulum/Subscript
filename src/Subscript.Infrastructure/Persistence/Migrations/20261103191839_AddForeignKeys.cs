using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Subscript.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_PendingPlanId",
                table: "subscriptions",
                column: "PendingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_PlanId",
                table: "subscriptions",
                column: "PlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_invoices_subscriptions_SubscriptionId",
                table: "invoices",
                column: "SubscriptionId",
                principalTable: "subscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_invoices_InvoiceId",
                table: "payments",
                column: "InvoiceId",
                principalTable: "invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_customers_CustomerId",
                table: "subscriptions",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_plans_PendingPlanId",
                table: "subscriptions",
                column: "PendingPlanId",
                principalTable: "plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_plans_PlanId",
                table: "subscriptions",
                column: "PlanId",
                principalTable: "plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invoices_subscriptions_SubscriptionId",
                table: "invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_invoices_InvoiceId",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "FK_subscriptions_customers_CustomerId",
                table: "subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_subscriptions_plans_PendingPlanId",
                table: "subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_subscriptions_plans_PlanId",
                table: "subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_subscriptions_PendingPlanId",
                table: "subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_subscriptions_PlanId",
                table: "subscriptions");
        }
    }
}
