using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentGatewaySupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StripePaymentIntentId",
                table: "Payments",
                newName: "GatewayPaymentId");

            migrationBuilder.RenameIndex(
                name: "IX_Payments_StripePaymentIntentId",
                table: "Payments",
                newName: "IX_Payments_GatewayPaymentId");

            migrationBuilder.AddColumn<string>(
                name: "GatewayName",
                table: "Payments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GatewayName",
                table: "Payments");

            migrationBuilder.RenameColumn(
                name: "GatewayPaymentId",
                table: "Payments",
                newName: "StripePaymentIntentId");

            migrationBuilder.RenameIndex(
                name: "IX_Payments_GatewayPaymentId",
                table: "Payments",
                newName: "IX_Payments_StripePaymentIntentId");
        }
    }
}
