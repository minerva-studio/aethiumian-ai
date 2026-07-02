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
            bool wasCapitalized = true;

            for (int i = 1; i < text.Length; i++)
            {
                bool isCapitalized = char.IsUpper(text, i);
                if (isCapitalized && !wasCapitalized)
                {
                    builder.Append(' ');
                    builder.Append(char.ToUpper(text[i], CultureInfo.InvariantCulture));
                }
                else
                {
                    builder.Append(text[i]);
                }

                wasCapitalized = isCapitalized;
            }

            return builder.ToString();
        }
    }
}
