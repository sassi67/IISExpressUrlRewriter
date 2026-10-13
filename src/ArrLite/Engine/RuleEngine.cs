using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using ArrLite.Configuration;

namespace ArrLite.Engine
{
    /// <summary>
    /// Evaluates rules against a request using URL Rewrite semantics (spec §7). Pure: it only computes a
    /// <see cref="RuleResult"/>; <see cref="ArrLiteModule"/> applies it to the real request.
    /// </summary>
    public static class RuleEngine
    {
        private static readonly Regex ServerVariable = new Regex(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled);
        private static readonly Regex BackReference = new Regex(@"\{([RC]):(\d+)\}", RegexOptions.Compiled);

        public static RuleResult Evaluate(IRequestContext request, IReadOnlyList<Rule> rules)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (rules == null || rules.Count == 0) return RuleResult.None;

            // URL Rewrite matches the URL path WITHOUT the leading slash and without the query string.
            // A Rewrite without stopProcessing feeds the rewritten URL to the following rules.
            string path = (request.Path ?? string.Empty).TrimStart('/');
            string query = request.QueryString ?? string.Empty;
            string lastRewriteRule = null;

            foreach (Rule rule in rules)
            {
                Match ruleMatch = rule.Match.Match(path);
                if (ruleMatch.Success == rule.Negate) continue;

                // A negated match has no groups to back-reference.
                if (rule.Negate) ruleMatch = null;

                if (!EvaluateConditions(request, rule, ruleMatch, out Match lastCondition)) continue;

                string target = Expand(request, rule.ActionUrl ?? string.Empty, ruleMatch, lastCondition);

                switch (rule.ActionType)
                {
                    case ActionType.Rewrite:
                        SplitQuery(target, out string targetPath, out string targetQuery);
                        if (rule.AppendQueryString) targetQuery = CombineQuery(targetQuery, query);

                        if (IsAbsolute(targetPath))
                        {
                            // TODO (ARR): gate by an arrLite/proxy "enabled" flag, like ARR does.
                            return RuleResult.Proxy(rule.Name, WithQuery(targetPath, targetQuery));
                        }

                        path = targetPath.TrimStart('/');
                        query = targetQuery;
                        lastRewriteRule = rule.Name;
                        break;

                    case ActionType.Redirect:
                        string location = rule.AppendQueryString ? AppendQuery(target, query) : target;
                        return RuleResult.Redirect(rule.Name, location, rule.RedirectStatus);

                    case ActionType.CustomResponse:
                        return RuleResult.CustomResponse(rule.Name);

                    case ActionType.AbortRequest:
                        return RuleResult.AbortRequest(rule.Name);
                }

                if (rule.StopProcessing) break;
            }

            return lastRewriteRule == null
                ? RuleResult.None
                : RuleResult.Rewrite(lastRewriteRule, WithQuery("/" + path, query));
        }

        private static bool EvaluateConditions(IRequestContext request, Rule rule, Match ruleMatch, out Match lastMatched)
        {
            lastMatched = null;
            if (rule.Conditions.Count == 0) return true;

            foreach (Condition condition in rule.Conditions)
            {
                string input = Expand(request, condition.Input, ruleMatch, lastMatched, unknownVariable: string.Empty);
                Match conditionMatch = condition.Regex.Match(input);
                bool ok = conditionMatch.Success != condition.Negate;
                if (ok && !condition.Negate) lastMatched = conditionMatch;

                if (rule.MatchAnyCondition && ok) return true;
                if (!rule.MatchAnyCondition && !ok) return false;
            }

            return !rule.MatchAnyCondition;
        }

        /// <summary>
        /// Replaces {R:n} / {C:n} back-references and {SERVER_VARIABLE} references.
        /// Undefined server variables become <paramref name="unknownVariable"/>, or stay verbatim when it is null.
        /// </summary>
        private static string Expand(IRequestContext request, string template, Match ruleMatch, Match conditionMatch, string unknownVariable = null)
        {
            string s = BackReference.Replace(template, b =>
            {
                Match source = b.Groups[1].Value == "R" ? ruleMatch : conditionMatch;
                int n = int.Parse(b.Groups[2].Value, CultureInfo.InvariantCulture);
                return source != null && n < source.Groups.Count ? source.Groups[n].Value : string.Empty;
            });

            return ServerVariable.Replace(s, v => request.GetServerVariable(v.Groups[1].Value) ?? unknownVariable ?? v.Value);
        }

        private static void SplitQuery(string url, out string path, out string query)
        {
            int q = url.IndexOf('?');
            path = q < 0 ? url : url.Substring(0, q);
            query = q < 0 ? string.Empty : url.Substring(q + 1);
        }

        private static string CombineQuery(string first, string second)
        {
            if (string.IsNullOrEmpty(first)) return second ?? string.Empty;
            if (string.IsNullOrEmpty(second)) return first;
            return first + "&" + second;
        }

        private static string AppendQuery(string url, string query) =>
            string.IsNullOrEmpty(query) ? url : url + (url.IndexOf('?') >= 0 ? "&" : "?") + query;

        private static string WithQuery(string path, string query) =>
            string.IsNullOrEmpty(query) ? path : path + "?" + query;

        private static bool IsAbsolute(string url) =>
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
