using Farmacia.Domain.Common;

namespace Farmacia.Domain.Tests.Common;

public class NitTests
{
    [Theory]
    [InlineData("800197268-4")] // DIAN
    [InlineData("890903938-8")] // Bancolombia
    [InlineData("900123455-0")] // remainder 0 -> digit 0
    [InlineData("900123459-1")] // remainder 1 -> digit 1
    public void Parse_WithValidVerificationDigit_Succeeds(string value)
    {
        var nit = Nit.Parse(value);

        Assert.Equal(value, nit.Value);
    }

    [Theory]
    [InlineData("800.197.268-4")]
    [InlineData(" 800 197 268 - 4 ")]
    public void Parse_NormalizesDotsAndSpaces(string value)
    {
        var nit = Nit.Parse(value);

        Assert.Equal("800197268", nit.Number);
        Assert.Equal(4, nit.VerificationDigit);
        Assert.Equal("800197268-4", nit.ToString());
    }

    [Fact]
    public void Parse_WithWrongVerificationDigit_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => Nit.Parse("800197268-5"));

        Assert.Contains("dígito de verificación", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("8001972684")]          // no dash: ambiguous
    [InlineData("800197268-")]          // missing digit
    [InlineData("-4")]                  // missing number
    [InlineData("800197268-45")]        // digit too long
    [InlineData("80019A268-4")]         // letters
    [InlineData("800-197-268-4")]       // several dashes
    [InlineData("1234567890123456-1")]  // longer than the 15 DIAN weights
    public void Parse_WithInvalidFormat_Throws(string value)
    {
        Assert.Throws<DomainException>(() => Nit.Parse(value));
    }

    [Theory]
    [InlineData("800197268", "800197268-4")] // DIAN
    [InlineData("890.903.938", "890903938-8")] // Bancolombia
    [InlineData("900123455", "900123455-0")]
    public void FromNumber_CalculatesTheVerificationDigit(string number, string expected)
    {
        Assert.Equal(expected, Nit.FromNumber(number).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("80019726A")]
    [InlineData("800197268-4")] // already has a digit: use Parse
    public void FromNumber_WithInvalidInput_Throws(string number)
    {
        Assert.Throws<DomainException>(() => Nit.FromNumber(number));
    }

    [Fact]
    public void Equality_IsByValue()
    {
        Assert.Equal(Nit.Parse("800.197.268-4"), Nit.Parse("800197268-4"));
    }
}
