using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GWT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fund_meta",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    region = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    amc = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ticker = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    scheme_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    isin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    latest_nav = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    nav_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fund_meta", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "nav_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fund_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nav = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    nav_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nav_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_nav_history_fund_meta_fund_id",
                        column: x => x.fund_id,
                        principalTable: "fund_meta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "holdings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fund_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    units = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    avg_cost = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    purchase_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_holdings", x => x.id);
                    table.ForeignKey(
                        name: "FK_holdings_fund_meta_fund_id",
                        column: x => x.fund_id,
                        principalTable: "fund_meta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_holdings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fund_meta_region_name_amc",
                table: "fund_meta",
                columns: new[] { "region", "name", "amc" });

            migrationBuilder.CreateIndex(
                name: "IX_fund_meta_ticker",
                table: "fund_meta",
                column: "ticker",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_holdings_fund_id",
                table: "holdings",
                column: "fund_id");

            migrationBuilder.CreateIndex(
                name: "IX_holdings_user_id",
                table: "holdings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_holdings_user_id_fund_id",
                table: "holdings",
                columns: new[] { "user_id", "fund_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nav_history_fund_id_nav_date",
                table: "nav_history",
                columns: new[] { "fund_id", "nav_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "holdings");

            migrationBuilder.DropTable(
                name: "nav_history");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "fund_meta");
        }
    }
}
