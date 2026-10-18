using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class InscricaoPush : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "INSCRICAO_PUSH",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ID_USUARIO = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    ENDPOINT = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: false),
                    P256DH = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    AUTH = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    USER_AGENT = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    DATA_CRIACAO = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INSCRICAO_PUSH", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INSCRICAO_PUSH_AspNetUsers_ID_USUARIO",
                        column: x => x.ID_USUARIO,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_INSCRICAO_PUSH_ENDPOINT",
                table: "INSCRICAO_PUSH",
                column: "ENDPOINT",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INSCRICAO_PUSH_ID_USUARIO",
                table: "INSCRICAO_PUSH",
                column: "ID_USUARIO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "INSCRICAO_PUSH");
        }
    }
}
