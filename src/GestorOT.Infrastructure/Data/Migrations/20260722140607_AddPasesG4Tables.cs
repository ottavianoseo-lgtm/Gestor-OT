using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPasesG4Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LaborTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DebitAccountCode = table.Column<string>(type: "text", nullable: false),
                    CreditAccountCode = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CodEmpresa = table.Column<long>(type: "bigint", nullable: true),
                    CodComprobante = table.Column<long>(type: "bigint", nullable: true),
                    PuntoVenta = table.Column<int>(type: "integer", nullable: false),
                    CodMoneda = table.Column<long>(type: "bigint", nullable: true),
                    CodPerfilDebe = table.Column<long>(type: "bigint", nullable: true),
                    CodPerfilHaber = table.Column<long>(type: "bigint", nullable: true),
                    CodPersona = table.Column<long>(type: "bigint", nullable: true),
                    NoImputaGestion = table.Column<bool>(type: "boolean", nullable: false),
                    NoImputaContabilidad = table.Column<bool>(type: "boolean", nullable: false),
                    NoImputaCentro = table.Column<bool>(type: "boolean", nullable: false),
                    NoImputaAuxiliar = table.Column<bool>(type: "boolean", nullable: false),
                    CodCuentaDebeGestion = table.Column<long>(type: "bigint", nullable: true),
                    CodCuentaHaberGestion = table.Column<long>(type: "bigint", nullable: true),
                    CodCuentaDebeCentro = table.Column<long>(type: "bigint", nullable: true),
                    CodCuentaHaberCentro = table.Column<long>(type: "bigint", nullable: true),
                    CodCuentaDebeContabilidad = table.Column<long>(type: "bigint", nullable: true),
                    CodCuentaHaberContabilidad = table.Column<long>(type: "bigint", nullable: true),
                    CodCuentaDebeAuxiliar = table.Column<long>(type: "bigint", nullable: true),
                    CodCuentaHaberAuxiliar = table.Column<long>(type: "bigint", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountConfigurations_LaborTypes_LaborTypeId",
                        column: x => x.LaborTypeId,
                        principalTable: "LaborTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PasesLote",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneradoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: true),
                    TotalPases = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasesLote", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PasesImputacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaseLoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    LaborId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdReferencia = table.Column<string>(type: "text", nullable: false),
                    IdAgrupacionPase = table.Column<int>(type: "integer", nullable: false),
                    CodEmpresa = table.Column<long>(type: "bigint", nullable: false),
                    CodComprobante = table.Column<long>(type: "bigint", nullable: false),
                    NoImputaGestion = table.Column<bool>(type: "boolean", nullable: false),
                    NoImputaContabilidad = table.Column<bool>(type: "boolean", nullable: false),
                    NoImputaCentro = table.Column<bool>(type: "boolean", nullable: false),
                    NoImputaAuxiliar = table.Column<bool>(type: "boolean", nullable: false),
                    PuntoVenta = table.Column<int>(type: "integer", nullable: false),
                    NumeroComprobante = table.Column<int>(type: "integer", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CodPersona = table.Column<long>(type: "bigint", nullable: true),
                    CodMoneda = table.Column<long>(type: "bigint", nullable: false),
                    CodListaDePrecios = table.Column<long>(type: "bigint", nullable: true),
                    CodConcepto = table.Column<long>(type: "bigint", nullable: false),
                    CodigoConcepto = table.Column<string>(type: "text", nullable: true),
                    CantidadAuxiliar = table.Column<decimal>(type: "numeric", nullable: true),
                    Cantidad = table.Column<decimal>(type: "numeric", nullable: false),
                    Precio = table.Column<decimal>(type: "numeric", nullable: false),
                    CodPerfilImputacionDebe = table.Column<long>(type: "bigint", nullable: true),
                    CodPerfilImputacionHaber = table.Column<long>(type: "bigint", nullable: true),
                    CodCuentaDebeGestion = table.Column<long>(type: "bigint", nullable: true),
                    CodCuentaHaberGestion = table.Column<long>(type: "bigint", nullable: true),
                    CodCuentaDebeCentro = table.Column<long>(type: "bigint", nullable: true),
                    CodCuentaHaberCentro = table.Column<long>(type: "bigint", nullable: true),
                    CodCuentaDebeContabilidad = table.Column<long>(type: "bigint", nullable: true),
                    CodCuentaHaberContabilidad = table.Column<long>(type: "bigint", nullable: true),
                    CodCuentaDebeAuxiliar = table.Column<long>(type: "bigint", nullable: true),
                    CodCuentaHaberAuxiliar = table.Column<long>(type: "bigint", nullable: true),
                    Notas = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasesImputacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasesImputacion_Labors_LaborId",
                        column: x => x.LaborId,
                        principalSchema: "public",
                        principalTable: "Labors",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PasesImputacion_PasesLote_PaseLoteId",
                        column: x => x.PaseLoteId,
                        principalTable: "PasesLote",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PasesImputacion_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalSchema: "public",
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountConfigurations_LaborTypeId",
                table: "AccountConfigurations",
                column: "LaborTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PasesImputacion_LaborId",
                table: "PasesImputacion",
                column: "LaborId");

            migrationBuilder.CreateIndex(
                name: "IX_PasesImputacion_PaseLoteId",
                table: "PasesImputacion",
                column: "PaseLoteId");

            migrationBuilder.CreateIndex(
                name: "IX_PasesImputacion_WorkOrderId",
                table: "PasesImputacion",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountConfigurations");

            migrationBuilder.DropTable(
                name: "PasesImputacion");

            migrationBuilder.DropTable(
                name: "PasesLote");
        }
    }
}
