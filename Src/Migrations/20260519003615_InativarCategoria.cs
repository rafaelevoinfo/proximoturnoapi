using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class InativarCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CATEGORIA_DESCRICAO",
                table: "CATEGORIA");

            migrationBuilder.AddColumn<bool>(
                name: "ATIVO",
                table: "CATEGORIA",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ATIVO",
                table: "CATEGORIA");

            migrationBuilder.CreateIndex(
                name: "IX_CATEGORIA_DESCRICAO",
                table: "CATEGORIA",
                column: "DESCRICAO",
                unique: true);
        }
    }
}
