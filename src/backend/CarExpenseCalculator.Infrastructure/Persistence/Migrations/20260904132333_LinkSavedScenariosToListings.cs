using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarExpenseCalculator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkSavedScenariosToListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "source_listing_version",
                table: "saved_cost_scenarios",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_saved_cost_scenarios_source_listing_version",
                table: "saved_cost_scenarios",
                sql: "source_listing_version IS NULL OR source_listing_version >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_saved_cost_scenarios_source_listing_version",
                table: "saved_cost_scenarios");

            migrationBuilder.DropColumn(
                name: "source_listing_version",
                table: "saved_cost_scenarios");
        }
    }
}
