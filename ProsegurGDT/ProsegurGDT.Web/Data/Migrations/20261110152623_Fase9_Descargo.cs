using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProsegurGDT.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fase9_Descargo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActasFraude",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TarjetaId = table.Column<int>(type: "int", nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReporteFraudeId = table.Column<int>(type: "int", nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DescargadorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BovedaPorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaIngreso = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UbicacionBoveda = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActasFraude", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActasFraude_AspNetUsers_BovedaPorId",
                        column: x => x.BovedaPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActasFraude_AspNetUsers_DescargadorId",
                        column: x => x.DescargadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActasFraude_ReportesFraude_ReporteFraudeId",
                        column: x => x.ReporteFraudeId,
                        principalTable: "ReportesFraude",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActasFraude_Tarjetas_TarjetaId",
                        column: x => x.TarjetaId,
                        principalTable: "Tarjetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DescargosRuta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HojaRutaId = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CargosRecibidos = table.Column<int>(type: "int", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DescargadorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaCierre = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DocumentacionOrganizada = table.Column<bool>(type: "bit", nullable: false),
                    FechaOrganizacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DescargosRuta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DescargosRuta_AspNetUsers_DescargadorId",
                        column: x => x.DescargadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DescargosRuta_HojasRuta_HojaRutaId",
                        column: x => x.HojaRutaId,
                        principalTable: "HojasRuta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportesDescargo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FechaJornada = table.Column<DateOnly>(type: "date", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GeneradoPorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaGeneracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Asignadas = table.Column<int>(type: "int", nullable: false),
                    Entregadas = table.Column<int>(type: "int", nullable: false),
                    Devueltas = table.Column<int>(type: "int", nullable: false),
                    EnCustodia = table.Column<int>(type: "int", nullable: false),
                    Incidencias = table.Column<int>(type: "int", nullable: false),
                    Fraude = table.Column<int>(type: "int", nullable: false),
                    BancoDestino = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DerivadoPorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaDerivacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportesDescargo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportesDescargo_AspNetUsers_DerivadoPorId",
                        column: x => x.DerivadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportesDescargo_AspNetUsers_GeneradoPorId",
                        column: x => x.GeneradoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DescargosTarjeta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DescargoRutaId = table.Column<int>(type: "int", nullable: false),
                    TarjetaId = table.Column<int>(type: "int", nullable: false),
                    Resultado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ReceptorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReceptorDocumento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Verificacion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Detalle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ObservacionBuscador = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BuscadorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaVerificacionBuscador = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DescargadorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DescargosTarjeta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DescargosTarjeta_AspNetUsers_BuscadorId",
                        column: x => x.BuscadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DescargosTarjeta_AspNetUsers_DescargadorId",
                        column: x => x.DescargadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DescargosTarjeta_DescargosRuta_DescargoRutaId",
                        column: x => x.DescargoRutaId,
                        principalTable: "DescargosRuta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DescargosTarjeta_Tarjetas_TarjetaId",
                        column: x => x.TarjetaId,
                        principalTable: "Tarjetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportesDescargoLineas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReporteDescargoId = table.Column<int>(type: "int", nullable: false),
                    DescargoRutaId = table.Column<int>(type: "int", nullable: false),
                    RepartidorId = table.Column<int>(type: "int", nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Asignadas = table.Column<int>(type: "int", nullable: false),
                    Entregadas = table.Column<int>(type: "int", nullable: false),
                    Devueltas = table.Column<int>(type: "int", nullable: false),
                    EnCustodia = table.Column<int>(type: "int", nullable: false),
                    Incidencias = table.Column<int>(type: "int", nullable: false),
                    Fraude = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportesDescargoLineas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportesDescargoLineas_DescargosRuta_DescargoRutaId",
                        column: x => x.DescargoRutaId,
                        principalTable: "DescargosRuta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportesDescargoLineas_Repartidores_RepartidorId",
                        column: x => x.RepartidorId,
                        principalTable: "Repartidores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportesDescargoLineas_ReportesDescargo_ReporteDescargoId",
                        column: x => x.ReporteDescargoId,
                        principalTable: "ReportesDescargo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActasFraude_BovedaPorId",
                table: "ActasFraude",
                column: "BovedaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ActasFraude_DescargadorId",
                table: "ActasFraude",
                column: "DescargadorId");

            migrationBuilder.CreateIndex(
                name: "IX_ActasFraude_ReporteFraudeId",
                table: "ActasFraude",
                column: "ReporteFraudeId");

            migrationBuilder.CreateIndex(
                name: "IX_ActasFraude_TarjetaId",
                table: "ActasFraude",
                column: "TarjetaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DescargosRuta_DescargadorId",
                table: "DescargosRuta",
                column: "DescargadorId");

            migrationBuilder.CreateIndex(
                name: "IX_DescargosRuta_HojaRutaId",
                table: "DescargosRuta",
                column: "HojaRutaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DescargosTarjeta_BuscadorId",
                table: "DescargosTarjeta",
                column: "BuscadorId");

            migrationBuilder.CreateIndex(
                name: "IX_DescargosTarjeta_DescargadorId",
                table: "DescargosTarjeta",
                column: "DescargadorId");

            migrationBuilder.CreateIndex(
                name: "IX_DescargosTarjeta_DescargoRutaId",
                table: "DescargosTarjeta",
                column: "DescargoRutaId");

            migrationBuilder.CreateIndex(
                name: "IX_DescargosTarjeta_TarjetaId",
                table: "DescargosTarjeta",
                column: "TarjetaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportesDescargo_DerivadoPorId",
                table: "ReportesDescargo",
                column: "DerivadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesDescargo_FechaJornada_Tipo",
                table: "ReportesDescargo",
                columns: new[] { "FechaJornada", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportesDescargo_GeneradoPorId",
                table: "ReportesDescargo",
                column: "GeneradoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesDescargoLineas_DescargoRutaId",
                table: "ReportesDescargoLineas",
                column: "DescargoRutaId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesDescargoLineas_RepartidorId",
                table: "ReportesDescargoLineas",
                column: "RepartidorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesDescargoLineas_ReporteDescargoId_DescargoRutaId",
                table: "ReportesDescargoLineas",
                columns: new[] { "ReporteDescargoId", "DescargoRutaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActasFraude");

            migrationBuilder.DropTable(
                name: "DescargosTarjeta");

            migrationBuilder.DropTable(
                name: "ReportesDescargoLineas");

            migrationBuilder.DropTable(
                name: "DescargosRuta");

            migrationBuilder.DropTable(
                name: "ReportesDescargo");
        }
    }
}
