using NileChain.Application.Validation.Common;

namespace NileChain.Tests;

public class EgyptianPhoneTests
{
    [Theory]
    [InlineData("01001234567", true)]
    [InlineData("01112345678", true)]
    [InlineData("01234567890", true)]
    [InlineData("01512345678", true)]
    [InlineData("+201001234567", true)]
    [InlineData("00201001234567", true)]
    [InlineData("0100 123 4567", true)]
    [InlineData("010-012-34567", true)]
    public void ValidEgyptianMobiles(string input, bool expected)
    {
        Assert.Equal(expected, EgyptianPhone.IsValid(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("12345")]
    [InlineData("02001234567")] // landline-style, not 01
    [InlineData("0100123456")] // 10 digits
    [InlineData("010012345678")] // 12 digits
    [InlineData("01abcdefghi")] // letters only after 01 → too short
    public void InvalidEgyptianMobiles(string input)
    {
        Assert.False(EgyptianPhone.IsValid(input));
    }

    [Fact]
    public void Normalize_StripsAndConvertsInternational()
    {
        Assert.True(EgyptianPhone.TryNormalizeValid("+20 100-123-4567", out var n));
        Assert.Equal("01001234567", n);
    }
}
