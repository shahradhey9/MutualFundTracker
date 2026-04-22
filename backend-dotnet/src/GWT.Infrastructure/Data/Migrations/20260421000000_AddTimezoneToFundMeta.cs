using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GWT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimezoneToFundMeta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "timezone",
                table: "fund_meta",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Back-fill existing rows so the NAV sync background service can group
            // funds by timezone immediately after the migration runs.
            migrationBuilder.Sql("UPDATE fund_meta SET timezone = 'Asia/Kolkata'     WHERE region = 'INDIA'  AND timezone IS NULL");
            migrationBuilder.Sql("UPDATE fund_meta SET timezone = 'America/New_York' WHERE region = 'GLOBAL' AND timezone IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "timezone",
                table: "fund_meta");
        }
    }
}
