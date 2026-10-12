using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProsegurGDT.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fase6_Clasificacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ZonaId",
                table: "Tarjetas",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DireccionesFraudulentas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DireccionOriginal = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    DireccionNormalizada = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Vector = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ModeloVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RegistradaPorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidadaPorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaValidacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DireccionesFraudulentas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DireccionesFraudulentas_AspNetUsers_RegistradaPorId",
                        column: x => x.RegistradaPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DireccionesFraudulentas_AspNetUsers_ValidadaPorId",
                        column: x => x.ValidadaPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Zonas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zonas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Alertas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TarjetaId = table.Column<int>(type: "int", nullable: false),
                    DireccionFraudulentaId = table.Column<int>(type: "int", nullable: false),
                    Puntaje = table.Column<double>(type: "float", nullable: false),
                    Nivel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Metodo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Decision = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FechaGeneracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecididoPorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FechaDecision = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alertas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alertas_AspNetUsers_DecididoPorId",
                        column: x => x.DecididoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Alertas_DireccionesFraudulentas_DireccionFraudulentaId",
                        column: x => x.DireccionFraudulentaId,
                        principalTable: "DireccionesFraudulentas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Alertas_Tarjetas_TarjetaId",
                        column: x => x.TarjetaId,
                        principalTable: "Tarjetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tarjetas_ZonaId",
                table: "Tarjetas",
                column: "ZonaId");

            migrationBuilder.CreateIndex(
                name: "IX_Alertas_DecididoPorId",
                table: "Alertas",
                column: "DecididoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Alertas_DireccionFraudulentaId",
                table: "Alertas",
                column: "DireccionFraudulentaId");

            migrationBuilder.CreateIndex(
                name: "IX_Alertas_TarjetaId_DireccionFraudulentaId",
                table: "Alertas",
                columns: new[] { "TarjetaId", "DireccionFraudulentaId" });

            migrationBuilder.CreateIndex(
                name: "IX_DireccionesFraudulentas_DireccionNormalizada",
                table: "DireccionesFraudulentas",
                column: "DireccionNormalizada");

            migrationBuilder.CreateIndex(
                name: "IX_DireccionesFraudulentas_RegistradaPorId",
                table: "DireccionesFraudulentas",
                column: "RegistradaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_DireccionesFraudulentas_ValidadaPorId",
                table: "DireccionesFraudulentas",
                column: "ValidadaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Zonas_Nombre",
                table: "Zonas",
                column: "Nombre",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tarjetas_Zonas_ZonaId",
                table: "Tarjetas",
                column: "ZonaId",
                principalTable: "Zonas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tarjetas_Zonas_ZonaId",
                table: "Tarjetas");

            migrationBuilder.DropTable(
                name: "Alertas");

            migrationBuilder.DropTable(
                name: "Zonas");

            migrationBuilder.DropTable(
                name: "DireccionesFraudulentas");

            migrationBuilder.DropIndex(
                name: "IX_Tarjetas_ZonaId",
                table: "Tarjetas");

            migrationBuilder.DropColumn(
                name: "ZonaId",
                table: "Tarjetas");
        }
    }
}
