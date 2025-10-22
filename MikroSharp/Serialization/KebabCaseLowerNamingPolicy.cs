using System.Text;
using System.Text.Json;

namespace MikroSharp.Serialization;

/// <summary>
/// A System.Text.Json naming policy that converts property names to lower kebab-case (dash-case).
/// Example: "SharedUsers" => "shared-users".
/// </summary>
public sealed class KebabCaseLowerNamingPolicy : JsonNamingPolicy
{
    public static readonly JsonNamingPolicy Instance = new KebabCaseLowerNamingPolicy();

    public override string ConvertName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var sb = new StringBuilder(name.Length + 8);
        var prevCategory = CharCategory.Unknown;

        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            var cat = Classify(c);

            if (i > 0 && ShouldInsertDash(prevCategory, cat))
            {
                sb.Append('-');
            }

            sb.Append(char.ToLowerInvariant(c));
            prevCategory = cat;
        }

        return sb.ToString();
    }

    private static CharCategory Classify(char c)
    {
        if (char.IsUpper(c)) return CharCategory.Upper;
        if (char.IsLower(c)) return CharCategory.Lower;
        if (char.IsDigit(c)) return CharCategory.Digit;
        return CharCategory.Other;
    }

    private static bool ShouldInsertDash(CharCategory prev, CharCategory current)
    {
        // Transition rules similar to common kebab-case splitting:
        // - lower/upper/digit to upper/lower/digit boundaries
        // - don't insert between consecutive lowers
        // - insert between: lower->upper, digit->letter, letter->digit
        if (prev == CharCategory.Unknown) return false;
        if (current == CharCategory.Other) return false;
        if (prev == CharCategory.Other) return false;

        if (prev == CharCategory.Lower && current == CharCategory.Upper) return true;
        if (prev == CharCategory.Digit && current != CharCategory.Digit) return true;
        if (prev != CharCategory.Digit && current == CharCategory.Digit) return true;

        // Handle acronym boundaries: "HTTPServer" => "http-server"
        if (prev == CharCategory.Upper && current == CharCategory.Upper) return false;
        if (prev == CharCategory.Upper && current == CharCategory.Lower) return true;

        return false;
    }

    private enum CharCategory { Unknown, Lower, Upper, Digit, Other }
}

