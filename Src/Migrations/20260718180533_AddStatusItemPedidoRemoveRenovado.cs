using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusItemPedidoRemoveRenovado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RENOVADO",
                table: "PEDIDO_ITEM");

            migrationBuilder.AddColumn<short>(
                name: "STATUS",
                table: "PEDIDO_ITEM",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.Sql("UPDATE PEDIDO_ITEM pi JOIN PEDIDO p ON pi.ID_PEDIDO = p.ID SET pi.STATUS = p.STATUS;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "STATUS",
                table: "PEDIDO_ITEM");

            migrationBuilder.AddColumn<bool>(
                name: "RENOVADO",
                table: "PEDIDO_ITEM",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }
    }
}
