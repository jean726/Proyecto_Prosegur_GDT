using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProsegurGDT.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fase16_Mejoras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tarjetas_Estado",
                table: "Tarjetas",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_FechaHoraUtc",
                table: "AuditLogs",
                column: "FechaHoraUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tarjetas_Estado",
                table: "Tarjetas");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_FechaHoraUtc",
                table: "AuditLogs");
        }
    }
}
