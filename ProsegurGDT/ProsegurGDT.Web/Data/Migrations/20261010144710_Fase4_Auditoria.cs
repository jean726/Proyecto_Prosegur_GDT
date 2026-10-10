using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProsegurGDT.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fase4_Auditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FechaHoraUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Entidad = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    EntidadId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Detalle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");
        }
    }
}
