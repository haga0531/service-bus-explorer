#nullable disable
using FluentAssertions;
using ServiceBusExplorer.Infrastructure.Utils;

namespace ServiceBusExplorer.Tests.Infrastructure.Utils;

[TestFixture]
public class StringUtilsTests
{
    [Test]
    public void DecodeEscapeSequences_ShouldDecodeUnicodeEscapeSequences()
    {
        // Arrange
        var input = "Hello \\u4e16\\u754c"; // "Hello 世界" in Unicode escape sequences

        // Act
        var result = StringUtils.DecodeEscapeSequences(input);

        // Assert
        result.Should().Be("Hello 世界");
    }

    [Test]
    public void DecodeEscapeSequences_ShouldDecodeCommonEscapeSequences()
    {
        // Arrange
        var input = "Line 1\\nLine 2\\tTabbed\\r\\nWindows line ending\\\"Quoted\\\"\\\\Backslash";

        // Act
        var result = StringUtils.DecodeEscapeSequences(input);

        // Assert
        result.Should().Be("Line 1\nLine 2\tTabbed\r\nWindows line ending\"Quoted\"\\Backslash");
    }

    [Test]
    public void DecodeEscapeSequences_ShouldHandleJsonWithUnicodeEscapes()
    {
        // Arrange - Use actual escape sequences that would come from a message body
        var input = "{\"message\": \"Hello \\u4e16\\u754c\", \"emoji\": \"\\ud83d\\ude00\"}";
        var expected = "{\"message\": \"Hello 世界\", \"emoji\": \"😀\"}";

        // Act
        var result = StringUtils.DecodeEscapeSequences(input);

        // Assert
        result.Should().Be(expected);
    }

    [Test]
    public void DecodeEscapeSequences_ShouldHandleSurrogatePair()
    {
        // Arrange - Emoji using surrogate pair (actual escape sequences, not C# interpreted)
        var input = "\\ud83d\\ude00"; // 😀 emoji

        // Act
        var result = StringUtils.DecodeEscapeSequences(input);

        // Assert
        result.Should().Be("😀");
    }

    [Test]
    public void DecodeEscapeSequences_ShouldReturnSameStringWhenNoEscapeSequences()
    {
        // Arrange
        var input = "This is a normal string with no escape sequences";

        // Act
        var result = StringUtils.DecodeEscapeSequences(input);

        // Assert
        result.Should().Be(input);
    }

    [Test]
    public void DecodeEscapeSequences_ShouldHandleNullInput()
    {
        // Arrange
        string input = null;

        // Act
        var result = StringUtils.DecodeEscapeSequences(input);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void DecodeEscapeSequences_ShouldHandleEmptyString()
    {
        // Arrange
        var input = "";

        // Act
        var result = StringUtils.DecodeEscapeSequences(input);

        // Assert
        result.Should().Be("");
    }

    [Test]
    public void DecodeEscapeSequences_ShouldHandleInvalidUnicodeEscapes()
    {
        // Arrange - Invalid hex characters
        var input = "\\uGGGG invalid \\u1234 valid";

        // Act
        var result = StringUtils.DecodeEscapeSequences(input);

        // Assert
        result.Should().Be("\\uGGGG invalid ሴ valid");
    }

    [Test]
    public void DecodeEscapeSequences_ShouldHandleMixedEscapeSequences()
    {
        // Arrange
        var input = "Mixed: \\u4e16\\u754c\\n\\t\\\"Hello\\\"\\\\Path";

        // Act
        var result = StringUtils.DecodeEscapeSequences(input);

        // Assert
        result.Should().Be("Mixed: 世界\n\t\"Hello\"\\Path");
    }
}