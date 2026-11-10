using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProsegurGDT.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fase7_Rutas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventarioBoveda",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TarjetaId = table.Column<int>(type: "int", nullable: false),
                    Ubicacion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EstadoFisico = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventarioBoveda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventarioBoveda_Tarjetas_TarjetaId",
                        column: x => x.TarjetaId,
                        principalTable: "Tarjetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Planillas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoteId = table.Column<int>(type: "int", nullable: false),
                    GeneradaPorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaGeneracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Planillas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Planillas_AspNetUsers_GeneradaPorId",
                        column: x => x.GeneradaPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Planillas_Lotes_LoteId",
                        column: x => x.LoteId,
                        principalTable: "Lotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Repartidores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ZonaHabitualId = table.Column<int>(type: "int", nullable: true),
                    CapacidadTarjetas = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repartidores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Repartidores_Zonas_ZonaHabitualId",
                        column: x => x.ZonaHabitualId,
                        principalTable: "Zonas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanillaZonas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanillaArmadoId = table.Column<int>(type: "int", nullable: false),
                    ZonaId = table.Column<int>(type: "int", nullable: false),
                    TipoRepartidorRequerido = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OrdenVisita = table.Column<int>(type: "int", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanillaZonas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanillaZonas_Planillas_PlanillaArmadoId",
                        column: x => x.PlanillaArmadoId,
                        principalTable: "Planillas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanillaZonas_Zonas_ZonaId",
                        column: x => x.ZonaId,
                        principalTable: "Zonas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportesDensidad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoteId = table.Column<int>(type: "int", nullable: false),
                    PlanillaArmadoId = table.Column<int>(type: "int", nullable: false),
                    GeneradoPorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaGeneracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevisadoPorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaRevision = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportesDensidad", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportesDensidad_AspNetUsers_GeneradoPorId",
                        column: x => x.GeneradoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportesDensidad_AspNetUsers_RevisadoPorId",
                        column: x => x.RevisadoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportesDensidad_Lotes_LoteId",
                        column: x => x.LoteId,
                        principalTable: "Lotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportesDensidad_Planillas_PlanillaArmadoId",
                        column: x => x.PlanillaArmadoId,
                        principalTable: "Planillas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HojasAsignacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoteId = table.Column<int>(type: "int", nullable: false),
                    ReporteDensidadId = table.Column<int>(type: "int", nullable: false),
                    GeneradaPorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaHoraEmision = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TotalTarjetas = table.Column<int>(type: "int", nullable: false),
                    TotalRutas = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HojasAsignacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HojasAsignacion_AspNetUsers_GeneradaPorId",
                        column: x => x.GeneradaPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HojasAsignacion_Lotes_LoteId",
                        column: x => x.LoteId,
                        principalTable: "Lotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HojasAsignacion_ReportesDensidad_ReporteDensidadId",
                        column: x => x.ReporteDensidadId,
                        principalTable: "ReportesDensidad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportesDensidadZona",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReporteDensidadId = table.Column<int>(type: "int", nullable: false),
                    ZonaId = table.Column<int>(type: "int", nullable: false),
                    CantidadTarjetas = table.Column<int>(type: "int", nullable: false),
                    CantidadNoClasificables = table.Column<int>(type: "int", nullable: false),
                    TieneAlerta = table.Column<bool>(type: "bit", nullable: false),
                    TipoRepartidorRequerido = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportesDensidadZona", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportesDensidadZona_ReportesDensidad_ReporteDensidadId",
                        column: x => x.ReporteDensidadId,
                        principalTable: "ReportesDensidad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportesDensidadZona_Zonas_ZonaId",
                        column: x => x.ZonaId,
                        principalTable: "Zonas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AsignacionesRuta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HojaAsignacionId = table.Column<int>(type: "int", nullable: false),
                    RepartidorId = table.Column<int>(type: "int", nullable: false),
                    ZonaId = table.Column<int>(type: "int", nullable: false),
                    SectorCobertura = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CantidadTarjetas = table.Column<int>(type: "int", nullable: false),
                    OrdenVisita = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionesRuta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsignacionesRuta_HojasAsignacion_HojaAsignacionId",
                        column: x => x.HojaAsignacionId,
                        principalTable: "HojasAsignacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AsignacionesRuta_Repartidores_RepartidorId",
                        column: x => x.RepartidorId,
                        principalTable: "Repartidores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AsignacionesRuta_Zonas_ZonaId",
                        column: x => x.ZonaId,
                        principalTable: "Zonas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Conciliaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoteId = table.Column<int>(type: "int", nullable: false),
                    HojaAsignacionId = table.Column<int>(type: "int", nullable: false),
                    VerificadaPorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaVerificacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CantidadFicha = table.Column<int>(type: "int", nullable: false),
                    CantidadPlanilla = table.Column<int>(type: "int", nullable: false),
                    CantidadHoja = table.Column<int>(type: "int", nullable: false),
                    CantidadFisica = table.Column<int>(type: "int", nullable: false),
                    Faltantes = table.Column<int>(type: "int", nullable: false),
                    Duplicados = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conciliaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conciliaciones_AspNetUsers_VerificadaPorId",
                        column: x => x.VerificadaPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conciliaciones_HojasAsignacion_HojaAsignacionId",
                        column: x => x.HojaAsignacionId,
                        principalTable: "HojasAsignacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conciliaciones_Lotes_LoteId",
                        column: x => x.LoteId,
                        principalTable: "Lotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AsignacionesTarjeta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AsignacionRutaId = table.Column<int>(type: "int", nullable: false),
                    TarjetaId = table.Column<int>(type: "int", nullable: false),
                    FechaAsignacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionesTarjeta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsignacionesTarjeta_AsignacionesRuta_AsignacionRutaId",
                        column: x => x.AsignacionRutaId,
                        principalTable: "AsignacionesRuta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AsignacionesTarjeta_Tarjetas_TarjetaId",
                        column: x => x.TarjetaId,
                        principalTable: "Tarjetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncidenciasConciliacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConciliacionLoteId = table.Column<int>(type: "int", nullable: false),
                    TarjetaId = table.Column<int>(type: "int", nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BuscadorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Ubicacion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FechaResolucion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidenciasConciliacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidenciasConciliacion_AspNetUsers_BuscadorId",
                        column: x => x.BuscadorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncidenciasConciliacion_Conciliaciones_ConciliacionLoteId",
                        column: x => x.ConciliacionLoteId,
                        principalTable: "Conciliaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncidenciasConciliacion_Tarjetas_TarjetaId",
                        column: x => x.TarjetaId,
                        principalTable: "Tarjetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesRuta_HojaAsignacionId",
                table: "AsignacionesRuta",
                column: "HojaAsignacionId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesRuta_RepartidorId",
                table: "AsignacionesRuta",
                column: "RepartidorId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesRuta_ZonaId",
                table: "AsignacionesRuta",
                column: "ZonaId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesTarjeta_AsignacionRutaId_TarjetaId",
                table: "AsignacionesTarjeta",
                columns: new[] { "AsignacionRutaId", "TarjetaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesTarjeta_TarjetaId",
                table: "AsignacionesTarjeta",
                column: "TarjetaId");

            migrationBuilder.CreateIndex(
                name: "IX_Conciliaciones_HojaAsignacionId",
                table: "Conciliaciones",
                column: "HojaAsignacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Conciliaciones_LoteId",
                table: "Conciliaciones",
                column: "LoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Conciliaciones_VerificadaPorId",
                table: "Conciliaciones",
                column: "VerificadaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_HojasAsignacion_GeneradaPorId",
                table: "HojasAsignacion",
                column: "GeneradaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_HojasAsignacion_LoteId",
                table: "HojasAsignacion",
                column: "LoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HojasAsignacion_ReporteDensidadId",
                table: "HojasAsignacion",
                column: "ReporteDensidadId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidenciasConciliacion_BuscadorId",
                table: "IncidenciasConciliacion",
                column: "BuscadorId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidenciasConciliacion_ConciliacionLoteId",
                table: "IncidenciasConciliacion",
                column: "ConciliacionLoteId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidenciasConciliacion_TarjetaId",
                table: "IncidenciasConciliacion",
                column: "TarjetaId");

            migrationBuilder.CreateIndex(
                name: "IX_InventarioBoveda_TarjetaId",
                table: "InventarioBoveda",
                column: "TarjetaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Planillas_GeneradaPorId",
                table: "Planillas",
                column: "GeneradaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Planillas_LoteId",
                table: "Planillas",
                column: "LoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanillaZonas_PlanillaArmadoId",
                table: "PlanillaZonas",
                column: "PlanillaArmadoId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanillaZonas_ZonaId",
                table: "PlanillaZonas",
                column: "ZonaId");

            migrationBuilder.CreateIndex(
                name: "IX_Repartidores_ZonaHabitualId",
                table: "Repartidores",
                column: "ZonaHabitualId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesDensidad_GeneradoPorId",
                table: "ReportesDensidad",
                column: "GeneradoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesDensidad_LoteId",
                table: "ReportesDensidad",
                column: "LoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportesDensidad_PlanillaArmadoId",
                table: "ReportesDensidad",
                column: "PlanillaArmadoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesDensidad_RevisadoPorId",
                table: "ReportesDensidad",
                column: "RevisadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesDensidadZona_ReporteDensidadId",
                table: "ReportesDensidadZona",
                column: "ReporteDensidadId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportesDensidadZona_ZonaId",
                table: "ReportesDensidadZona",
                column: "ZonaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsignacionesTarjeta");

            migrationBuilder.DropTable(
                name: "IncidenciasConciliacion");

            migrationBuilder.DropTable(
                name: "InventarioBoveda");

            migrationBuilder.DropTable(
                name: "PlanillaZonas");

            migrationBuilder.DropTable(
                name: "ReportesDensidadZona");

            migrationBuilder.DropTable(
                name: "AsignacionesRuta");

            migrationBuilder.DropTable(
                name: "Conciliaciones");

            migrationBuilder.DropTable(
                name: "Repartidores");

            migrationBuilder.DropTable(
                name: "HojasAsignacion");

            migrationBuilder.DropTable(
                name: "ReportesDensidad");

            migrationBuilder.DropTable(
                name: "Planillas");
        }
    }
}
