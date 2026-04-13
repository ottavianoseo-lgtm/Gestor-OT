using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace GestorOT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorErpIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UserEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Planning"),
                    BudgetTotalUSD = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    BusinessRules = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CropStrategies",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CropType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropStrategies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    ExternalErpId = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Position = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fields",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HectareasTotales = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inventories",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CurrentStock = table.Column<double>(type: "double precision", precision: 18, scale: 4, nullable: false),
                    ReorderLevel = table.Column<double>(type: "double precision", precision: 18, scale: 4, nullable: false),
                    UnitA = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UnitB = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConversionFactor = table.Column<double>(type: "double precision", precision: 18, scale: 6, nullable: false, defaultValue: 1.0),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LaborTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ExternalErpId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaborTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Agronomist"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StrategyItems",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CropStrategyId = table.Column<Guid>(type: "uuid", nullable: false),
                    LaborTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOffset = table.Column<int>(type: "integer", nullable: false),
                    DefaultSupplies = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategyItems_CropStrategies_CropStrategyId",
                        column: x => x.CropStrategyId,
                        principalSchema: "public",
                        principalTable: "CropStrategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignFields",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetYieldTonHa = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    AllocatedHectares = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignFields_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "public",
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignFields_Fields_FieldId",
                        column: x => x.FieldId,
                        principalSchema: "public",
                        principalTable: "Fields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Lots",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Geometry = table.Column<Geometry>(type: "geometry(Polygon, 4326)", nullable: true),
                    CadastralArea = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lots_Fields_FieldId",
                        column: x => x.FieldId,
                        principalSchema: "public",
                        principalTable: "Fields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TankMixRules",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductAId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductBId = table.Column<Guid>(type: "uuid", nullable: false),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Warning"),
                    WarningMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TankMixRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TankMixRules_Inventories_ProductAId",
                        column: x => x.ProductAId,
                        principalSchema: "public",
                        principalTable: "Inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TankMixRules_Inventories_ProductBId",
                        column: x => x.ProductBId,
                        principalSchema: "public",
                        principalTable: "Inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CampaignLots",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductiveArea = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CropId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignLots_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "public",
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignLots_Lots_LotId",
                        column: x => x.LotId,
                        principalSchema: "public",
                        principalTable: "Lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrders",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssignedTo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OTNumber = table.Column<string>(type: "text", nullable: false),
                    ContractorId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgreedRate = table.Column<decimal>(type: "numeric", nullable: false),
                    PlannedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstimatedCostUSD = table.Column<decimal>(type: "numeric", nullable: false),
                    StockReserved = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrders_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "public",
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkOrders_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrders_Lots_LotId",
                        column: x => x.LotId,
                        principalSchema: "public",
                        principalTable: "Lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Labors",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignLotId = table.Column<Guid>(type: "uuid", nullable: false),
                    LaborTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Planned"),
                    ExecutionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstimatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Hectares = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    EffectiveArea = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    RateUnit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "ha"),
                    PlannedDose = table.Column<decimal>(type: "numeric", nullable: false),
                    RealizedDose = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    PrescriptionMapUrl = table.Column<string>(type: "text", nullable: true),
                    MachineryUsedId = table.Column<string>(type: "text", nullable: true),
                    WeatherLogJson = table.Column<string>(type: "text", nullable: true),
                    EvidencePhotosJson = table.Column<string>(type: "text", nullable: true),
                    MetadataExterna = table.Column<string>(type: "jsonb", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Labors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Labors_CampaignLots_CampaignLotId",
                        column: x => x.CampaignLotId,
                        principalSchema: "public",
                        principalTable: "CampaignLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Labors_LaborTypes_LaborTypeId",
                        column: x => x.LaborTypeId,
                        principalTable: "LaborTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Labors_Lots_LotId",
                        column: x => x.LotId,
                        principalSchema: "public",
                        principalTable: "Lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Labors_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalSchema: "public",
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SharedTokens",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedTokens_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalSchema: "public",
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LaborSupplies",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LaborId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedDose = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    RealDose = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    PlannedTotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RealTotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TankMixOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSubstitute = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaborSupplies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LaborSupplies_Inventories_SupplyId",
                        column: x => x.SupplyId,
                        principalSchema: "public",
                        principalTable: "Inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LaborSupplies_Labors_LaborId",
                        column: x => x.LaborId,
                        principalSchema: "public",
                        principalTable: "Labors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignFields_CampaignId_FieldId",
                schema: "public",
                table: "CampaignFields",
                columns: new[] { "CampaignId", "FieldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignFields_FieldId",
                schema: "public",
                table: "CampaignFields",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignLots_CampaignId_LotId",
                schema: "public",
                table: "CampaignLots",
                columns: new[] { "CampaignId", "LotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignLots_LotId",
                schema: "public",
                table: "CampaignLots",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_Labors_CampaignLotId",
                schema: "public",
                table: "Labors",
                column: "CampaignLotId");

            migrationBuilder.CreateIndex(
                name: "IX_Labors_LaborTypeId",
                schema: "public",
                table: "Labors",
                column: "LaborTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Labors_LotId",
                schema: "public",
                table: "Labors",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_Labors_WorkOrderId",
                schema: "public",
                table: "Labors",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LaborSupplies_LaborId",
                schema: "public",
                table: "LaborSupplies",
                column: "LaborId");

            migrationBuilder.CreateIndex(
                name: "IX_LaborSupplies_SupplyId",
                schema: "public",
                table: "LaborSupplies",
                column: "SupplyId");

            migrationBuilder.CreateIndex(
                name: "IX_Lots_FieldId",
                schema: "public",
                table: "Lots",
                column: "FieldId");

            migrationBuilder.CreateIndex(
                name: "IX_Lots_Geometry",
                schema: "public",
                table: "Lots",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_SharedTokens_TokenHash",
                schema: "public",
                table: "SharedTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedTokens_WorkOrderId",
                schema: "public",
                table: "SharedTokens",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategyItems_CropStrategyId",
                schema: "public",
                table: "StrategyItems",
                column: "CropStrategyId");

            migrationBuilder.CreateIndex(
                name: "IX_TankMixRules_ProductAId",
                schema: "public",
                table: "TankMixRules",
                column: "ProductAId");

            migrationBuilder.CreateIndex(
                name: "IX_TankMixRules_ProductBId",
                schema: "public",
                table: "TankMixRules",
                column: "ProductBId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_CampaignId",
                schema: "public",
                table: "WorkOrders",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_EmployeeId",
                schema: "public",
                table: "WorkOrders",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_LotId",
                schema: "public",
                table: "WorkOrders",
                column: "LotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "public");

            migrationBuilder.DropTable(
                name: "CampaignFields",
                schema: "public");

            migrationBuilder.DropTable(
                name: "LaborSupplies",
                schema: "public");

            migrationBuilder.DropTable(
                name: "SharedTokens",
                schema: "public");

            migrationBuilder.DropTable(
                name: "StrategyItems",
                schema: "public");

            migrationBuilder.DropTable(
                name: "TankMixRules",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Tenants",
                schema: "public");

            migrationBuilder.DropTable(
                name: "UserProfiles",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Labors",
                schema: "public");

            migrationBuilder.DropTable(
                name: "CropStrategies",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Inventories",
                schema: "public");

            migrationBuilder.DropTable(
                name: "CampaignLots",
                schema: "public");

            migrationBuilder.DropTable(
                name: "LaborTypes");

            migrationBuilder.DropTable(
                name: "WorkOrders",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Campaigns",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "Lots",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Fields",
                schema: "public");
        }
    }
}
