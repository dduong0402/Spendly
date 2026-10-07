using FluentAssertions;
using Spendly.Web.Models.Categories;
using Xunit;

namespace Spendly.Tests;

public class CategoryIconsTests
{
    [Theory]
    [InlineData("☕")]
    [InlineData("🍽️")]
    [InlineData("📌")]
    [InlineData("🐶")]
    [InlineData("👍🏽")] // emoji có tông màu da vẫn là 1 ký tự
    [InlineData("❤️")]
    public void IsValid_SingleEmoji_ReturnsTrue(string value)
    {
        CategoryIcons.IsValid(value).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    [InlineData("abc")]
    [InlineData("Ă")]
    [InlineData("1")]
    [InlineData("utensils")] // khóa icon cũ không được lưu mới
    [InlineData("😀😀")]     // 2 emoji
    [InlineData("☕☕")]
    [InlineData("☕a")]
    [InlineData("a☕")]
    [InlineData(" ☕")]      // khoảng trắng thừa
    [InlineData("☕ ")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\uD83D")]   // surrogate lẻ (chuỗi hỏng)
    public void IsValid_NotExactlyOneEmoji_ReturnsFalse(string? value)
    {
        CategoryIcons.IsValid(value).Should().BeFalse();
    }

    [Fact]
    public void IsValid_TooLong_ReturnsFalse()
    {
        CategoryIcons.IsValid(new string('☕', CategoryIcons.MaxLength + 1)).Should().BeFalse();
    }

    [Theory]
    [InlineData("utensils", "🍽️")]
    [InlineData("coffee", "☕")]
    [InlineData("ellipsis", "📌")]
    public void Resolve_LegacyKey_ReturnsEmoji(string key, string expected)
    {
        CategoryIcons.Resolve(key).Should().Be(expected);
    }

    [Fact]
    public void Resolve_ValidEmoji_ReturnsItUnchanged()
    {
        CategoryIcons.Resolve("🐶").Should().Be("🐶");
        CategoryIcons.Emoji("🐶").Should().Be("🐶");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("khong-co-icon")]
    [InlineData("😀😀")]
    public void Resolve_MissingOrInvalid_ReturnsDefault(string? value)
    {
        CategoryIcons.Resolve(value).Should().Be(CategoryIcons.DefaultEmoji);
    }

    [Fact]
    public void LegacyKeys_AllResolveToValidEmoji()
    {
        CategoryIcons.LegacyKeys.Should().NotBeEmpty();
        foreach (var key in CategoryIcons.LegacyKeys)
        {
            CategoryIcons.IsValid(CategoryIcons.Resolve(key)).Should().BeTrue($"khóa cũ '{key}' phải đổi được sang emoji hợp lệ");
        }
    }
}
