using System.ComponentModel.DataAnnotations;

namespace ProximoTurnoApi.Application.DTOs;

public abstract class CpfUtils {
    /// <summary>Remove máscara e devolve apenas os dígitos. Strings vazias viram null.</summary>
    public static string? Normalizar(string? input) {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var digitos = new string([.. input.Where(char.IsDigit)]);
        return digitos.Length == 0 ? null : digitos;
    }

    public static bool EhValido(string? cpf) {
        cpf = Normalizar(cpf);
        if (cpf is null || cpf.Length != 11) return false;
        if (cpf.All(c => c == cpf[0])) return false;

        foreach (var tamanho in (int[])[9, 10]) {
            var soma = 0;
            for (var i = 0; i < tamanho; i++) {
                soma += (cpf[i] - '0') * (tamanho + 1 - i);
            }
            var digito = soma * 10 % 11;
            if (digito == 10) digito = 0;
            if (digito != cpf[tamanho] - '0') return false;
        }
        return true;
    }
}

/// <summary>Valida o dígito verificador do CPF. Aceita null/vazio: o CPF é opcional.</summary>
public class CpfValidoAttribute : ValidationAttribute {
    public override bool IsValid(object? value) {
        if (value is null) return true;
        if (value is not string cpf || string.IsNullOrWhiteSpace(cpf)) return true;
        return CpfUtils.EhValido(cpf);
    }
}
