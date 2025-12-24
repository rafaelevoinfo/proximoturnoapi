using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations {
    /// <inheritdoc />
    public partial class Corrigindo_Vinculo_Pedido_Cliente : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropForeignKey(
                name: "FK_PEDIDO_CLIENTE_ID_CLIENTE",
                table: "PEDIDO");


            migrationBuilder.DropIndex(
                name: "IX_PEDIDO_ID_CLIENTE",
                table: "PEDIDO");



            migrationBuilder.CreateIndex(
                name: "IX_PEDIDO_ID_CLIENTE",
                table: "PEDIDO",
                column: "ID_CLIENTE");

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDO_CLIENTE_ID_CLIENTE",
                table: "PEDIDO",
                column: "ID_CLIENTE",
                principalTable: "CLIENTE",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropForeignKey(
               name: "FK_PEDIDO_CLIENTE_ID_CLIENTE",
               table: "PEDIDO");
            migrationBuilder.DropIndex(
                name: "IX_PEDIDO_ID_CLIENTE",
                table: "PEDIDO");

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDO_ID_CLIENTE",
                table: "PEDIDO",
                column: "ID_CLIENTE",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDO_CLIENTE_ID_CLIENTE",
                table: "PEDIDO",
                column: "ID_CLIENTE",
                principalTable: "CLIENTE",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
