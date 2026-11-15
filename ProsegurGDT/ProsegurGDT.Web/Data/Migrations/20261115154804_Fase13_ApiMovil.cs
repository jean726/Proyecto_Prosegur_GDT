using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProsegurGDT.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fase13_ApiMovil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UsuarioId",
                table: "Repartidores",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReportesSuplantacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HojaRutaId = table.Column<int>(type: "int", nullable: false),
                    TarjetaId = table.Column<int>(type: "int", nullable: false),
                    RepartidorId = table.Column<int>(type: "int", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReporteFraudeId = table.Column<int>(type: "int", nullable: true),
                    AtendidoPorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaAtencion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Observacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportesSuplantacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportesSuplantacion_AspNetUsers_AtendidoPorId",
                        column: x => x.AtendidoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportesSuplantacion_HojasRuta_HojaRutaId",
                        column: x => x.HojaRutaId,
                        principalTable: "HojasRuta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportesSuplantacion_Repartidores_RepartidorId",
                        column: x => x.RepartidorId,
                        principalTable: "Repartidores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportesSuplantacion_ReportesFraude_ReporteFraudeId",
                        column: x => x.ReporteFraudeId,
                        principalTable: "ReportesFraude",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportesSuplantacion_Tarjetas_TarjetaId",
                        column: x => x.TarjetaId,
                        principalTable: "Tarjetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResultadosCampo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HojaRutaId = table.Column<int>(type: "int", nullable: false),
                    TarjetaId = table.Column<int>(type: "int", nullable: false),
                    RepartidorId = table.Column<int>(type: "int", nullable: false),
                    Resultado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReceptorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReceptorDocumento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Detalle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultadosCampo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultadosCampo_HojasRuta_HojaRutaId",
                        column: x => x.HojaRutaId,
                        principalTable: "HojasRuta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResultadosCampo_Repartidores_RepartidorId",
                        column: x => x.RepartidorId,
                        principalTable: "Repartidores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResultadosCampo_Tarjetas_TarjetaId",
                        column: x => x.TarjetaId,
                        principalTable: "Tarjetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Repartidores_UsuarioId",
                table: "Repartidores",
                column: "UsuarioId",
                unique: true,
                filter: "[UsuarioId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesSuplantacion_AtendidoPorId",
                table: "ReportesSuplantacion",
                column: "AtendidoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesSuplantacion_HojaRutaId_TarjetaId",
                table: "ReportesSuplantacion",
                columns: new[] { "HojaRutaId", "TarjetaId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportesSuplantacion_RepartidorId",
                table: "ReportesSuplantacion",
                column: "RepartidorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesSuplantacion_ReporteFraudeId",
                table: "ReportesSuplantacion",
                column: "ReporteFraudeId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesSuplantacion_TarjetaId",
                table: "ReportesSuplantacion",
                column: "TarjetaId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultadosCampo_HojaRutaId_TarjetaId",
                table: "ResultadosCampo",
                columns: new[] { "HojaRutaId", "TarjetaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResultadosCampo_RepartidorId",
                table: "ResultadosCampo",
                column: "RepartidorId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultadosCampo_TarjetaId",
                table: "ResultadosCampo",
                column: "TarjetaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Repartidores_AspNetUsers_UsuarioId",
                table: "Repartidores",
                column: "UsuarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Repartidores_AspNetUsers_UsuarioId",
                table: "Repartidores");

            migrationBuilder.DropTable(
                name: "ReportesSuplantacion");

            migrationBuilder.DropTable(
                name: "ResultadosCampo");

            migrationBuilder.DropIndex(
                name: "IX_Repartidores_UsuarioId",
                table: "Repartidores");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Repartidores");
        }
    }
}
