using Vitta.Domain.Common;

namespace Vitta.Domain.UnitTests;

public class CpfTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    public void Create_ValidCpf_Succeeds(string input)
    {
        var result = Cpf.Create(input);

        Assert.True(result.IsSuccess);
        Assert.Equal("52998224725", result.Value.Value);
        Assert.Equal("529.982.247-25", result.Value.Formatted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("111.111.111-11")]
    [InlineData("529.982.247-26")]
    [InlineData("123")]
    public void Create_InvalidCpf_Fails(string input) => Assert.True(Cpf.Create(input).IsFailure);
}
