using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class faixapreco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CATEGORIA_PRECO");

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

            migrationBuilder.CreateTable(
                name: "CATEGORIA_FAIXA_PRECO",
                columns: table => new
                {
                    ID_CATEGORIA = table.Column<int>(type: "int", nullable: false),
                    ID_FAIXA_PRECO = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CATEGORIA_FAIXA_PRECO", x => new { x.ID_CATEGORIA, x.ID_FAIXA_PRECO });
                    table.ForeignKey(
                        name: "FK_CATEGORIA_FAIXA_PRECO_CATEGORIA_ID_CATEGORIA",
                        column: x => x.ID_CATEGORIA,
                        principalTable: "CATEGORIA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CATEGORIA_FAIXA_PRECO_FAIXA_PRECO_ID_FAIXA_PRECO",
                        column: x => x.ID_FAIXA_PRECO,
                        principalTable: "FAIXA_PRECO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CATEGORIA_FAIXA_PRECO_ID_FAIXA_PRECO",
                table: "CATEGORIA_FAIXA_PRECO",
                column: "ID_FAIXA_PRECO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CATEGORIA_FAIXA_PRECO");

            migrationBuilder.DropTable(
                name: "FAIXA_PRECO");

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

            migrationBuilder.CreateIndex(
                name: "IX_CATEGORIA_PRECO_ID_CATEGORIA",
                table: "CATEGORIA_PRECO",
                column: "ID_CATEGORIA");
        }
    }
}
