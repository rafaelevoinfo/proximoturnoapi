using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddContratoAtivoField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CONTRATO_AUTENTIQUE_PEDIDO_ID_PEDIDO",
                table: "CONTRATO_AUTENTIQUE");

            migrationBuilder.DropIndex(
                name: "IX_CONTRATO_AUTENTIQUE_ID_PEDIDO",
                table: "CONTRATO_AUTENTIQUE");

            migrationBuilder.AddColumn<bool>(
                name: "ATIVO",
                table: "CONTRATO_AUTENTIQUE",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_CONTRATO_AUTENTIQUE_ID_PEDIDO",
                table: "CONTRATO_AUTENTIQUE",
                column: "ID_PEDIDO");

            migrationBuilder.AddForeignKey(
                name: "FK_CONTRATO_AUTENTIQUE_PEDIDO_ID_PEDIDO",
                table: "CONTRATO_AUTENTIQUE",
                column: "ID_PEDIDO",
                principalTable: "PEDIDO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CONTRATO_AUTENTIQUE_PEDIDO_ID_PEDIDO",
                table: "CONTRATO_AUTENTIQUE");

            migrationBuilder.DropIndex(
                name: "IX_CONTRATO_AUTENTIQUE_ID_PEDIDO",
                table: "CONTRATO_AUTENTIQUE");

            migrationBuilder.DropColumn(
                name: "ATIVO",
                table: "CONTRATO_AUTENTIQUE");

            migrationBuilder.CreateIndex(
                name: "IX_CONTRATO_AUTENTIQUE_ID_PEDIDO",
                table: "CONTRATO_AUTENTIQUE",
                column: "ID_PEDIDO",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CONTRATO_AUTENTIQUE_PEDIDO_ID_PEDIDO",
                table: "CONTRATO_AUTENTIQUE",
                column: "ID_PEDIDO",
                principalTable: "PEDIDO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
