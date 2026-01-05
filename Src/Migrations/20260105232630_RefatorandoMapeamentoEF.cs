using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class RefatorandoMapeamentoEF : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CATEGORIA_FAIXA_PRECO_FAIXA_PRECO_ID_FAIXA_PRECO",
                table: "CATEGORIA_FAIXA_PRECO");

            migrationBuilder.DropForeignKey(
                name: "FK_JOGO_COPIA_JOGO_JogoId",
                table: "JOGO_COPIA");

            migrationBuilder.DropForeignKey(
                name: "FK_PEDIDO_CLIENTE_ID_CLIENTE",
                table: "PEDIDO");

            migrationBuilder.DropTable(
                name: "FAIXA_PRECO");

            migrationBuilder.DropIndex(
                name: "IX_JOGO_COPIA_JogoId",
                table: "JOGO_COPIA");

            migrationBuilder.DropColumn(
                name: "JogoId",
                table: "JOGO_COPIA");

            migrationBuilder.AlterColumn<decimal>(
                name: "VALOR_TOTAL",
                table: "PEDIDO",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AddColumn<DateTime>(
                name: "DATA_HORA_ENTREGA",
                table: "PEDIDO",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ID_PEDIDO_ORIGINAL",
                table: "PEDIDO",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JogosMaisAlugados",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    NOME = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FOTO = table.Column<byte[]>(type: "longblob", nullable: false),
                    QTDE = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JogosMaisAlugados", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PERIODO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    QUANTIDADE_DIAS = table.Column<int>(type: "int", nullable: false),
                    VALOR = table.Column<decimal>(type: "decimal(65,30)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PERIODO", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDO_ID_PEDIDO_ORIGINAL",
                table: "PEDIDO",
                column: "ID_PEDIDO_ORIGINAL");

            migrationBuilder.AddForeignKey(
                name: "FK_CATEGORIA_FAIXA_PRECO_PERIODO_ID_FAIXA_PRECO",
                table: "CATEGORIA_FAIXA_PRECO",
                column: "ID_FAIXA_PRECO",
                principalTable: "PERIODO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDO_CLIENTE_ID_CLIENTE",
                table: "PEDIDO",
                column: "ID_CLIENTE",
                principalTable: "CLIENTE",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDO_PEDIDO_ID_PEDIDO_ORIGINAL",
                table: "PEDIDO",
                column: "ID_PEDIDO_ORIGINAL",
                principalTable: "PEDIDO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CATEGORIA_FAIXA_PRECO_PERIODO_ID_FAIXA_PRECO",
                table: "CATEGORIA_FAIXA_PRECO");

            migrationBuilder.DropForeignKey(
                name: "FK_PEDIDO_CLIENTE_ID_CLIENTE",
                table: "PEDIDO");

            migrationBuilder.DropForeignKey(
                name: "FK_PEDIDO_PEDIDO_ID_PEDIDO_ORIGINAL",
                table: "PEDIDO");

            migrationBuilder.DropTable(
                name: "JogosMaisAlugados");

            migrationBuilder.DropTable(
                name: "PERIODO");

            migrationBuilder.DropIndex(
                name: "IX_PEDIDO_ID_PEDIDO_ORIGINAL",
                table: "PEDIDO");

            migrationBuilder.DropColumn(
                name: "DATA_HORA_ENTREGA",
                table: "PEDIDO");

            migrationBuilder.DropColumn(
                name: "ID_PEDIDO_ORIGINAL",
                table: "PEDIDO");

            migrationBuilder.AlterColumn<decimal>(
                name: "VALOR_TOTAL",
                table: "PEDIDO",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<int>(
                name: "JogoId",
                table: "JOGO_COPIA",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "FAIXA_PRECO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    QUANTIDADE_DIAS = table.Column<int>(type: "int", nullable: false),
                    VALOR = table.Column<decimal>(type: "decimal(65,30)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FAIXA_PRECO", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_JOGO_COPIA_JogoId",
                table: "JOGO_COPIA",
                column: "JogoId");

            migrationBuilder.AddForeignKey(
                name: "FK_CATEGORIA_FAIXA_PRECO_FAIXA_PRECO_ID_FAIXA_PRECO",
                table: "CATEGORIA_FAIXA_PRECO",
                column: "ID_FAIXA_PRECO",
                principalTable: "FAIXA_PRECO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_JOGO_COPIA_JOGO_JogoId",
                table: "JOGO_COPIA",
                column: "JogoId",
                principalTable: "JOGO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDO_CLIENTE_ID_CLIENTE",
                table: "PEDIDO",
                column: "ID_CLIENTE",
                principalTable: "CLIENTE",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
