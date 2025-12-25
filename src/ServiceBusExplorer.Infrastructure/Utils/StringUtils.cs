using System.Text.RegularExpressions;

namespace ServiceBusExplorer.Infrastructure.Utils;

/// <summary>
/// Utility methods for string processing and Unicode handling.
/// </summary>
public static class StringUtils
{
    private static readonly Regex UnicodeEscapeRegex = new(@"\\u([0-9a-fA-F]{4})", RegexOptions.Compiled);
    
    /// <summary>
    /// Decodes Unicode escape sequences and other common escape sequences in a string.
    /// Converts patterns like \uXXXX to their corresponding Unicode characters.
    /// </summary>
    /// <param name="input">The input string that may contain escape sequences</param>
    /// <returns>A string with escape sequences decoded to actual characters</returns>
    public static string DecodeEscapeSequences(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // Simple replacement approach for Unicode escape sequences
        var result = input;
        int searchIndex = 0;
        
        while (searchIndex < result.Length)
        {
            int index = result.IndexOf("\\u", searchIndex);
            if (index == -1) break;
            
            // Check if we have enough characters for a complete escape sequence
            if (index + 6 > result.Length) break;
            
            var hexPart = result.Substring(index + 2, 4);
            if (int.TryParse(hexPart, System.Globalization.NumberStyles.HexNumber, null, out var codePoint))
            {
                string replacement;
                int replaceLength = 6; // Default length of \uXXXX
                
                // Check for surrogate pairs
                if (codePoint >= 0xD800 && codePoint <= 0xDBFF) // High surrogate
                {
                    // Look for the following low surrogate
                    if (index + 11 < result.Length && result.Substring(index + 6, 2) == "\\u")
                    {
                        var lowSurrogateHex = result.Substring(index + 8, 4);
                        if (int.TryParse(lowSurrogateHex, System.Globalization.NumberStyles.HexNumber, null, out var lowSurrogate)
                            && lowSurrogate >= 0xDC00 && lowSurrogate <= 0xDFFF)
                        {
                            // Valid surrogate pair
                            var fullCodePoint = 0x10000 + ((codePoint - 0xD800) << 10) + (lowSurrogate - 0xDC00);
                            replacement = char.ConvertFromUtf32(fullCodePoint);
                            replaceLength = 12; // \uXXXX\uXXXX
                        }
                        else
                        {
                            // Invalid low surrogate, treat high surrogate as individual character
                            replacement = ((char)codePoint).ToString();
                        }
                    }
                    else
                    {
                        // No following escape sequence, treat as individual character
                        replacement = ((char)codePoint).ToString();
                    }
                }
                else if (codePoint >= 0xDC00 && codePoint <= 0xDFFF) // Low surrogate without high surrogate
                {
                    // Isolated low surrogate, treat as individual character
                    replacement = ((char)codePoint).ToString();
                }
                else
                {
                    // Regular Unicode character
                    try
                    {
                        replacement = char.ConvertFromUtf32(codePoint);
                    }
                    catch
                    {
                        // If conversion fails, leave the original escape sequence
                        searchIndex = index + 6;
                        continue;
                    }
                }
                
                result = result.Substring(0, index) + replacement + result.Substring(index + replaceLength);
                searchIndex = index + replacement.Length;
            }
            else
            {
                // Invalid hex, skip this escape sequence
                searchIndex = index + 2;
            }
        }

        // Handle other common escape sequences
        result = result.Replace("\\n", "\n")
                      .Replace("\\r", "\r")
                      .Replace("\\t", "\t")
                      .Replace("\\\"", "\"")
                      .Replace("\\\\", "\\");

        return result;
    }
}