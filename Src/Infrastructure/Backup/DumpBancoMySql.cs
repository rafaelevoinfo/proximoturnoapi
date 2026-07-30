using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.UseCases.Backup;

namespace ProximoTurnoApi.Infrastructure.Backup;

/// <summary>
/// Gera o dump com `mysqldump`, comprime com gzip e cifra com GPG simétrico.
/// mysqldump é cliente de rede: conecta no contêiner do MySQL pela connection
/// string, sem precisar de docker exec nem do socket do Docker — desde que a
/// conexão seja forçada para TCP (ver --protocol em GerarAsync).
/// </summary>
public class DumpBancoMySql(
    IConfiguration configuration,
    BackupOptions options,
    ILogger<DumpBancoMySql> logger) : IDumpBanco {
    public async Task<ResultadoDump> GerarAsync(CancellationToken cancellationToken) {
        var conexao = LerConexao();
        var destino = Path.Combine(Path.GetTempPath(), $"dump-{Guid.NewGuid()}.sql.gz.gpg");

        // Pipeline em shell: o dump nunca é materializado em claro no disco.
        // --single-transaction dá um snapshot consistente sem travar tabelas.
        //
        // --no-tablespaces evita que o mysqldump consulte INFORMATION_SCHEMA.FILES
        // para emitir CREATE TABLESPACE, consulta que exige o privilégio global
        // PROCESS. Sem a flag, um usuário sem PROCESS (o da aplicação) faz o
        // mysqldump escrever "Error: 'Access denied; you need ... PROCESS'" em
        // stderr a cada execução. O dump em si sai completo e idêntico — só usamos
        // tablespaces per-table do InnoDB, então nada seria emitido de todo modo —
        // mas o LogWarning de stderr abaixo passaria a disparar em todo backup,
        // afogando qualquer aviso legítimo em ruído previsível.
        //
        // --protocol=TCP não é opcional: para os clientes baseados em libmysqlclient
        // o host "localhost" significa "socket Unix local", e a porta é ignorada. O
        // MySQL roda em outro contêiner, então não existe socket no filesystem local
        // e o mysqldump falharia com "Can't connect to local MySQL server through
        // socket '/var/run/mysqld/mysqld.sock'". O provider do EF Core não tem essa
        // regra e conecta por TCP com a mesma connection string, então o sintoma
        // aparece só aqui.
        //
        // Host/usuário/banco chegam via variáveis de ambiente referenciadas como
        // "$VAR" (aspas duplas): o shell expande o valor sem reinterpretá-lo como
        // sintaxe, então uma senha ou host com aspas simples, `;` ou outro
        // metacaractere não escapa da posição de argumento nem injeta comandos.
        //
        // A senha do MySQL vai por MYSQL_PWD (lida nativamente pelo mysqldump) e a
        // passphrase do GPG por um descritor de arquivo dedicado (fd 3, alimentado
        // por variável de ambiente) — nenhuma das duas aparece em /proc/<pid>/cmdline,
        // visível para qualquer processo do contêiner.
        //
        // A passphrase NÃO pode ir para o fd 0 (stdin) do processo bash: numa pipe de
        // 3 estágios (mysqldump | gzip | gpg), só o primeiro estágio herda o stdin
        // original do shell — o fd 0 do gpg é o outro extremo do pipe vindo do gzip.
        // Isso foi reproduzido: com --passphrase-fd 0 nessa pipe, o gpg sai com
        // código 0 e gera um arquivo .gpg plausível, mas usa bytes do gzip como
        // "senha" — o arquivo nunca é decifrável com a passphrase real.
        var comando =
            "set -o pipefail; " +
            "mysqldump --single-transaction --routines --triggers --no-tablespaces " +
            "--host=\"$DB_HOST\" --port=\"$DB_PORT\" --protocol=TCP --user=\"$DB_USER\" \"$DB_NAME\" " +
            "| gzip " +
            $"| gpg --batch --yes --symmetric --cipher-algo AES256 --passphrase-fd 3 --output '{destino}' 3<<<\"$BACKUP_PASSPHRASE\"";

        var processo = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = "/bin/bash",
                ArgumentList = { "-c", comando },
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        processo.StartInfo.Environment["DB_HOST"] = conexao.Host;
        processo.StartInfo.Environment["DB_PORT"] = conexao.Porta.ToString();
        processo.StartInfo.Environment["DB_USER"] = conexao.Usuario;
        processo.StartInfo.Environment["DB_NAME"] = conexao.Banco;
        processo.StartInfo.Environment["MYSQL_PWD"] = conexao.Senha;
        processo.StartInfo.Environment["BACKUP_PASSPHRASE"] = options.Passphrase;

        try {
            processo.Start();

            var erro = await processo.StandardError.ReadToEndAsync(cancellationToken);
            await processo.WaitForExitAsync(cancellationToken);

            if (processo.ExitCode != 0)
                throw new InvalidOperationException($"mysqldump falhou (código {processo.ExitCode}): {erro}");

            if (!string.IsNullOrWhiteSpace(erro))
                logger.LogWarning("mysqldump escreveu em stderr: {Erro}", erro);

            var tamanho = new FileInfo(destino).Length;
            if (tamanho == 0)
                throw new InvalidOperationException("O dump gerado está vazio.");

            return new ResultadoDump(destino, tamanho);
        } catch {
            // Cobre falha de exit code, dump vazio e cancelamento (ex.: shutdown da
            // aplicação durante o backup noturno) num único lugar, em vez de duplicar
            // a limpeza em cada ponto de erro: mata a árvore de processos
            // (bash/mysqldump/gzip/gpg) se ainda estiver rodando e remove o arquivo
            // temporário parcial, se algum chegou a ser criado.
            try {
                if (!processo.HasExited) processo.Kill(entireProcessTree: true);
            } catch {
                // Processo pode não ter chegado a iniciar ou já ter terminado; sem ação.
            }

            if (File.Exists(destino)) File.Delete(destino);
            throw;
        }
    }

    private ConexaoMySql LerConexao() {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection não configurada.");

        return ConexaoMySql.Parse(connectionString);
    }
}
