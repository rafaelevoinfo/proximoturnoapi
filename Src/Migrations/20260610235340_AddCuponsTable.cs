using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCuponsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ID_CUPOM",
                table: "PEDIDO",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VALOR_DESCONTO",
                table: "PEDIDO",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CUPOM",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CODIGO = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    TIPO_DESCONTO = table.Column<short>(type: "smallint", nullable: false),
                    VALOR_DESCONTO = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DATA_INICIO = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DATA_FIM = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LIMITE_USO_GLOBAL = table.Column<int>(type: "int", nullable: true),
                    LIMITE_USO_CLIENTE = table.Column<int>(type: "int", nullable: true),
                    CONDICAO = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ATIVO = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUPOM", x => x.ID);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PEDIDO_ID_CUPOM",
                table: "PEDIDO",
                column: "ID_CUPOM");

            migrationBuilder.CreateIndex(
                name: "IX_CUPOM_CODIGO",
                table: "CUPOM",
                column: "CODIGO",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDO_CUPOM_ID_CUPOM",
                table: "PEDIDO",
                column: "ID_CUPOM",
                principalTable: "CUPOM",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PEDIDO_CUPOM_ID_CUPOM",
                table: "PEDIDO");

            migrationBuilder.DropTable(
                name: "CUPOM");

            migrationBuilder.DropIndex(
                name: "IX_PEDIDO_ID_CUPOM",
                table: "PEDIDO");

            migrationBuilder.DropColumn(
                name: "ID_CUPOM",
                table: "PEDIDO");

            migrationBuilder.DropColumn(
                name: "VALOR_DESCONTO",
                table: "PEDIDO");
        }
    }
}
