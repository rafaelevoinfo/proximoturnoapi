using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddMetodosPagamentoEEntrega : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "METODO_ENTREGA",
                table: "PEDIDO",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "METODO_PAGAMENTO",
                table: "PEDIDO",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "METODO_ENTREGA",
                table: "PEDIDO");

            migrationBuilder.DropColumn(
                name: "METODO_PAGAMENTO",
                table: "PEDIDO");
        }
    }
}
