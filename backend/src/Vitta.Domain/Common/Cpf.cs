namespace Vitta.Domain.Common;

/// <summary>CPF brasileiro (apenas digitos, com validacao dos digitos verificadores).</summary>
public sealed record Cpf
{
    private Cpf(string value) => Value = value;

    public string Value { get; }

    public string Formatted => $"{Value[..3]}.{Value[3..6]}.{Value[6..9]}-{Value[9..]}";

    public static Result<Cpf> Create(string? input)
    {
        var digits = new string((input ?? string.Empty).Where(char.IsDigit).ToArray());

        if (!IsValid(digits))
        {
            return Error.Validation("Cpf.Invalid", "CPF invalido.");
        }

        return new Cpf(digits);
    }

    private static bool IsValid(string d)
    {
        if (d.Length != 11 || d.Distinct().Count() == 1)
        {
            return false;
        }

        return CheckDigit(d, 9) == d[9] - '0' && CheckDigit(d, 10) == d[10] - '0';
    }

    private static int CheckDigit(string d, int length)
    {
        var sum = 0;
        for (var i = 0; i < length; i++)
        {
            sum += (d[i] - '0') * (length + 1 - i);
        }

        var rest = sum % 11;
        return rest < 2 ? 0 : 11 - rest;
    }

    public override string ToString() => Formatted;
}
