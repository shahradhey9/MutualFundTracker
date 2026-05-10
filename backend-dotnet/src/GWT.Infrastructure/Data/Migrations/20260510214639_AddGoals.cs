using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GWT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "goals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    goal_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    inflation_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    target_debt_pct = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goals", x => x.id);
                    table.ForeignKey(
                        name: "FK_goals_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "goal_funds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    goal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    holding_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goal_funds", x => x.id);
                    table.ForeignKey(
                        name: "FK_goal_funds_goals_goal_id",
                        column: x => x.goal_id,
                        principalTable: "goals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_goal_funds_holdings_holding_id",
                        column: x => x.holding_id,
                        principalTable: "holdings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_goal_funds_goal_id_holding_id",
                table: "goal_funds",
                columns: new[] { "goal_id", "holding_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goal_funds_holding_id",
                table: "goal_funds",
                column: "holding_id");

            migrationBuilder.CreateIndex(
                name: "IX_goals_user_id",
                table: "goals",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "goal_funds");

            migrationBuilder.DropTable(
                name: "goals");
        }
    }
}
