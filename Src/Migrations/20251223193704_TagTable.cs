using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class TagTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TAG_DESCRICAO",
                table: "TAG",
                column: "DESCRICAO",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TAG_DESCRICAO",
                table: "TAG");
        }
    }
}
