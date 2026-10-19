using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Tests.Fakes;

public class FakeInscricaoPushRepository : IInscricaoPushRepository {
    public List<InscricaoPush> Inscricoes { get; } = [];
    /// <summary>Ids de usuários com o papel Admin; GetDosAdminsAsync filtra por eles.</summary>
    public HashSet<string> Admins { get; } = [];
    public int Salvamentos { get; private set; }

    public Task<InscricaoPush?> GetByEndpointAsync(string endpoint) =>
        Task.FromResult(Inscricoes.FirstOrDefault(i => i.Endpoint == endpoint));

    public Task SalvarAsync(InscricaoPush inscricao) {
        Salvamentos++;
        if (inscricao.Id == 0) {
            inscricao.Id = Inscricoes.Count == 0 ? 1 : Inscricoes.Max(i => i.Id) + 1;
            Inscricoes.Add(inscricao);
        }
        return Task.CompletedTask;
    }

    public Task<bool> RemoverAsync(string endpoint, string idUsuario) =>
        Task.FromResult(Inscricoes.RemoveAll(i => i.Endpoint == endpoint && i.IdUsuario == idUsuario) > 0);

    public Task RemoverPorIdAsync(int id) {
        Inscricoes.RemoveAll(i => i.Id == id);
        return Task.CompletedTask;
    }

    public Task<List<InscricaoPush>> GetDosAdminsAsync() =>
        Task.FromResult(Inscricoes.Where(i => Admins.Contains(i.IdUsuario)).ToList());

    /// <summary>E-mail de cada usuário, para GetDoUsuarioPorEmailAsync.</summary>
    public Dictionary<string, string> EmailsUsuarios { get; } = [];

    public Task<List<InscricaoPush>> GetDoUsuarioPorEmailAsync(string email) =>
        Task.FromResult(Inscricoes
            .Where(i => EmailsUsuarios.TryGetValue(i.IdUsuario, out var e) && string.Equals(e, email, StringComparison.OrdinalIgnoreCase))
            .ToList());
}

public class FakeEnviadorWebPush : IEnviadorWebPush {
    public List<(InscricaoPush Inscricao, string Payload)> Enviados { get; } = [];
    /// <summary>Resultado por endpoint; o que não estiver aqui é entregue.</summary>
    public Dictionary<string, ResultadoEnvioPush> Resultados { get; } = [];

    public Task<ResultadoEnvioPush> EnviarAsync(InscricaoPush inscricao, string payload) {
        Enviados.Add((inscricao, payload));
        return Task.FromResult(Resultados.GetValueOrDefault(inscricao.Endpoint, ResultadoEnvioPush.Entregue));
    }
}

public class FakePushService : IPushService {
    /// <summary>Mensagens para os admins.</summary>
    public List<MensagemPush> Mensagens { get; } = [];
    public List<(string Email, MensagemPush Mensagem)> ParaClientes { get; } = [];

    public Task EnviarParaAdminsAsync(MensagemPush mensagem) {
        Mensagens.Add(mensagem);
        return Task.CompletedTask;
    }

    public Task EnviarParaClienteAsync(string email, MensagemPush mensagem) {
        ParaClientes.Add((email, mensagem));
        return Task.CompletedTask;
    }
}

public class FakeEmailService : IEmailService {
    public List<string> Destinatarios { get; } = [];
    public Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true) {
        Destinatarios.Add(toEmail);
        return Task.CompletedTask;
    }
}
