namespace ProximoTurnoApi.Infrastructure.Backup;

/// <summary>
/// Campos da connection string necessários para montar a linha de comando do
/// mysqldump. Aceita as variações de nome que o provider do MySQL permite.
/// </summary>
public record ConexaoMySql(string Host, int Porta, string Usuario, string Senha, string Banco)
{
    private const int PortaPadrao = 3306;

    public static ConexaoMySql Parse(string connectionString)
    {
        var partes = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            // Split em 2: senhas base64 podem conter '=' e não podem ser truncadas.
            .Select(parte => parte.Split('=', 2))
            .Where(par => par.Length == 2)
            .ToDictionary(par => par[0].Trim().ToLowerInvariant(), par => par[1].Trim());

        string Obrigatorio(string rotulo, params string[] chaves)
        {
            foreach (var chave in chaves)
                if (partes.TryGetValue(chave, out var valor) && !string.IsNullOrWhiteSpace(valor))
                    return valor;

            throw new InvalidOperationException($"Connection string sem o campo '{rotulo}'.");
        }

        var porta = partes.TryGetValue("port", out var textoPorta) && int.TryParse(textoPorta, out var numero)
            ? numero
            : PortaPadrao;

        return new ConexaoMySql(
            Obrigatorio("server", "server", "host"),
            porta,
            Obrigatorio("user", "user", "user id", "uid"),
            Obrigatorio("password", "password", "pwd"),
            Obrigatorio("database", "database"));
    }
}
