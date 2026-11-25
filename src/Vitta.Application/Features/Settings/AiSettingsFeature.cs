using FluentValidation;
using Mediator;
using Vitta.Application.Abstractions.Ai;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Common;
using Vitta.Domain.Settings;

namespace Vitta.Application.Features.Settings;

/// <param name="Provider">Provedor salvo ("anthropic"/"groq"); nulo = padrao do servico de IA.</param>
/// <param name="ServiceOnline">Servico de IA respondeu. Fora do ar, a Vitta usa a heuristica por nomes de coluna.</param>
public sealed record AiSettingsDto(
    string? Provider, string? Model, string? Effort, bool IsCustom, bool ServiceOnline,
    string? DefaultProvider, string? DefaultEffort, IReadOnlyList<AiProviderInfo> Providers);

internal static class AiSettingsMapping
{
    public static string ToId(this AiProvider provider) => provider.ToString().ToLowerInvariant();

    public static LlmChoice ToChoice(this AiSettings s) => new(s.Provider.ToId(), s.Model, s.Effort);
}

// ---------- Consultar ----------

public sealed record GetAiSettingsQuery : IQuery<AiSettingsDto>;

public sealed class GetAiSettingsHandler(IAiSettingsRepository settings, IAiService ai) : IQueryHandler<GetAiSettingsQuery, AiSettingsDto>
{
    public async ValueTask<AiSettingsDto> Handle(GetAiSettingsQuery query, CancellationToken ct)
    {
        var saved = await settings.GetAsync(ct);
        AiProvidersInfo? info = null;
        try
        {
            info = await ai.GetProvidersAsync(ct);
        }
        catch (AiUnavailableException)
        {
            // Servico fora do ar: a tela mostra o aviso e ainda permite salvar.
        }

        return new AiSettingsDto(
            saved?.Provider.ToId(), saved?.Model, saved?.Effort, saved is not null, info is not null,
            info?.DefaultProvider, info?.DefaultEffort, info?.Providers ?? []);
    }
}

// ---------- Salvar ----------

public sealed record UpdateAiSettingsCommand(AiProvider Provider, string Model, string Effort) : ICommand<Result>;

internal sealed class UpdateAiSettingsValidator : AbstractValidator<UpdateAiSettingsCommand>
{
    public UpdateAiSettingsValidator()
    {
        RuleFor(x => x.Provider).IsInEnum().WithName("Provedor");
        RuleFor(x => x.Model).NotEmpty().MaximumLength(100).WithName("Modelo");
        RuleFor(x => x.Effort).NotEmpty().WithName("Esforço");
    }
}

public sealed class UpdateAiSettingsHandler(IAiSettingsRepository settings, IUnitOfWork uow) : ICommandHandler<UpdateAiSettingsCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateAiSettingsCommand c, CancellationToken ct)
    {
        var current = await settings.GetAsync(ct);
        if (current is null)
        {
            var created = AiSettings.Create(c.Provider, c.Model, c.Effort);
            if (created.IsFailure)
            {
                return created.Error;
            }

            settings.Add(created.Value);
        }
        else
        {
            var updated = current.Update(c.Provider, c.Model, c.Effort);
            if (updated.IsFailure)
            {
                return updated;
            }
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---------- Testar ----------

public sealed record TestAiConnectionCommand(AiProvider Provider, string Model, string Effort) : ICommand<AiConnectionTest>;

public sealed class TestAiConnectionHandler(IAiService ai) : ICommandHandler<TestAiConnectionCommand, AiConnectionTest>
{
    public async ValueTask<AiConnectionTest> Handle(TestAiConnectionCommand c, CancellationToken ct)
    {
        var choice = new LlmChoice(c.Provider.ToId(), (c.Model ?? string.Empty).Trim(), (c.Effort ?? "medium").Trim().ToLowerInvariant());
        try
        {
            return await ai.TestConnectionAsync(choice, ct);
        }
        catch (AiUnavailableException)
        {
            return new AiConnectionTest(false, choice.Provider, choice.Model, null,
                "O serviço de IA (Vitta.AI) não respondeu. Confira se ele está rodando.");
        }
    }
}
