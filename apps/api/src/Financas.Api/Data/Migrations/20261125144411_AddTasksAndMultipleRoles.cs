using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financas.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTasksAndMultipleRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_roles_user_id_board_id",
                table: "user_roles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_roles_role",
                table: "user_roles");

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    board_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    status = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tasks", x => x.id);
                    table.CheckConstraint("ck_tasks_status", "status in ('a_fazer', 'em_andamento', 'concluida')");
                    table.ForeignKey(
                        name: "fk_tasks_boards_board_id",
                        column: x => x.board_id,
                        principalTable: "boards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tasks_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tasks_users_updated_by",
                        column: x => x.updated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_assignees",
                columns: table => new
                {
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_assignees", x => new { x.task_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_task_assignees_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_task_assignees_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_user_id_board_id_role",
                table: "user_roles",
                columns: new[] { "user_id", "board_id", "role" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_roles_role",
                table: "user_roles",
                sql: "role in ('admin', 'tesoureiro', 'junta_administrativa', 'viewer')");

            migrationBuilder.CreateIndex(
                name: "ix_task_assignees_user_id",
                table: "task_assignees",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_board_id_status_position",
                table: "tasks",
                columns: new[] { "board_id", "status", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_created_by",
                table: "tasks",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_updated_by",
                table: "tasks",
                column: "updated_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_assignees");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropIndex(
                name: "ix_user_roles_user_id_board_id_role",
                table: "user_roles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_roles_role",
                table: "user_roles");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_user_id_board_id",
                table: "user_roles",
                columns: new[] { "user_id", "board_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_roles_role",
                table: "user_roles",
                sql: "role in ('admin', 'tesoureiro', 'viewer')");
        }
    }
}
