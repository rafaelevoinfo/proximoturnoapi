using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class UsoLlm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "USO_LLM",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    MOMENTO = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TRACE_ID = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true),
                    OPERACAO = table.Column<short>(type: "smallint", nullable: false),
                    ID_JOGO = table.Column<int>(type: "int", nullable: true),
                    ID_JOGO_LINK = table.Column<int>(type: "int", nullable: true),
                    ALVO = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    MODELO_PEDIDO = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    MODELO_RESPONDEU = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    PROVIDER = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    TOKENS_ENTRADA = table.Column<int>(type: "int", nullable: false),
                    TOKENS_SAIDA = table.Column<int>(type: "int", nullable: false),
                    TOKENS_RACIOCINIO = table.Column<int>(type: "int", nullable: false),
                    TOKENS_CACHE = table.Column<int>(type: "int", nullable: false),
                    CUSTO_USD = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: true),
                    DURACAO_MS = table.Column<int>(type: "int", nullable: false),
                    DESFECHO = table.Column<short>(type: "smallint", nullable: false),
                    DETALHE = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    ID_GERACAO = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USO_LLM", x => x.ID);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_USO_LLM_ID_JOGO_LINK",
                table: "USO_LLM",
                column: "ID_JOGO_LINK");

            migrationBuilder.CreateIndex(
                name: "IX_USO_LLM_MOMENTO",
                table: "USO_LLM",
                column: "MOMENTO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "USO_LLM");
        }
    }
}
