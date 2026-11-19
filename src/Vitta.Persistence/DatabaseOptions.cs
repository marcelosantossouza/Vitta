namespace Vitta.Persistence;

public enum DatabaseProvider
{
    Sqlite,
    Postgres,
}

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Sqlite (dev, padrao) ou Postgres (producao).</summary>
    public DatabaseProvider Provider { get; init; } = DatabaseProvider.Sqlite;

    /// <summary>Aplica migrations pendentes ao subir a API (bom para dev; em prod prefira um passo de deploy).</summary>
    public bool MigrateOnStartup { get; init; }
}
