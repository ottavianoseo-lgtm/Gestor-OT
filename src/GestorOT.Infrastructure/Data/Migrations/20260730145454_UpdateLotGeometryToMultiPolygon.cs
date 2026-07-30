using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace GestorOT.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLotGeometryToMultiPolygon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Geometry>(
                name: "Geometry",
                schema: "public",
                table: "Lots",
                type: "geometry(Geometry, 4326)",
                nullable: true,
                oldClrType: typeof(Geometry),
                oldType: "geometry(Polygon, 4326)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Geometry>(
                name: "Geometry",
                schema: "public",
                table: "Lots",
                type: "geometry(Polygon, 4326)",
                nullable: true,
                oldClrType: typeof(Geometry),
                oldType: "geometry(Geometry, 4326)",
                oldNullable: true);
        }
    }
}
