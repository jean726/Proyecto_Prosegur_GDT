using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProsegurGDT.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fase8_Mensajeria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HojasRuta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AsignacionRutaId = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GeneradaPorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaGeneracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Discrepancia = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    AprobadaPorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaAprobacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirmaRegistradaPorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaFirma = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoSinFirma = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HojasRuta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HojasRuta_AsignacionesRuta_AsignacionRutaId",
                        column: x => x.AsignacionRutaId,
                        principalTable: "AsignacionesRuta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HojasRuta_AspNetUsers_AprobadaPorId",
                        column: x => x.AprobadaPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HojasRuta_AspNetUsers_FirmaRegistradaPorId",
                        column: x => x.FirmaRegistradaPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HojasRuta_AspNetUsers_GeneradaPorId",
                        column: x => x.GeneradaPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportesEntregas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FechaJornada = table.Column<DateOnly>(type: "date", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GeneradoPorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaGeneracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Asignadas = table.Column<int>(type: "int", nullable: false),
                    Entregadas = table.Column<int>(type: "int", nullable: false),
                    Devueltas = table.Column<int>(type: "int", nullable: false),
                    Incidencias = table.Column<int>(type: "int", nullable: false),
                    RemitidoPorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaRemision = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportesEntregas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportesEntregas_AspNetUsers_GeneradoPorId",
                        column: x => x.GeneradoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportesEntregas_AspNetUsers_RemitidoPorId",
                        column: x => x.RemitidoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportesFraude",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TarjetaId = table.Column<int>(type: "int", nullable: false),
                    HojaRutaId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    VerificadorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportesFraude", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportesFraude_AspNetUsers_VerificadorId",
                        column: x => x.VerificadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportesFraude_HojasRuta_HojaRutaId",
                        column: x => x.HojaRutaId,
                        principalTable: "HojasRuta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportesFraude_Tarjetas_TarjetaId",
                        column: x => x.TarjetaId,
                        principalTable: "Tarjetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportesEntregasLineas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReporteEntregasId = table.Column<int>(type: "int", nullable: false),
                    HojaRutaId = table.Column<int>(type: "int", nullable: false),
                    RepartidorId = table.Column<int>(type: "int", nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Asignadas = table.Column<int>(type: "int", nullable: false),
                    Entregadas = table.Column<int>(type: "int", nullable: false),
                    Devueltas = table.Column<int>(type: "int", nullable: false),
                    Incidencias = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportesEntregasLineas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportesEntregasLineas_HojasRuta_HojaRutaId",
                        column: x => x.HojaRutaId,
                        principalTable: "HojasRuta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportesEntregasLineas_Repartidores_RepartidorId",
                        column: x => x.RepartidorId,
                        principalTable: "Repartidores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportesEntregasLineas_ReportesEntregas_ReporteEntregasId",
                        column: x => x.ReporteEntregasId,
                        principalTable: "ReportesEntregas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HojasRuta_AprobadaPorId",
                table: "HojasRuta",
                column: "AprobadaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_HojasRuta_AsignacionRutaId",
                table: "HojasRuta",
                column: "AsignacionRutaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HojasRuta_FirmaRegistradaPorId",
                table: "HojasRuta",
                column: "FirmaRegistradaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_HojasRuta_GeneradaPorId",
                table: "HojasRuta",
                column: "GeneradaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesEntregas_FechaJornada",
                table: "ReportesEntregas",
                column: "FechaJornada",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportesEntregas_GeneradoPorId",
                table: "ReportesEntregas",
                column: "GeneradoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesEntregas_RemitidoPorId",
                table: "ReportesEntregas",
                column: "RemitidoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesEntregasLineas_HojaRutaId",
                table: "ReportesEntregasLineas",
                column: "HojaRutaId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesEntregasLineas_RepartidorId",
                table: "ReportesEntregasLineas",
                column: "RepartidorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesEntregasLineas_ReporteEntregasId_HojaRutaId",
                table: "ReportesEntregasLineas",
                columns: new[] { "ReporteEntregasId", "HojaRutaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportesFraude_HojaRutaId",
                table: "ReportesFraude",
                column: "HojaRutaId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesFraude_TarjetaId",
                table: "ReportesFraude",
                column: "TarjetaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportesFraude_VerificadorId",
                table: "ReportesFraude",
                column: "VerificadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportesEntregasLineas");

            migrationBuilder.DropTable(
                name: "ReportesFraude");

            migrationBuilder.DropTable(
                name: "ReportesEntregas");

            migrationBuilder.DropTable(
                name: "HojasRuta");
        }
    }
}
