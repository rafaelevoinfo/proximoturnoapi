using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations {
    /// <inheritdoc />
    public partial class RefatorandoCategoriaAndPeriodos : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropForeignKey(
                name: "FK_CATEGORIA_PERIODO_PERIODO_ID_PERIODO",
                table: "CATEGORIA_PERIODO");

            migrationBuilder.DropTable(
                name: "PERIODO");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CATEGORIA_PERIODO",
                table: "CATEGORIA_PERIODO");

            migrationBuilder.DropIndex(
                name: "IX_CATEGORIA_PERIODO_ID_PERIODO",
                table: "CATEGORIA_PERIODO");

            migrationBuilder.RenameColumn(
                name: "ID_PERIODO",
                table: "CATEGORIA_PERIODO",
                newName: "QTDE_DIAS");

            migrationBuilder.AddColumn<int>(
                name: "ID",
                table: "CATEGORIA_PERIODO",
                type: "int",
                nullable: false)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<decimal>(
                name: "VALOR",
                table: "CATEGORIA_PERIODO",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddPrimaryKey(
                name: "PK_CATEGORIA_PERIODO",
                table: "CATEGORIA_PERIODO",
                column: "ID");

            migrationBuilder.CreateIndex(
                name: "IX_CATEGORIA_PERIODO_ID_CATEGORIA",
                table: "CATEGORIA_PERIODO",
                column: "ID_CATEGORIA");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CATEGORIA_PERIODO",
                table: "CATEGORIA_PERIODO");

            migrationBuilder.DropIndex(
                name: "IX_CATEGORIA_PERIODO_ID_CATEGORIA",
                table: "CATEGORIA_PERIODO");

            migrationBuilder.DropColumn(
                name: "ID",
                table: "CATEGORIA_PERIODO");

            migrationBuilder.DropColumn(
                name: "VALOR",
                table: "CATEGORIA_PERIODO");

            migrationBuilder.RenameColumn(
                name: "QTDE_DIAS",
                table: "CATEGORIA_PERIODO",
                newName: "ID_PERIODO");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CATEGORIA_PERIODO",
                table: "CATEGORIA_PERIODO",
                columns: new[] { "ID_CATEGORIA", "ID_PERIODO" });

            migrationBuilder.CreateTable(
                name: "PERIODO",
                columns: table => new {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    QUANTIDADE_DIAS = table.Column<int>(type: "int", nullable: false),
                    VALOR = table.Column<decimal>(type: "decimal(65,30)", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_PERIODO", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CATEGORIA_PERIODO_ID_PERIODO",
                table: "CATEGORIA_PERIODO",
                column: "ID_PERIODO");

            migrationBuilder.AddForeignKey(
                name: "FK_CATEGORIA_PERIODO_PERIODO_ID_PERIODO",
                table: "CATEGORIA_PERIODO",
                column: "ID_PERIODO",
                principalTable: "PERIODO",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
