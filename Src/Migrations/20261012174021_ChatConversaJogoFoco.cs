using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class ChatConversaJogoFoco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ID_JOGO_FOCO",
                table: "CHAT_CONVERSA",
                type: "int",
                nullable: true);

            // Conversas em andamento comecam com o foco no jogo da pagina em que foram abertas.
            migrationBuilder.Sql("UPDATE CHAT_CONVERSA SET ID_JOGO_FOCO = ID_JOGO WHERE ID_JOGO IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ID_JOGO_FOCO",
                table: "CHAT_CONVERSA");
        }
    }
}
