using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class UniqueConstrainstCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CLIENTE_EMAIL",
                table: "CLIENTE",
                column: "EMAIL",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CLIENTE_TELEFONE",
                table: "CLIENTE",
                column: "TELEFONE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CLIENTE_EMAIL",
                table: "CLIENTE");

            migrationBuilder.DropIndex(
                name: "IX_CLIENTE_TELEFONE",
                table: "CLIENTE");
        }
    }
}
