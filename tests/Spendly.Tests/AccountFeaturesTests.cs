using System.Net;
using System.Text;
using FluentAssertions;
using Spendly.Web.Common;
using Spendly.Web.Services.Email;
using Xunit;

namespace Spendly.Tests;

public class CsvWriterTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("Cà phê", "Cà phê")]
    [InlineData("Phở, bò", "\"Phở, bò\"")]
    [InlineData("Nói \"có\"", "\"Nói \"\"có\"\"\"")]
    [InlineData("dòng 1\ndòng 2", "\"dòng 1\ndòng 2\"")]
    public void Escape_QuotesOnlyWhenNeeded(string? input, string expected)
    {
        CsvWriter.Escape(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("=SUM(A1:A9)", "'=SUM(A1:A9)")]
    [InlineData("+84 90", "'+84 90")]
    [InlineData("-50k", "'-50k")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("\tabc", "'\tabc")]
    public void Escape_NeutralizesFormulaInjection(string input, string expected)
    {
        CsvWriter.Escape(input).Should().Be(expected);
    }

    [Fact]
    public void Escape_InjectionGuardAndQuotingCombine()
    {
        CsvWriter.Escape("=1,2").Should().Be("\"'=1,2\"");
    }

    [Fact]
    public void Build_StartsWithBomAndSepLine_ThenHeaderAndCrlfRows()
    {
        var bytes = CsvWriter.Build(new[] { "A", "B" }, new[] { new[] { "1", "x,y" }, new[] { "2", null } });

        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        text.Should().Be("sep=,\r\nA,B\r\n1,\"x,y\"\r\n2,\r\n");
    }
}

public class PasswordResetEmailTests
{
    [Fact]
    public void Build_ContainsLinkInBothVersions_AndHtmlEncodesUserInput()
    {
        var link = "https://spendly.example.com/Account/ResetPassword?email=a%40b.com&token=ab%2Bc";

        var message = PasswordResetEmail.Build("a@b.com", "<script>x</script> Dương", link, 60);

        message.To.Should().Be("a@b.com");
        message.Subject.Should().Be(PasswordResetEmail.Subject);
        message.TextBody.Should().Contain(link).And.Contain("60 phút");
        message.HtmlBody.Should().Contain(WebUtility.HtmlEncode(link));
        message.HtmlBody.Should().NotContain("<script>");
        message.HtmlBody.Should().Contain("&amp;token=");
    }

    [Fact]
    public void Build_BlankName_FallsBackToGenericGreeting()
    {
        PasswordResetEmail.Build("a@b.com", "  ", "https://x/y", 60).TextBody.Should().StartWith("Xin chào bạn");
    }
}

public class EmailOptionsTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("smtp.gmail.com", true)]
    public void IsConfigured_DependsOnHost(string? host, bool expected)
    {
        new EmailOptions { Host = host }.IsConfigured.Should().Be(expected);
    }

    [Fact]
    public void EffectiveFromAddress_FallsBackToUserName()
    {
        new EmailOptions { UserName = "me@gmail.com" }.EffectiveFromAddress.Should().Be("me@gmail.com");
        new EmailOptions { UserName = "me@gmail.com", FromAddress = "no-reply@spendly.vn" }.EffectiveFromAddress.Should().Be("no-reply@spendly.vn");
    }
}
