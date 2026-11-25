using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financas.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "priority",
                table: "tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tasks_priority",
                table: "tasks",
                sql: "priority is null or priority in ('baixa', 'media', 'alta', 'urgente')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tasks_priority",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "tasks");
        }
    }
}
