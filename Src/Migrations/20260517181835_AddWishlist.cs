using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddWishlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LISTA_DESEJOS",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ID_CLIENTE = table.Column<int>(type: "int", nullable: false),
                    ID_JOGO = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LISTA_DESEJOS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_LISTA_DESEJOS_CLIENTE_ID_CLIENTE",
                        column: x => x.ID_CLIENTE,
                        principalTable: "CLIENTE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LISTA_DESEJOS_JOGO_ID_JOGO",
                        column: x => x.ID_JOGO,
                        principalTable: "JOGO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LISTA_DESEJOS_ID_CLIENTE_ID_JOGO",
                table: "LISTA_DESEJOS",
                columns: new[] { "ID_CLIENTE", "ID_JOGO" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LISTA_DESEJOS_ID_JOGO",
                table: "LISTA_DESEJOS",
                column: "ID_JOGO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LISTA_DESEJOS");
        }
    }
}
