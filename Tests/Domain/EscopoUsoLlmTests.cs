using ProximoTurnoApi.Application.UseCases.IA;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class EscopoUsoLlmTests {

    [Fact]
    public void SemEscopo_AtualEhNulo() {
        Assert.Null(EscopoUsoLlm.Atual);
    }

    [Fact]
    public void DentroDoEscopo_AtualTemOAlvo() {
        using (EscopoUsoLlm.Abrir(17, 14, "Balde De Caranguejo / FAQ")) {
            Assert.Equal(17, EscopoUsoLlm.Atual?.IdJogo);
            Assert.Equal(14, EscopoUsoLlm.Atual?.IdJogoLink);
            Assert.Equal("Balde De Caranguejo / FAQ", EscopoUsoLlm.Atual?.Alvo);
        }

        Assert.Null(EscopoUsoLlm.Atual);
    }

    [Fact]
    public void EscopoAninhado_FecharRestauraOAnterior() {
        using (EscopoUsoLlm.Abrir(1, 8, "externo")) {
            using (EscopoUsoLlm.Abrir(2, 9, "interno")) {
                Assert.Equal(9, EscopoUsoLlm.Atual?.IdJogoLink);
            }

            Assert.Equal(8, EscopoUsoLlm.Atual?.IdJogoLink);
        }
    }

    // Trava do item 4 do Review Focus: se uma excecao deixasse o escopo aberto, o custo do
    // manual seguinte seria gravado no jogo errado - pior que nao registrar, porque mente.
    [Fact]
    public void ExcecaoDentroDoEscopo_NaoVazaParaOProximo() {
        // Metodo local em vez de lambda: um lambda de bloco que termina em throw casa com a
        // sobrecarga obsoleta Assert.Throws<T>(Func<Task>) e o compilador recusa.
        void Falhar() {
            using var _ = EscopoUsoLlm.Abrir(17, 14, "manual que falha");
            throw new InvalidOperationException("falha no meio do pipeline");
        }

        Assert.Throws<InvalidOperationException>(Falhar);

        Assert.Null(EscopoUsoLlm.Atual);
    }

    [Fact]
    public async Task EscopoAtravessaAwait() {
        using var _ = EscopoUsoLlm.Abrir(17, 14, "Balde De Caranguejo / FAQ");

        await Task.Yield();
        await Task.Delay(1);

        Assert.Equal(14, EscopoUsoLlm.Atual?.IdJogoLink);
    }

    [Fact]
    public void DescartarDuasVezes_NaoDesfazOEscopoDeFora() {
        using (EscopoUsoLlm.Abrir(1, 8, "externo")) {
            var interno = EscopoUsoLlm.Abrir(2, 9, "interno");
            interno.Dispose();
            interno.Dispose();

            Assert.Equal(8, EscopoUsoLlm.Atual?.IdJogoLink);
        }
    }
}
