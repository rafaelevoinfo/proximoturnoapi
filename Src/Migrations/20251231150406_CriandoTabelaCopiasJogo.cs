using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class CriandoTabelaCopiasJogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PEDIDO_ITEM_JOGO_ID_JOGO",
                table: "PEDIDO_ITEM");

            migrationBuilder.DropColumn(
                name: "STATUS",
                table: "JOGO");

            migrationBuilder.RenameColumn(
                name: "ID_JOGO",
                table: "PEDIDO_ITEM",
                newName: "ID_JOGO_COPIA");

            migrationBuilder.RenameIndex(
                name: "IX_PEDIDO_ITEM_ID_JOGO",
                table: "PEDIDO_ITEM",
                newName: "IX_PEDIDO_ITEM_ID_JOGO_COPIA");

            migrationBuilder.CreateTable(
                name: "JOGO_COPIA",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ID_JOGO = table.Column<int>(type: "int", nullable: false),
                    STATUS = table.Column<short>(type: "smallint", nullable: false),
                    JogoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JOGO_COPIA", x => x.ID);
                    table.ForeignKey(
                        name: "FK_JOGO_COPIA_JOGO_ID_JOGO",
                        column: x => x.ID_JOGO,
                        principalTable: "JOGO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JOGO_COPIA_JOGO_JogoId",
                        column: x => x.JogoId,
                        principalTable: "JOGO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_JOGO_COPIA_ID_JOGO",
                table: "JOGO_COPIA",
                column: "ID_JOGO");

            migrationBuilder.CreateIndex(
                name: "IX_JOGO_COPIA_JogoId",
                table: "JOGO_COPIA",
                column: "JogoId");

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDO_ITEM_JOGO_COPIA_ID_JOGO_COPIA",
                table: "PEDIDO_ITEM",
                column: "ID_JOGO_COPIA",
                principalTable: "JOGO_COPIA",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PEDIDO_ITEM_JOGO_COPIA_ID_JOGO_COPIA",
                table: "PEDIDO_ITEM");

            migrationBuilder.DropTable(
                name: "JOGO_COPIA");

            migrationBuilder.RenameColumn(
                name: "ID_JOGO_COPIA",
                table: "PEDIDO_ITEM",
                newName: "ID_JOGO");

            migrationBuilder.RenameIndex(
                name: "IX_PEDIDO_ITEM_ID_JOGO_COPIA",
                table: "PEDIDO_ITEM",
                newName: "IX_PEDIDO_ITEM_ID_JOGO");

            migrationBuilder.AddColumn<short>(
                name: "STATUS",
                table: "JOGO",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddForeignKey(
                name: "FK_PEDIDO_ITEM_JOGO_ID_JOGO",
                table: "PEDIDO_ITEM",
                column: "ID_JOGO",
                principalTable: "JOGO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
