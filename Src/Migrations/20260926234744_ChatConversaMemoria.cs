using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class ChatConversaMemoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CHAT_CONVERSA",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CHAVE = table.Column<Guid>(type: "char(36)", nullable: false),
                    ID_USUARIO = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    ID_JOGO = table.Column<int>(type: "int", nullable: false),
                    SESSAO = table.Column<string>(type: "longtext", nullable: true),
                    DATA_CRIACAO = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DATA_ATUALIZACAO = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CHAT_CONVERSA", x => x.ID);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CHAT_MENSAGEM",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ID_CONVERSA = table.Column<int>(type: "int", nullable: false),
                    MOMENTO = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PERGUNTA = table.Column<string>(type: "text", nullable: false),
                    RESPOSTA = table.Column<string>(type: "text", nullable: true),
                    TIPO = table.Column<short>(type: "smallint", nullable: false),
                    TRECHOS = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CHAT_MENSAGEM", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CHAT_MENSAGEM_CHAT_CONVERSA_ID_CONVERSA",
                        column: x => x.ID_CONVERSA,
                        principalTable: "CHAT_CONVERSA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CHAT_CONVERSA_CHAVE",
                table: "CHAT_CONVERSA",
                column: "CHAVE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CHAT_CONVERSA_ID_USUARIO",
                table: "CHAT_CONVERSA",
                column: "ID_USUARIO");

            migrationBuilder.CreateIndex(
                name: "IX_CHAT_MENSAGEM_ID_CONVERSA",
                table: "CHAT_MENSAGEM",
                column: "ID_CONVERSA");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CHAT_MENSAGEM");

            migrationBuilder.DropTable(
                name: "CHAT_CONVERSA");
        }
    }
}
