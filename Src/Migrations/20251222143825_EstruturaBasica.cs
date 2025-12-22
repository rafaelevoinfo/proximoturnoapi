using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class EstruturaBasica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CATEGORIA",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DESCRICAO = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CATEGORIA", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CLIENTE",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    NOME = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TELEFONE = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ENDERECO = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EMAIL = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DATA_NASCIMENTO = table.Column<DateOnly>(type: "date", nullable: true),
                    COMO_NOS_CONHECEU = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ACEITA_RECEBER_OFERTAS = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CLIENTE", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TAG",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DESCRICAO = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TAG", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CATEGORIA_PRECO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ID_CATEGORIA = table.Column<int>(type: "int", nullable: false),
                    QUANTIDADE_DIAS = table.Column<int>(type: "int", nullable: false),
                    VALOR = table.Column<decimal>(type: "decimal(65,30)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CATEGORIA_PRECO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CATEGORIA_PRECO_CATEGORIA_ID_CATEGORIA",
                        column: x => x.ID_CATEGORIA,
                        principalTable: "CATEGORIA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "JOGO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ID_CATEGORIA = table.Column<int>(type: "int", nullable: false),
                    NOME = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DESCRICAO = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IDADE_MINIMA = table.Column<short>(type: "smallint", nullable: false),
                    FOTO = table.Column<byte[]>(type: "longblob", nullable: false),
                    MINIMO_JOGADORES = table.Column<short>(type: "smallint", nullable: false),
                    MAXIMO_JOGADORES = table.Column<short>(type: "smallint", nullable: false),
                    STATUS = table.Column<short>(type: "smallint", nullable: false),
                    TEMPO_ESTIMADO_JOGO = table.Column<TimeOnly>(type: "time(6)", nullable: true),
                    VALOR_COMPRA = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    DATA_COMPRA = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JOGO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_JOGO_CATEGORIA_ID_CATEGORIA",
                        column: x => x.ID_CATEGORIA,
                        principalTable: "CATEGORIA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PEDIDO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ID_CLIENTE = table.Column<int>(type: "int", nullable: false),
                    DATA_HORA = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    VALOR_TOTAL = table.Column<decimal>(type: "decimal(65,30)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PEDIDO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PEDIDO_CLIENTE_ID_CLIENTE",
                        column: x => x.ID_CLIENTE,
                        principalTable: "CLIENTE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "JOGO_TAG",
                columns: table => new
                {
                    ID_JOGO = table.Column<int>(type: "int", nullable: false),
                    ID_TAG = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JOGO_TAG", x => new { x.ID_JOGO, x.ID_TAG });
                    table.ForeignKey(
                        name: "FK_JOGO_TAG_JOGO_ID_JOGO",
                        column: x => x.ID_JOGO,
                        principalTable: "JOGO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JOGO_TAG_TAG_ID_TAG",
                        column: x => x.ID_TAG,
                        principalTable: "TAG",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LINK",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ID_JOGO = table.Column<int>(type: "int", nullable: false),
                    TITULO = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    URL = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LINK", x => x.ID);
                    table.ForeignKey(
                        name: "FK_LINK_JOGO_ID_JOGO",
                        column: x => x.ID_JOGO,
                        principalTable: "JOGO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PEDIDO_JOGO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ID_PEDIDO = table.Column<int>(type: "int", nullable: false),
                    ID_JOGO = table.Column<int>(type: "int", nullable: false),
                    VALOR = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DATA_DEVOLUCAO = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    STATUS = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PEDIDO_JOGO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PEDIDO_JOGO_JOGO_ID_JOGO",
                        column: x => x.ID_JOGO,
                        principalTable: "JOGO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PEDIDO_JOGO_PEDIDO_ID_PEDIDO",
                        column: x => x.ID_PEDIDO,
                        principalTable: "PEDIDO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CATEGORIA_PRECO_ID_CATEGORIA",
                table: "CATEGORIA_PRECO",
                column: "ID_CATEGORIA");

            migrationBuilder.CreateIndex(
                name: "IX_JOGO_ID_CATEGORIA",
                table: "JOGO",
                column: "ID_CATEGORIA");

            migrationBuilder.CreateIndex(
                name: "IX_JOGO_TAG_ID_TAG",
                table: "JOGO_TAG",
                column: "ID_TAG");

            migrationBuilder.CreateIndex(
                name: "IX_LINK_ID_JOGO",
                table: "LINK",
                column: "ID_JOGO");

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDO_ID_CLIENTE",
                table: "PEDIDO",
                column: "ID_CLIENTE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDO_JOGO_ID_JOGO",
                table: "PEDIDO_JOGO",
                column: "ID_JOGO");

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDO_JOGO_ID_PEDIDO",
                table: "PEDIDO_JOGO",
                column: "ID_PEDIDO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CATEGORIA_PRECO");

            migrationBuilder.DropTable(
                name: "JOGO_TAG");

            migrationBuilder.DropTable(
                name: "LINK");

            migrationBuilder.DropTable(
                name: "PEDIDO_JOGO");

            migrationBuilder.DropTable(
                name: "TAG");

            migrationBuilder.DropTable(
                name: "JOGO");

            migrationBuilder.DropTable(
                name: "PEDIDO");

            migrationBuilder.DropTable(
                name: "CATEGORIA");

            migrationBuilder.DropTable(
                name: "CLIENTE");
        }
    }
}
