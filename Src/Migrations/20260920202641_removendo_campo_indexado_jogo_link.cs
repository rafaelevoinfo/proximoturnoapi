using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class removendo_campo_indexado_jogo_link : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "INDEXADO",
                table: "JOGO_LINK");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "INDEXADO",
                table: "JOGO_LINK",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }
    }
}
