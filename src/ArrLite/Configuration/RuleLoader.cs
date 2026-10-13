using System;
using System.Collections.Generic;
using System.Web;
using Mwa = Microsoft.Web.Administration;

namespace ArrLite.Configuration
{
    /// <summary>
    /// Reads the effective <c>system.webServer/arrLite/rules</c> section for a request
    /// (applicationHost.config, &lt;location&gt; blocks and web.config merged) and converts it to POCOs.
    /// </summary>
    internal static class RuleLoader
    {
        public const string RulesSectionPath = "system.webServer/arrLite/rules";

        public static IReadOnlyList<Rule> Load(HttpContext context)
        {
            // ⚠ VERIFY (spec §9.4): this overload resolves inside iisexpress.exe with the MWA version it ships.
            Mwa.ConfigurationSection section = Mwa.WebConfigurationManager.GetSection(context, RulesSectionPath);

            var result = new List<Rule>();
            foreach (Mwa.ConfigurationElement r in section.GetCollection())
            {
                if (!(bool)r["enabled"]) continue;

                Mwa.ConfigurationElement match = r.GetChildElement("match");
                Mwa.ConfigurationElement conditions = r.GetChildElement("conditions");
                Mwa.ConfigurationElement action = r.GetChildElement("action");

                var parsedConditions = new List<Condition>();
                foreach (Mwa.ConfigurationElement c in conditions.GetCollection())
                {
                    parsedConditions.Add(new Condition(
                        (string)c["input"], (string)c["pattern"], (bool)c["negate"], (bool)c["ignoreCase"]));
                }

                result.Add(new Rule(
                    name: (string)r["name"],
                    matchUrl: (string)match["url"],
                    // ⚠ VERIFY (spec §9.5): enum attributes come back as Int32; Convert keeps this tolerant.
                    actionType: (ActionType)Convert.ToInt32(action["type"]),
                    actionUrl: (string)action["url"],
                    ignoreCase: (bool)match["ignoreCase"],
                    negate: (bool)match["negate"],
                    stopProcessing: (bool)r["stopProcessing"],
                    matchAnyCondition: Convert.ToInt32(conditions["logicalGrouping"]) == 1,
                    conditions: parsedConditions,
                    appendQueryString: (bool)action["appendQueryString"],
                    redirectStatus: Convert.ToInt32(action["redirectType"])));
            }

            return result.AsReadOnly();
        }
    }
}
