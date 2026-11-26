using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaternityManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddLaboratoryResultWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "TestDate",
                table: "LaboratoryResults",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Result",
                table: "LaboratoryResults",
                type: "TEXT",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RequestedDate",
                table: "LaboratoryResults",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "LaboratoryResults",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "LaboratoryResults"
                SET "Status" = CASE
                    WHEN "Result" IS NOT NULL
                         AND trim(
                             "Result",
                             char(9) || char(10) || char(11) || char(12) ||
                             char(13) || char(32) || char(133) || char(160) ||
                             char(5760) || char(8192) || char(8193) || char(8194) ||
                             char(8195) || char(8196) || char(8197) || char(8198) ||
                             char(8199) || char(8200) || char(8201) || char(8202) ||
                             char(8232) || char(8233) || char(8239) ||
                             char(8287) || char(12288)
                         ) <> ''
                    THEN 'Resulted'
                    ELSE NULL
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE "__LaboratoryResultRollbackGuard"
                (
                    "Guard" INTEGER NOT NULL CHECK ("Guard" = 1)
                );

                INSERT INTO "__LaboratoryResultRollbackGuard" ("Guard")
                SELECT CASE
                    WHEN EXISTS
                    (
                        SELECT 1
                        FROM "LaboratoryResults"
                        WHERE "RequestedDate" IS NOT NULL
                           OR "TestDate" IS NULL
                           OR "Result" IS NULL
                           OR NOT
                              (
                                  (
                                      "Status" IS NULL
                                      AND trim(
                                          "Result",
                                          char(9) || char(10) || char(11) || char(12) ||
                                          char(13) || char(32) || char(133) || char(160) ||
                                          char(5760) || char(8192) || char(8193) || char(8194) ||
                                          char(8195) || char(8196) || char(8197) || char(8198) ||
                                          char(8199) || char(8200) || char(8201) || char(8202) ||
                                          char(8232) || char(8233) || char(8239) ||
                                          char(8287) || char(12288)
                                      ) = ''
                                  )
                                  OR
                                  (
                                      "Status" = 'Resulted'
                                      AND trim(
                                          "Result",
                                          char(9) || char(10) || char(11) || char(12) ||
                                          char(13) || char(32) || char(133) || char(160) ||
                                          char(5760) || char(8192) || char(8193) || char(8194) ||
                                          char(8195) || char(8196) || char(8197) || char(8198) ||
                                          char(8199) || char(8200) || char(8201) || char(8202) ||
                                          char(8232) || char(8233) || char(8239) ||
                                          char(8287) || char(12288)
                                      ) <> ''
                                  )
                              )
                    )
                    THEN 0
                    ELSE 1
                END;

                DROP TABLE "__LaboratoryResultRollbackGuard";
                """);

            migrationBuilder.DropColumn(
                name: "RequestedDate",
                table: "LaboratoryResults");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "LaboratoryResults");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "TestDate",
                table: "LaboratoryResults",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Result",
                table: "LaboratoryResults",
                type: "TEXT",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 2000,
                oldNullable: true);
        }
    }
}
