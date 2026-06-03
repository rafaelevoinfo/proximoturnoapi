using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations;

/// <inheritdoc />
public partial class RefatorarComentarios : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_COMENTARIO_PEDIDO_ID_PEDIDO",
            table: "COMENTARIO");

        migrationBuilder.DropIndex(
            name: "IX_COMENTARIO_ID_PEDIDO_ID_JOGO",
            table: "COMENTARIO");

        // 1. Create the new unique index first, so that the foreign key IX_COMENTARIO_ID_CLIENTE remains backed by an index.
        migrationBuilder.CreateIndex(
            name: "IX_COMENTARIO_ID_CLIENTE_ID_JOGO",
            table: "COMENTARIO",
            columns: new[] { "ID_CLIENTE", "ID_JOGO" },
            unique: true);

        // 2. Now safe to drop the old index.
        migrationBuilder.DropIndex(
            name: "IX_COMENTARIO_ID_CLIENTE",
            table: "COMENTARIO");

        migrationBuilder.DropColumn(
            name: "ID_PEDIDO",
            table: "COMENTARIO");

        migrationBuilder.AlterColumn<short>(
            name: "NOTA",
            table: "COMENTARIO",
            type: "smallint",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "int");

        migrationBuilder.AddColumn<int>(
            name: "STATUS",
            table: "COMENTARIO",
            type: "int",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // 1. Add ID_PEDIDO back.
        migrationBuilder.AddColumn<int>(
            name: "ID_PEDIDO",
            table: "COMENTARIO",
            type: "int",
            nullable: false,
            defaultValue: 0);

        // 2. Create the old index first, so that we have an index backing the client foreign key before we drop the composite one.
        migrationBuilder.CreateIndex(
            name: "IX_COMENTARIO_ID_CLIENTE",
            table: "COMENTARIO",
            column: "ID_CLIENTE");

        // 3. Now safe to drop the composite index.
        migrationBuilder.DropIndex(
            name: "IX_COMENTARIO_ID_CLIENTE_ID_JOGO",
            table: "COMENTARIO");

        migrationBuilder.DropColumn(
            name: "STATUS",
            table: "COMENTARIO");

        migrationBuilder.AlterColumn<int>(
            name: "NOTA",
            table: "COMENTARIO",
            type: "int",
            nullable: false,
            oldClrType: typeof(short),
            oldType: "smallint");

        migrationBuilder.CreateIndex(
            name: "IX_COMENTARIO_ID_PEDIDO_ID_JOGO",
            table: "COMENTARIO",
            columns: new[] { "ID_PEDIDO", "ID_JOGO" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_COMENTARIO_PEDIDO_ID_PEDIDO",
            table: "COMENTARIO",
            column: "ID_PEDIDO",
            principalTable: "PEDIDO",
            principalColumn: "ID",
            onDelete: ReferentialAction.Restrict);
    }
}
