using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ArrLite.Configuration
{
    /// <summary>
    /// Immutable, pre-compiled form of a <c>rule</c> element. Built once from configuration and
    /// cached, so Microsoft.Web.Administration's ConfigurationElement is never touched per request.
    /// </summary>
    public sealed class Rule
    {
        public Rule(
            string name,
            string matchUrl,
            ActionType actionType,
            string actionUrl = null,
            bool ignoreCase = true,
            bool negate = false,
            bool stopProcessing = false,
            bool matchAnyCondition = false,
            IEnumerable<Condition> conditions = null,
            bool appendQueryString = true,
            int redirectStatus = 301)
        {
            Name = name;
            Match = RegexFactory.Create(matchUrl, ignoreCase);
            Negate = negate;
            StopProcessing = stopProcessing;
            MatchAnyCondition = matchAnyCondition;
            Conditions = (conditions ?? Enumerable.Empty<Condition>()).ToList().AsReadOnly();
            ActionType = actionType;
            ActionUrl = actionUrl;
            AppendQueryString = appendQueryString;
            RedirectStatus = redirectStatus;
        }

        public string Name { get; }

        public Regex Match { get; }

        public bool Negate { get; }

        public bool StopProcessing { get; }

        /// <summary><c>true</c> for <c>logicalGrouping="MatchAny"</c>, <c>false</c> for <c>MatchAll</c>.</summary>
        public bool MatchAnyCondition { get; }

        public IReadOnlyList<Condition> Conditions { get; }

        public ActionType ActionType { get; }

        public string ActionUrl { get; }

        public bool AppendQueryString { get; }

        /// <summary>301/302/303/307 (the schema enum values are the status codes).</summary>
        public int RedirectStatus { get; }
    }
}
