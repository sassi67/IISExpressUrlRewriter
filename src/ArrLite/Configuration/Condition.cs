using System.Text.RegularExpressions;

namespace ArrLite.Configuration
{
    /// <summary>A <c>conditions/add</c> entry, with its pattern compiled once.</summary>
    public sealed class Condition
    {
        public Condition(string input, string pattern, bool negate = false, bool ignoreCase = true)
        {
            Input = input;
            Negate = negate;
            Regex = RegexFactory.Create(pattern, ignoreCase);
        }

        /// <summary>Input template, e.g. <c>{HTTP_HOST}</c>.</summary>
        public string Input { get; }

        public Regex Regex { get; }

        public bool Negate { get; }
    }
}
