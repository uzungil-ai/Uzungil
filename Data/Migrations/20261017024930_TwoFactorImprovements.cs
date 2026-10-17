using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roma.Data.Migrations
{
    /// <inheritdoc />
    public partial class TwoFactorImprovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Code",
                table: "TwoFactorCodes",
                newName: "Salt");

            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "TwoFactorCodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "HashedCode",
                table: "TwoFactorCodes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "TwoFactorCodes");

            migrationBuilder.DropColumn(
                name: "HashedCode",
                table: "TwoFactorCodes");

            migrationBuilder.RenameColumn(
                name: "Salt",
                table: "TwoFactorCodes",
                newName: "Code");
        }
    }
}
