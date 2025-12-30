using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ProximoTurnoApi.Application.Swagger;

/// <summary>
/// Filtro de operação do Swagger para adicionar o requisito de segurança (cadeado)
/// apenas às rotas que possuem o atributo [Authorize].
/// </summary>
public class SecurityRequirementsOperationFilter : IOperationFilter {
    public void Apply(OpenApiOperation operation, OperationFilterContext context) {
        // Verifica se a rota tem o atributo [AllowAnonymous], que anula a necessidade de autorização.
        var hasAnonymousAccess = context.MethodInfo.GetCustomAttributes(true)
            .OfType<AllowAnonymousAttribute>().Any();

        if (hasAnonymousAccess) {
            return; // Se a rota é anônima, não adiciona o requisito de segurança.
        }

        // Verifica se a rota ou o seu controller pai tem o atributo [Authorize].
        var hasAuthorize = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<AuthorizeAttribute>()
            .Any()
            ||
            (context.MethodInfo
                .DeclaringType?
                .GetCustomAttributes(true)
                .OfType<AuthorizeAttribute>()
                .Any() ?? false);

        if (!hasAuthorize) {
            return; // Se a rota não é protegida, não faz nada.
        }

        // Adiciona a possível resposta 401 (Unauthorized) à documentação da rota.
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });

        // Cria o requisito de segurança especificando o esquema "Bearer".
        var securityRequirement = new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer" // Este ID deve ser o mesmo definido em AddSecurityDefinition.
                    }
                },
                new string[] {}
            }
        };

        // Adiciona o requisito de segurança à operação.
        operation.Security = new List<OpenApiSecurityRequirement> { securityRequirement };
    }
}
