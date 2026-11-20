namespace Vitta.Application.Common;

public static class AppClock
{
    private static readonly TimeZoneInfo Brazil = FindBrazilTimeZone();

    /// <summary>"Hoje" no horario de Brasilia (follow-ups, vencimentos).</summary>
    public static DateOnly Today(this TimeProvider time) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), Brazil).DateTime);

    private static TimeZoneInfo FindBrazilTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");
        }
    }
}
