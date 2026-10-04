using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProximoTurnoApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStatusIndisponivel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O status 3 (Indisponivel) saiu do enum: o código nunca o gravou em cópia, mas
            // uma linha com ele (SQL manual, versão antiga) passa a "Em manutenção" (6), o
            // status manual mais próximo de "não alugável", em vez de virar um valor sem nome.
            migrationBuilder.Sql("UPDATE JOGO_COPIA SET STATUS = 6 WHERE STATUS = 3;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem volta: depois do Up não dá para distinguir as cópias que eram 3 das que o
            // admin colocou em manutenção.
        }
    }
}
