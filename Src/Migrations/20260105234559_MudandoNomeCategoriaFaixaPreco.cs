using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations {
    /// <inheritdoc />
    public partial class MudandoNomeCategoriaFaixaPreco : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "CATEGORIA_FAIXA_PRECO");

            migrationBuilder.CreateTable(
                name: "CATEGORIA_PERIODO",
                columns: table => new {
                    ID_CATEGORIA = table.Column<int>(type: "int", nullable: false),
                    ID_PERIODO = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_CATEGORIA_PERIODO", x => new { x.ID_CATEGORIA, x.ID_PERIODO });
                    table.ForeignKey(
                        name: "FK_CATEGORIA_PERIODO_CATEGORIA_ID_CATEGORIA",
                        column: x => x.ID_CATEGORIA,
                        principalTable: "CATEGORIA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CATEGORIA_PERIODO_PERIODO_ID_PERIODO",
                        column: x => x.ID_PERIODO,
                        principalTable: "PERIODO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CATEGORIA_PERIODO_ID_PERIODO",
                table: "CATEGORIA_PERIODO",
                column: "ID_PERIODO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "CATEGORIA_PERIODO");

            migrationBuilder.CreateTable(
                name: "CATEGORIA_FAIXA_PRECO",
                columns: table => new {
                    ID_CATEGORIA = table.Column<int>(type: "int", nullable: false),
                    ID_FAIXA_PRECO = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_CATEGORIA_FAIXA_PRECO", x => new { x.ID_CATEGORIA, x.ID_FAIXA_PRECO });
                    table.ForeignKey(
                        name: "FK_CATEGORIA_FAIXA_PRECO_CATEGORIA_ID_CATEGORIA",
                        column: x => x.ID_CATEGORIA,
                        principalTable: "CATEGORIA",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CATEGORIA_FAIXA_PRECO_PERIODO_ID_FAIXA_PRECO",
                        column: x => x.ID_FAIXA_PRECO,
                        principalTable: "PERIODO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");


            migrationBuilder.CreateIndex(
                name: "IX_CATEGORIA_FAIXA_PRECO_ID_FAIXA_PRECO",
                table: "CATEGORIA_FAIXA_PRECO",
                column: "ID_FAIXA_PRECO");
        }
    }
}
