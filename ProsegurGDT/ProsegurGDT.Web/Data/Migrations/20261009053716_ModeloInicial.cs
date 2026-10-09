using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProsegurGDT.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ModeloInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DocumentoIdentidad",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NombreCompleto",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Bancos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bancos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NombreCompleto = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DocumentoIdentidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    DireccionNormalizada = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Remitos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Numero = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BancoId = table.Column<int>(type: "int", nullable: false),
                    CantidadDeclarada = table.Column<int>(type: "int", nullable: false),
                    FechaEmision = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Remitos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Remitos_Bancos_BancoId",
                        column: x => x.BancoId,
                        principalTable: "Bancos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Lotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RemitoId = table.Column<int>(type: "int", nullable: false),
                    FechaRecepcion = table.Column<DateOnly>(type: "date", nullable: false),
                    CantidadDeclarada = table.Column<int>(type: "int", nullable: false),
                    CantidadContada = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CodigoHojaControl = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RegistradoPorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaHoraRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lotes_AspNetUsers_RegistradoPorId",
                        column: x => x.RegistradoPorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lotes_Remitos_RemitoId",
                        column: x => x.RemitoId,
                        principalTable: "Remitos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tarjetas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LoteId = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tarjetas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tarjetas_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tarjetas_Lotes_LoteId",
                        column: x => x.LoteId,
                        principalTable: "Lotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistorialEstados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TarjetaId = table.Column<int>(type: "int", nullable: false),
                    EstadoAnterior = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    EstadoNuevo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialEstados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialEstados_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistorialEstados_Tarjetas_TarjetaId",
                        column: x => x.TarjetaId,
                        principalTable: "Tarjetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstados_TarjetaId",
                table: "HistorialEstados",
                column: "TarjetaId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstados_UsuarioId",
                table: "HistorialEstados",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Lotes_RegistradoPorId",
                table: "Lotes",
                column: "RegistradoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Lotes_RemitoId",
                table: "Lotes",
                column: "RemitoId");

            migrationBuilder.CreateIndex(
                name: "IX_Remitos_BancoId",
                table: "Remitos",
                column: "BancoId");

            migrationBuilder.CreateIndex(
                name: "IX_Remitos_Numero",
                table: "Remitos",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tarjetas_ClienteId",
                table: "Tarjetas",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Tarjetas_Codigo",
                table: "Tarjetas",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tarjetas_LoteId",
                table: "Tarjetas",
                column: "LoteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistorialEstados");

            migrationBuilder.DropTable(
                name: "Tarjetas");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropTable(
                name: "Lotes");

            migrationBuilder.DropTable(
                name: "Remitos");

            migrationBuilder.DropTable(
                name: "Bancos");

            migrationBuilder.DropColumn(
                name: "Activo",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DocumentoIdentidad",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NombreCompleto",
                table: "AspNetUsers");
        }
    }
}
