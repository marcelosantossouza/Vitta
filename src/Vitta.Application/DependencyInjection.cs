using System.Globalization;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Vitta.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra validadores. O Mediator em si e registrado no host (Vitta.API) porque
    /// o source generator precisa rodar no projeto executavel.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("pt-BR");
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);
        return services;
    }
}
