using Vitta.Domain.Common;

namespace Vitta.Domain.UnitTests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("João  Péreira", "joao pereira")]
    [InlineData("  CONCEIÇÃO ", "conceicao")]
    [InlineData(null, "")]
    public void ForSearch_RemovesAccentsCaseAndExtraSpaces(string? input, string expected) =>
        Assert.Equal(expected, TextNormalizer.ForSearch(input));
}
