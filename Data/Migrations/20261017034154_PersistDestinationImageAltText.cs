using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roma.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersistDestinationImageAltText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AltText",
                table: "DestinationImages",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AltText",
                table: "DestinationImages");
        }
    }
}
