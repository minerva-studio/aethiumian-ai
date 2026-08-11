using System.Globalization;
using System.Text;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// AI editor local string helpers.
    /// </summary>
    internal static class StringExtensions
    {
        public static string ToTitleCase(this string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text ?? string.Empty;
            }

            if (text.Length < 2)
            {
                return text.ToUpper(CultureInfo.InvariantCulture);
            }

            StringBuilder builder = new();
            builder.Append(char.ToUpper(text[0], CultureInfo.InvariantCulture));
            for (int i = 1; i < text.Length; i++)
            {
                char current = text[i];
                char previous = text[i - 1];
                char next = i + 1 < text.Length ? text[i + 1] : '\0';
                bool startsWord = char.IsUpper(current)
                    && (char.IsLower(previous)
                        || char.IsDigit(previous) && char.IsLower(next)
                        || char.IsUpper(previous) && char.IsLower(next));
                bool startsNumber = char.IsDigit(current) && char.IsLetter(previous);
                if (startsWord || startsNumber)
                {
                    builder.Append(' ');
                }

                builder.Append(current);
            }

            return builder.ToString();
        }
    }
}
