using System.Text.RegularExpressions;

namespace ArrLite.Configuration
{
    internal static class RegexFactory
    {
        public static Regex Create(string pattern, bool ignoreCase) =>
            new Regex(pattern ?? string.Empty,
                RegexOptions.Compiled | RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None));
    }
}
