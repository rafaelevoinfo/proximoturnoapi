using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarComentarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "COMENTARIO",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ID_PEDIDO = table.Column<int>(type: "int", nullable: false),
                    ID_JOGO = table.Column<int>(type: "int", nullable: false),
                    ID_CLIENTE = table.Column<int>(type: "int", nullable: false),
                    TEXTO = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    NOTA = table.Column<int>(type: "int", nullable: false),
                    DATA_HORA = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMENTARIO", x => x.ID);
                    table.ForeignKey(
                        name: "FK_COMENTARIO_CLIENTE_ID_CLIENTE",
                        column: x => x.ID_CLIENTE,
                        principalTable: "CLIENTE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COMENTARIO_JOGO_ID_JOGO",
                        column: x => x.ID_JOGO,
                        principalTable: "JOGO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COMENTARIO_PEDIDO_ID_PEDIDO",
                        column: x => x.ID_PEDIDO,
                        principalTable: "PEDIDO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_COMENTARIO_ID_CLIENTE",
                table: "COMENTARIO",
                column: "ID_CLIENTE");

            migrationBuilder.CreateIndex(
                name: "IX_COMENTARIO_ID_JOGO",
                table: "COMENTARIO",
                column: "ID_JOGO");

            migrationBuilder.CreateIndex(
                name: "IX_COMENTARIO_ID_PEDIDO_ID_JOGO",
                table: "COMENTARIO",
                columns: new[] { "ID_PEDIDO", "ID_JOGO" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "COMENTARIO");
        }
    }
}
