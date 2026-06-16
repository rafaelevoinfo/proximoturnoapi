using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddContratoAutentique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CONTRATO_AUTENTIQUE",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ID_PEDIDO = table.Column<int>(type: "int", nullable: false),
                    AUTENTIQUE_DOCUMENT_ID = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    AUTENTIQUE_PUBLIC_ID = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    LINK_ASSINATURA = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    STATUS = table.Column<short>(type: "smallint", nullable: false),
                    DATA_CRIACAO = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DATA_ASSINATURA = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONTRATO_AUTENTIQUE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CONTRATO_AUTENTIQUE_PEDIDO_ID_PEDIDO",
                        column: x => x.ID_PEDIDO,
                        principalTable: "PEDIDO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CONTRATO_AUTENTIQUE_AUTENTIQUE_DOCUMENT_ID",
                table: "CONTRATO_AUTENTIQUE",
                column: "AUTENTIQUE_DOCUMENT_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CONTRATO_AUTENTIQUE_ID_PEDIDO",
                table: "CONTRATO_AUTENTIQUE",
                column: "ID_PEDIDO",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CONTRATO_AUTENTIQUE");
        }
    }
}
