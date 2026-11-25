using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Vitta.Application.Abstractions.Notifications;
using Vitta.Application.Features.Dashboard;
using Vitta.Application.Features.Policies;
using Vitta.Domain.Prospects;

namespace Vitta.Application.Features.Notifications;

/// <summary>
/// Monta o e-mail diario: numeros da carteira, follow-ups de hoje (e atrasados) e saidas recentes.
/// Nao inclui CPF: e-mail passa por servidores de terceiros.
/// </summary>
public static class DailyDigest
{
    private const int MaxFollowUps = 15;
    private const int MaxExits = 10;
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    // Escapa so o perigoso (<, >, &, aspas); acentos ficam legiveis no HTML.
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    public static EmailMessage Build(IReadOnlyList<string> to, DashboardDto d, BookSummaryDto book, DateOnly today, string? appUrl)
    {
        var followUps = d.FollowUpsDue.Take(MaxFollowUps).ToList();
        var exits = d.RecentExits.Take(MaxExits).ToList();
        var subject = $"Vitta · Resumo de {today.ToString("dd/MM", PtBr)} — {Plural(d.FollowUpsDue.Count, "contato", "contatos")} para hoje";
        var url = string.IsNullOrWhiteSpace(appUrl) ? null : appUrl.TrimEnd('/');

        var html = new StringBuilder();
        var text = new StringBuilder();
        html.Append("""<div style="font-family:Arial,Helvetica,sans-serif;color:#1f2937;max-width:640px;margin:0 auto">""");
        html.Append($"""<h2 style="color:#0f6e5c;margin:0 0 4px">Resumo do dia</h2><p style="margin:0 0 16px;color:#6b7280">{E(today.ToString("dddd, dd 'de' MMMM 'de' yyyy", PtBr))}</p>""");
        text.AppendLine($"Resumo do dia — {today.ToString("dd/MM/yyyy", PtBr)}").AppendLine();

        // Carteira
        var commissionNote = book.ActiveWithoutRate > 0 ? $" (sem contar {Plural(book.ActiveWithoutRate, "apólice", "apólices")} sem %)" : string.Empty;
        html.Append(Section("Carteira"));
        html.Append("<table style=\"border-collapse:collapse;width:100%\">");
        html.Append(Row("Apólices ativas", book.ActivePolicies.ToString(PtBr)));
        html.Append(Row("Clientes com apólice ativa", book.ClientsWithActivePolicies.ToString(PtBr)));
        html.Append(Row("Prêmio mensal", Money(book.MonthlyPremium)));
        html.Append(Row("Sua comissão mensal", $"<strong>{Money(book.MonthlyCommission)}</strong>{E(commissionNote)}"));
        html.Append("</table>");
        text.AppendLine("CARTEIRA")
            .AppendLine($"Apólices ativas: {book.ActivePolicies}")
            .AppendLine($"Prêmio mensal: {Money(book.MonthlyPremium)}")
            .AppendLine($"Sua comissão mensal: {Money(book.MonthlyCommission)}{commissionNote}").AppendLine();

        // Follow-ups
        html.Append(Section($"Contatos para hoje ({d.FollowUpsDue.Count})"));
        text.AppendLine($"CONTATOS PARA HOJE ({d.FollowUpsDue.Count})");
        if (followUps.Count == 0)
        {
            html.Append(Muted("Nenhum contato pendente. Tudo em dia!"));
            text.AppendLine("Nenhum contato pendente.");
        }
        else
        {
            html.Append("<ul style=\"padding-left:18px;margin:0\">");
            foreach (var p in followUps)
            {
                var late = p.NextFollowUpOn < today ? $" — atrasado desde {p.NextFollowUpOn!.Value.ToString("dd/MM", PtBr)}" : string.Empty;
                var contact = p.Phone ?? p.Email ?? "sem contato";
                html.Append($"<li style=\"margin:0 0 6px\"><strong>{E(p.Name)}</strong> · {E(contact)} · {E(StageLabel(p.Stage))}<span style=\"color:#c2410c\">{E(late)}</span></li>");
                text.AppendLine($"- {p.Name} · {contact} · {StageLabel(p.Stage)}{late}");
            }

            html.Append("</ul>");
            if (d.FollowUpsDue.Count > followUps.Count)
            {
                html.Append(Muted($"…e mais {d.FollowUpsDue.Count - followUps.Count} na Prospecção."));
            }
        }

        text.AppendLine();

        // Saidas
        html.Append(Section("Saídas recentes da carteira"));
        text.AppendLine("SAÍDAS RECENTES DA CARTEIRA");
        if (exits.Count == 0)
        {
            html.Append(Muted("Nenhuma saída registrada nas importações."));
            text.AppendLine("Nenhuma saída registrada.");
        }
        else
        {
            html.Append("<ul style=\"padding-left:18px;margin:0\">");
            foreach (var x in exits)
            {
                var line = $"{x.HolderName} · apólice {x.PolicyNumber} · {x.InsurerName} · {x.ReferenceMonth.ToString("MM/yyyy", PtBr)}";
                html.Append($"<li style=\"margin:0 0 6px\">{E(line)}</li>");
                text.AppendLine($"- {line}");
            }

            html.Append("</ul>");
            html.Append(Muted("Vale entrar em contato: pode ser cancelamento, falta de pagamento ou troca de corretor."));
        }

        if (d.LastImportAt is { } last)
        {
            html.Append(Muted($"Última importação de carteira: {last.ToString("dd/MM/yyyy", PtBr)}."));
        }

        if (url is not null)
        {
            html.Append($"""<p style="margin:24px 0"><a href="{E(url)}/painel" style="background:#0f6e5c;color:#fff;padding:10px 18px;border-radius:8px;text-decoration:none">Abrir a Vitta</a></p>""");
            text.AppendLine().AppendLine($"Abrir a Vitta: {url}/painel");
        }

        html.Append("""<p style="color:#9ca3af;font-size:12px;margin-top:24px">Você recebe este e-mail porque está na lista do resumo diário (Configurações da Vitta).</p></div>""");
        return new EmailMessage(to, subject, html.ToString(), text.ToString());
    }

    private static string Section(string title) => $"""<h3 style="margin:22px 0 8px;font-size:15px;border-bottom:1px solid #e5e7eb;padding-bottom:4px">{E(title)}</h3>""";
    private static string Row(string label, string valueHtml) =>
        $"""<tr><td style="padding:4px 0;color:#6b7280">{E(label)}</td><td style="padding:4px 0;text-align:right">{valueHtml}</td></tr>""";
    private static string Muted(string value) => $"""<p style="color:#6b7280;margin:6px 0">{E(value)}</p>""";
    private static string Money(decimal value) => value.ToString("C", PtBr);
    private static string E(string value) => Html.Encode(value);
    private static string Plural(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";

    private static string StageLabel(ProspectStage stage) => stage switch
    {
        ProspectStage.New => "Novo",
        ProspectStage.Contacted => "Contatado",
        ProspectStage.MeetingScheduled => "Reunião marcada",
        ProspectStage.ProposalSent => "Proposta enviada",
        ProspectStage.Won => "Ganho",
        ProspectStage.Lost => "Perdido",
        _ => stage.ToString(),
    };
}
