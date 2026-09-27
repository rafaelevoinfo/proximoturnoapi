using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class ChatRegrasUsoLlmUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "COTACAO_USD_BRL",
                table: "USO_LLM",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CUSTO_BRL",
                table: "USO_LLM",
                type: "decimal(18,10)",
                precision: 18,
                scale: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ID_USUARIO",
                table: "USO_LLM",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_USO_LLM_ID_USUARIO",
                table: "USO_LLM",
                column: "ID_USUARIO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_USO_LLM_ID_USUARIO",
                table: "USO_LLM");

            migrationBuilder.DropColumn(
                name: "COTACAO_USD_BRL",
                table: "USO_LLM");

            migrationBuilder.DropColumn(
                name: "CUSTO_BRL",
                table: "USO_LLM");

            migrationBuilder.DropColumn(
                name: "ID_USUARIO",
                table: "USO_LLM");
        }
    }
}
