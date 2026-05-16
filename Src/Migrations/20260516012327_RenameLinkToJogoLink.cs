using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class RenameLinkToJogoLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LINK_JOGO_ID_JOGO",
                table: "LINK");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LINK",
                table: "LINK");

            migrationBuilder.RenameTable(
                name: "LINK",
                newName: "JOGO_LINK");

            migrationBuilder.RenameIndex(
                name: "IX_LINK_ID_JOGO",
                table: "JOGO_LINK",
                newName: "IX_JOGO_LINK_ID_JOGO");

            migrationBuilder.AddPrimaryKey(
                name: "PK_JOGO_LINK",
                table: "JOGO_LINK",
                column: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_JOGO_LINK_JOGO_ID_JOGO",
                table: "JOGO_LINK",
                column: "ID_JOGO",
                principalTable: "JOGO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JOGO_LINK_JOGO_ID_JOGO",
                table: "JOGO_LINK");

            migrationBuilder.DropPrimaryKey(
                name: "PK_JOGO_LINK",
                table: "JOGO_LINK");

            migrationBuilder.RenameTable(
                name: "JOGO_LINK",
                newName: "LINK");

            migrationBuilder.RenameIndex(
                name: "IX_JOGO_LINK_ID_JOGO",
                table: "LINK",
                newName: "IX_LINK_ID_JOGO");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LINK",
                table: "LINK",
                column: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_LINK_JOGO_ID_JOGO",
                table: "LINK",
                column: "ID_JOGO",
                principalTable: "JOGO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
