using System.Text.RegularExpressions;
using Vitta.Domain.Common;

namespace Vitta.Domain.Settings;

public enum AiProvider
{
    Anthropic = 1,
    Groq = 2,
}

/// <summary>
/// Qual IA a Vitta usa (provedor, modelo e esforco). Registro unico; enquanto ninguem salva a tela de
/// configuracao ele nao existe e o servico de IA usa os padroes do proprio .env. As chaves de API NUNCA ficam
/// aqui: vivem so no .env do servico de IA.
/// </summary>
public sealed partial class AiSettings : AggregateRoot
{
    public static readonly Guid SingletonId = Guid.Parse("0199a000-0000-7000-8000-0000000000a1");
    public static readonly IReadOnlyList<string> Efforts = ["low", "medium", "high"];

    private AiSettings() { } // EF

    public AiProvider Provider { get; private set; }
    public string Model { get; private set; } = null!;
    public string Effort { get; private set; } = null!;

    public static Result<AiSettings> Create(AiProvider provider, string model, string effort)
    {
        var settings = new AiSettings { Id = SingletonId };
        var result = settings.Update(provider, model, effort);
        return result.IsFailure ? result.Error : settings;
    }

    public Result Update(AiProvider provider, string model, string effort)
    {
        if (!Enum.IsDefined(provider))
        {
            return AiSettingsErrors.InvalidProvider;
        }

        model = (model ?? string.Empty).Trim();
        if (model.Length is 0 or > 100 || !ModelId().IsMatch(model))
        {
            return AiSettingsErrors.InvalidModel;
        }

        effort = (effort ?? string.Empty).Trim().ToLowerInvariant();
        if (!Efforts.Contains(effort))
        {
            return AiSettingsErrors.InvalidEffort;
        }

        Provider = provider;
        Model = model;
        Effort = effort;
        Touch();
        return Result.Success();
    }

    /// <summary>Ids como "claude-haiku-5-5", "openai/gpt-oss-120b", "llama-3.3-70b-versatile".</summary>
    [GeneratedRegex("^[A-Za-z0-9._:/-]+$")]
    private static partial Regex ModelId();
}

public static class AiSettingsErrors
{
    public static readonly Error InvalidProvider = Error.Validation("AiSettings.InvalidProvider", "Provedor de IA inválido.");
    public static readonly Error InvalidModel = Error.Validation("AiSettings.InvalidModel", "Informe um modelo válido (ex.: claude-haiku-5-5).");
    public static readonly Error InvalidEffort = Error.Validation("AiSettings.InvalidEffort", "Esforço deve ser low, medium ou high.");
}
