using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class MudandoNomeTabelaItensPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PEDIDO_JOGO_JOGO_ID_JOGO",
                table: "PEDIDO_JOGO");

            migrationBuilder.DropForeignKey(
                name: "FK_PEDIDO_JOGO_PEDIDO_ID_PEDIDO",
                table: "PEDIDO_JOGO");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PEDIDO_JOGO",
                table: "PEDIDO_JOGO");

            migrationBuilder.RenameTable(
                name: "PEDIDO_JOGO",
                newName: "PEDIDO_ITEM");

            migrationBuilder.RenameIndex(
                name: "IX_PEDIDO_JOGO_ID_PEDIDO",
                table: "PEDIDO_ITEM",
                newName: "IX_PEDIDO_ITEM_ID_PEDIDO");

            migrationBuilder.RenameIndex(
                name: "IX_PEDIDO_JOGO_ID_JOGO",
                table: "PEDIDO_ITEM",
                newName: "IX_PEDIDO_ITEM_ID_JOGO");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PEDIDO_ITEM",
                table: "PEDIDO_ITEM",
                column: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDO_ITEM_JOGO_ID_JOGO",
                table: "PEDIDO_ITEM",
                column: "ID_JOGO",
                principalTable: "JOGO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDO_ITEM_PEDIDO_ID_PEDIDO",
                table: "PEDIDO_ITEM",
                column: "ID_PEDIDO",
                principalTable: "PEDIDO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PEDIDO_ITEM_JOGO_ID_JOGO",
                table: "PEDIDO_ITEM");

            migrationBuilder.DropForeignKey(
                name: "FK_PEDIDO_ITEM_PEDIDO_ID_PEDIDO",
                table: "PEDIDO_ITEM");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PEDIDO_ITEM",
                table: "PEDIDO_ITEM");

            migrationBuilder.RenameTable(
                name: "PEDIDO_ITEM",
                newName: "PEDIDO_JOGO");

            migrationBuilder.RenameIndex(
                name: "IX_PEDIDO_ITEM_ID_PEDIDO",
                table: "PEDIDO_JOGO",
                newName: "IX_PEDIDO_JOGO_ID_PEDIDO");

            migrationBuilder.RenameIndex(
                name: "IX_PEDIDO_ITEM_ID_JOGO",
                table: "PEDIDO_JOGO",
                newName: "IX_PEDIDO_JOGO_ID_JOGO");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PEDIDO_JOGO",
                table: "PEDIDO_JOGO",
                column: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDO_JOGO_JOGO_ID_JOGO",
                table: "PEDIDO_JOGO",
                column: "ID_JOGO",
                principalTable: "JOGO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDO_JOGO_PEDIDO_ID_PEDIDO",
                table: "PEDIDO_JOGO",
                column: "ID_PEDIDO",
                principalTable: "PEDIDO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
