using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaternityManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordVersionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecordVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityId = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousValuesJson = table.Column<string>(type: "TEXT", nullable: false),
                    NewValuesJson = table.Column<string>(type: "TEXT", nullable: false),
                    StaffId = table.Column<int>(type: "INTEGER", nullable: true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecordVersions_EntityType_EntityId_ChangedAtUtc",
                table: "RecordVersions",
                columns: new[] { "EntityType", "EntityId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RecordVersions_StaffId",
                table: "RecordVersions",
                column: "StaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecordVersions");
        }
    }
}
