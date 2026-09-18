using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class criando_tabela_jogo_link_indexacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JOGO_LINK_INDEXACAO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ID_JOGO_LINK = table.Column<int>(type: "int", nullable: false),
                    URL = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    HASH_PDF = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    STATUS = table.Column<short>(type: "smallint", nullable: false),
                    TENTATIVAS = table.Column<int>(type: "int", nullable: false),
                    ULTIMO_ERRO = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    MODELO_EXTRACAO = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    CONFIABILIDADE_EXTRACAO = table.Column<short>(type: "smallint", nullable: true),
                    MODELO_REVISAO = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    CORRECOES_APLICADAS = table.Column<int>(type: "int", nullable: true),
                    CORRECOES_DESCARTADAS = table.Column<int>(type: "int", nullable: true),
                    REVISAO_COMPLETA = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    QUANTIDADE_CHUNKS = table.Column<int>(type: "int", nullable: true),
                    DATA_ATUALIZACAO = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DATA_INDEXACAO = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JOGO_LINK_INDEXACAO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_JOGO_LINK_INDEXACAO_JOGO_LINK_ID_JOGO_LINK",
                        column: x => x.ID_JOGO_LINK,
                        principalTable: "JOGO_LINK",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_JOGO_LINK_INDEXACAO_HASH_PDF",
                table: "JOGO_LINK_INDEXACAO",
                column: "HASH_PDF");

            migrationBuilder.CreateIndex(
                name: "IX_JOGO_LINK_INDEXACAO_ID_JOGO_LINK",
                table: "JOGO_LINK_INDEXACAO",
                column: "ID_JOGO_LINK",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JOGO_LINK_INDEXACAO");
        }
    }
}
