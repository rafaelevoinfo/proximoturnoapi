using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class StatusPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "STATUS",
                table: "PEDIDO_JOGO");

            migrationBuilder.AddColumn<bool>(
                name: "RENOVADO",
                table: "PEDIDO_JOGO",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<short>(
                name: "STATUS",
                table: "PEDIDO",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RENOVADO",
                table: "PEDIDO_JOGO");

            migrationBuilder.DropColumn(
                name: "STATUS",
                table: "PEDIDO");

            migrationBuilder.AddColumn<short>(
                name: "STATUS",
                table: "PEDIDO_JOGO",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);
        }
    }
}
