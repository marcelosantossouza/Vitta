using Vitta.Domain.Common;

namespace Vitta.Domain.Insurers;

/// <summary>Seguradora parceira (ex.: MAG/Mongeral, Prudential, Icatu...).</summary>
public sealed class Insurer : AggregateRoot
{
    private Insurer() { } // EF

    private Insurer(Guid id, string code, string name)
    {
        Id = id;
        Code = code;
        Name = name;
        IsActive = true;
    }

    /// <summary>Codigo curto e estavel usado em integracoes/importacoes (ex.: "MAG").</summary>
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; }

    public static Insurer Create(string code, string name, Guid? id = null) =>
        new(id ?? Guid.CreateVersion7(), code.Trim().ToUpperInvariant(), name.Trim());

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }
}
