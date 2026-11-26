using Vitta.Domain.Common;

namespace Vitta.Domain.Settings;

/// <summary>
/// Pagina publica de captacao (/simular): a pessoa pede contato e aceita ser procurada; vira prospect na hora.
/// Registro unico enquanto a Vitta nao e multi-corretora.
/// </summary>
public sealed class LeadCaptureSettings : AggregateRoot
{
    public static readonly Guid SingletonId = Guid.Parse("0199a000-0000-7000-8000-0000000000a3");

    private LeadCaptureSettings() { } // EF

    public bool Enabled { get; private set; }

    /// <summary>Nome que aparece na pagina e no texto de consentimento (quem vai entrar em contato).</summary>
    public string BrokerName { get; private set; } = string.Empty;

    public string Headline { get; private set; } = DefaultHeadline;

    /// <summary>WhatsApp da corretora (so digitos, com DDD), para o botao "Falar agora".</summary>
    public string? WhatsApp { get; private set; }

    public const string DefaultHeadline = "Proteja quem você ama com um seguro de vida";

    public static LeadCaptureSettings CreateDefault() => new() { Id = SingletonId };

    public Result Update(bool enabled, string? brokerName, string? headline, string? whatsApp)
    {
        var name = (brokerName ?? string.Empty).Trim();
        if (enabled && name.Length == 0)
        {
            return Error.Validation("LeadCapture.BrokerNameRequired", "Informe o nome da corretora para ligar a página.");
        }

        var phone = new string((whatsApp ?? string.Empty).Where(char.IsDigit).ToArray());
        if (phone.Length is > 0 and (< 10 or > 13))
        {
            return Error.Validation("LeadCapture.InvalidWhatsApp", "WhatsApp inválido. Use DDD + número (ex.: 21 99999-0000).");
        }

        Enabled = enabled;
        BrokerName = name.Length > 120 ? name[..120] : name;
        Headline = string.IsNullOrWhiteSpace(headline) ? DefaultHeadline : headline.Trim() is var h && h.Length > 150 ? h[..150] : headline.Trim();
        WhatsApp = phone.Length == 0 ? null : phone;
        Touch();
        return Result.Success();
    }

    /// <summary>Texto que a pessoa aceita ao enviar (fica gravado no prospect, como prova do consentimento).</summary>
    public string ConsentText =>
        $"Autorizo {BrokerName} a entrar em contato comigo por telefone, WhatsApp ou e-mail para apresentar opções de seguro de vida, " +
        "e a guardar os dados que informei para isso. Posso pedir para não ser mais contatado e excluir meus dados a qualquer momento.";
}
