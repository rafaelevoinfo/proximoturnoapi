using Xunit;
using ProximoTurnoApi.Application.DTOs;

namespace ProximoTurnoApi.Tests.Domain;

public class CpfUtilsTests
{
    [Theory]
    [InlineData("705.045.771-07", "70504577107")]
    [InlineData("70504577107", "70504577107")]
    [InlineData("  624.438.553-50  ", "62443855350")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    [InlineData("...---", null)]
    public void Normalizar_RemoveMascaraEDevolveNullQuandoVazio(string? entrada, string? esperado)
    {
        Assert.Equal(esperado, CpfUtils.Normalizar(entrada));
    }

    [Theory]
    [InlineData("705.045.771-07")]
    [InlineData("624.438.553-50")]
    [InlineData("051.239.571-38")]
    [InlineData("03062125101")]
    public void EhValido_AceitaCpfComDigitoVerificadorCorreto(string cpf)
    {
        Assert.True(CpfUtils.EhValido(cpf));
    }

    [Theory]
    [InlineData("705.045.771-08")]   // dígito verificador errado
    [InlineData("111.111.111-11")]   // todos os dígitos iguais
    [InlineData("00000000000")]
    [InlineData("1234567890")]       // 10 dígitos
    [InlineData("123456789012")]     // 12 dígitos
    [InlineData("")]
    [InlineData(null)]
    public void EhValido_RejeitaCpfInvalido(string? cpf)
    {
        Assert.False(CpfUtils.EhValido(cpf));
    }

    [Theory]
    [InlineData(null, true)]          // CPF é opcional
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("705.045.771-07", true)]
    [InlineData("705.045.771-08", false)]
    [InlineData("111.111.111-11", false)]
    public void CpfValidoAttribute_AceitaNuloEValidaOResto(string? cpf, bool esperado)
    {
        var attr = new CpfValidoAttribute();
        Assert.Equal(esperado, attr.IsValid(cpf));
    }

    [Fact]
    public void ClienteDTO_NormalizaCpfAoAtribuir()
    {
        var dto = new ClienteDTO { Nome = "Teste", Cpf = "705.045.771-07" };
        Assert.Equal("70504577107", dto.Cpf);
    }

    [Fact]
    public void ClienteDTO_CpfVazioViraNull()
    {
        var dto = new ClienteDTO { Nome = "Teste", Cpf = "" };
        Assert.Null(dto.Cpf);
    }
}
