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

    public static Error NotFound(Guid id) => Error.NotFound("Insurer.NotFound", "Seguradora não encontrada.");
    public static readonly Error CodeAlreadyExists = Error.Conflict("Insurer.CodeAlreadyExists", "Já existe uma seguradora com este código.");

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }
}
