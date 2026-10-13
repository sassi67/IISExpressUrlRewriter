# IIS Express managed rewrite/proxy module — implementation spec

> Hand-off document for implementation in Claude Code.
> Goal: a **managed IIS module (C#, .NET Framework 4.8)** for **IIS Express** that replicates the parts of
> [URL Rewrite](https://www.iis.net/downloads/microsoft/url-rewrite) and
> [Application Request Routing (ARR)](https://www.iis.net/downloads/microsoft/application-request-routing)
> we need, because the official modules can't be used in the target environment.
>
> Companion project: the existing **.NET 10 config tool** (Microsoft.Web.Administration) that prepares
> the IIS Express `applicationHost.config` before startup. This spec extends it.
>
> `arrLite` is a **placeholder** for the module and section name. Rename it consistently everywhere.

---

## 0. Status of the claims in this document

Most of this document is established IIS behavior. Items marked **⚠ VERIFY** come from my own knowledge
and have not been tested on the target machine. Check each one early (see §9). Don't build heavily on any
of them until it has been confirmed.

---

## 1. Background: why a schema is needed

- IIS configuration is **schema-driven**. The config engine (`nativerd.dll`) only accepts sections that are:
  1. described by a schema file (`*.xml` in the `config\schema` folder), **and**
  2. declared under `<configSections>` in `applicationHost.config`.
- `<system.webServer><rewrite>` is valid only because the URL Rewrite installer adds `rewrite_schema.xml`
  and the section declarations. ARR does the same with `arr_schema.xml` (`webFarms`, `system.webServer/proxy`).
- If either piece is missing, IIS returns **HTTP 500.19** (unrecognized or undeclared configuration section),
  or the site fails to start.
- Reference: *Professional Microsoft IIS 8*, Ch. 12 "Extending IIS Configuration" (pp. 377–381: custom schema
  file, `<configSections>` registration, reading it from a managed module) and Ch. 18 "Direct Configuration"
  (pp. 616–617: creating a new section, writing it with `ServerManager`).

### Decision: use our own section names, not `system.webServer/rewrite`

- ⚠ VERIFY: recent IIS Express builds may already ship URL Rewrite (`rewrite.dll` in the install folder,
  `rewrite_schema.xml` in `config\schema`). If so, `system.webServer/rewrite` is **already declared**.
  Adding our own schema under the same name would then cause duplicate-definition errors.
- **Chosen:** `system.webServer/arrLite/*`. No conflicts, and it can run next to the real RewriteModule.
- Rejected alternative: reuse `system.webServer/rewrite/rules` for drop-in compatibility. This needs
  `<remove name="RewriteModule" />` under `<modules>`, otherwise both modules run the rules.
- Our element layout (`rule/match/conditions/action`) mirrors URL Rewrite, so porting existing rules
  only means renaming the parent element.

---

## 2. Where the schema file must go

- The schema folder is **relative to the IIS Express installation that actually runs**:
  `<folder of iisexpress.exe>\config\schema\`. The default MSI install is `%ProgramFiles%\IIS Express`.
  A custom or xcopy install at `D:\Tools\IISExpress` reads from `D:\Tools\IISExpress\config\schema\`.
  - ⚠ VERIFY: as far as I know the engine resolves this from its own install directory (the folder
    `%IIS_BIN%` points to), and there is **no documented override** in `applicationHost.config` or elsewhere.
- **Bitness = two installs.** `Program Files\IIS Express` (x64) and `Program Files (x86)\IIS Express` (x86)
  each have their own `config\schema`. Install into every instance that may be launched.
  Visual Studio may start either one.
- **Permissions.** Under `Program Files` you need admin rights, once per machine. A custom install in a
  user-writable folder needs no elevation.
- **Locating the folder from the tool, most reliable first:**
  1. The tool launches `iisexpress.exe` itself → `Path.Combine(Path.GetDirectoryName(exe), "config", "schema")`.
     This also covers portable installs.
  2. MSI installs: registry `HKLM\SOFTWARE\Microsoft\IISExpress\<version>` value `InstallPath`
     (and `HKLM\SOFTWARE\WOW6432Node\...` for x86). ⚠ VERIFY the key and value names.
- **Ground truth:** Process Monitor, filter `Process Name is iisexpress.exe` + `Path contains schema`,
  then start a site. It shows exactly which folder and which `*.xml` files get read.

---

## 3. Schema file — `arrLite_schema.xml`

Copy to `<IIS Express install>\config\schema\arrLite_schema.xml`.

IIS schema files can't share type definitions, so `globalRules` repeats the `rules` layout.

```xml
<configSchema>

  <!-- Per-site / per-directory rules (allowed in web.config) -->
  <sectionSchema name="system.webServer/arrLite/rules">
    <collection addElement="rule" clearElement="clear" removeElement="remove">
      <attribute name="name" type="string" required="true" isUniqueKey="true" validationType="nonEmptyString" />
      <attribute name="enabled" type="bool" defaultValue="true" />
      <attribute name="stopProcessing" type="bool" defaultValue="false" />
      <element name="match">
        <attribute name="url" type="string" required="true" />
        <attribute name="ignoreCase" type="bool" defaultValue="true" />
        <attribute name="negate" type="bool" defaultValue="false" />
      </element>
      <element name="conditions">
        <attribute name="logicalGrouping" type="enum" defaultValue="MatchAll">
          <enum name="MatchAll" value="0" />
          <enum name="MatchAny" value="1" />
        </attribute>
        <collection addElement="add" clearElement="clear">
          <attribute name="input" type="string" required="true" isCombinedKey="true" />
          <attribute name="pattern" type="string" isCombinedKey="true" />
          <attribute name="negate" type="bool" defaultValue="false" />
          <attribute name="ignoreCase" type="bool" defaultValue="true" />
        </collection>
      </element>
      <element name="action">
        <attribute name="type" type="enum" defaultValue="None">
          <enum name="None" value="0" />
          <enum name="Rewrite" value="1" />
          <enum name="Redirect" value="2" />
          <enum name="CustomResponse" value="3" />
          <enum name="AbortRequest" value="4" />
        </attribute>
        <attribute name="url" type="string" />
        <attribute name="appendQueryString" type="bool" defaultValue="true" />
        <attribute name="redirectType" type="enum" defaultValue="Permanent">
          <enum name="Permanent" value="301" />
          <enum name="Found" value="302" />
          <enum name="SeeOther" value="303" />
          <enum name="Temporary" value="307" />
        </attribute>
      </element>
    </collection>
  </sectionSchema>

  <!-- Server-wide rules (applicationHost.config only) -->
  <sectionSchema name="system.webServer/arrLite/globalRules">
    <collection addElement="rule" clearElement="clear" removeElement="remove">
      <attribute name="name" type="string" required="true" isUniqueKey="true" validationType="nonEmptyString" />
      <attribute name="enabled" type="bool" defaultValue="true" />
      <attribute name="stopProcessing" type="bool" defaultValue="false" />
      <element name="match">
        <attribute name="url" type="string" required="true" />
        <attribute name="ignoreCase" type="bool" defaultValue="true" />
        <attribute name="negate" type="bool" defaultValue="false" />
      </element>
      <element name="conditions">
        <attribute name="logicalGrouping" type="enum" defaultValue="MatchAll">
          <enum name="MatchAll" value="0" />
          <enum name="MatchAny" value="1" />
        </attribute>
        <collection addElement="add" clearElement="clear">
          <attribute name="input" type="string" required="true" isCombinedKey="true" />
          <attribute name="pattern" type="string" isCombinedKey="true" />
          <attribute name="negate" type="bool" defaultValue="false" />
          <attribute name="ignoreCase" type="bool" defaultValue="true" />
        </collection>
      </element>
      <element name="action">
        <attribute name="type" type="enum" defaultValue="None">
          <enum name="None" value="0" />
          <enum name="Rewrite" value="1" />
          <enum name="Redirect" value="2" />
          <enum name="CustomResponse" value="3" />
          <enum name="AbortRequest" value="4" />
        </attribute>
        <attribute name="url" type="string" />
        <attribute name="appendQueryString" type="bool" defaultValue="true" />
        <attribute name="redirectType" type="enum" defaultValue="Permanent">
          <enum name="Permanent" value="301" />
          <enum name="Found" value="302" />
          <enum name="SeeOther" value="303" />
          <enum name="Temporary" value="307" />
        </attribute>
      </element>
    </collection>
  </sectionSchema>

  <!-- TODO (not designed yet): rewriteMaps, outboundRules, and ARR-style
       system.webServer/arrLite/proxy + a webFarms equivalent for routing/load balancing. -->

</configSchema>
```

---

## 4. Section declaration in `applicationHost.config`

Goes **inside the existing** `system.webServer` section group of the `applicationHost.config` that
IIS Express will load:

```xml
<configSections>
  <sectionGroup name="system.webServer">
    <!-- existing sections... -->
    <sectionGroup name="arrLite">
      <section name="rules" overrideModeDefault="Allow" />
      <section name="globalRules" overrideModeDefault="Deny" allowDefinition="AppHostOnly" />
    </sectionGroup>
  </sectionGroup>
</configSections>
```

Example of the resulting rule configuration (written by the tool, §5):

```xml
<location path="MySite">
  <system.webServer>
    <arrLite>
      <rules>
        <rule name="api-to-backend" stopProcessing="true">
          <match url="^api/(.*)" />
          <action type="Rewrite" url="http://localhost:5001/{R:1}" />
        </rule>
      </rules>
    </arrLite>
  </system.webServer>
</location>
```

---

## 5. Config tool (.NET 10) — writing everything before IIS Express starts

### 5.1 Install the schema file

```csharp
using System;
using System.IO;

static class SchemaInstaller
{
    public const string SchemaFileName = "arrLite_schema.xml";

    /// <summary>Schema folder of the IIS Express instance that will actually run.</summary>
    public static string GetSchemaFolder(string iisExpressExePath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(iisExpressExePath))!, "config", "schema");

    /// <summary>Copies the schema file; overwrites if the content differs. Requires write access (admin under Program Files).</summary>
    public static void Install(string iisExpressExePath, string sourceSchemaPath)
    {
        string folder = GetSchemaFolder(iisExpressExePath);
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"IIS Express schema folder not found: {folder}");

        string target = Path.Combine(folder, SchemaFileName);
        if (File.Exists(target) &&
            File.ReadAllBytes(target).AsSpan().SequenceEqual(File.ReadAllBytes(sourceSchemaPath)))
            return;

        File.Copy(sourceSchemaPath, target, overwrite: true);
    }
}
```

### 5.2 Declare the section and write rules — option A: XDocument (recommended)

This needs **no schema at write time**. The schema only matters when `iisexpress.exe` loads the config.

```csharp
using System.Linq;
using System.Xml.Linq;

static class ArrLiteConfigWriter
{
    public static void EnsureSectionsDeclared(XDocument doc)
    {
        XElement configSections = doc.Root!.Element("configSections")
            ?? throw new InvalidOperationException("<configSections> not found");

        XElement webServer = configSections.Elements("sectionGroup")
            .FirstOrDefault(e => (string?)e.Attribute("name") == "system.webServer")
            ?? throw new InvalidOperationException("system.webServer sectionGroup not found");

        XElement group = GetOrAdd(webServer, "sectionGroup", "arrLite");
        EnsureSection(group, "rules", overrideModeDefault: "Allow", allowDefinition: null);
        EnsureSection(group, "globalRules", overrideModeDefault: "Deny", allowDefinition: "AppHostOnly");
    }

    /// <summary>Adds or replaces a rule (by name) in &lt;location path="{locationPath}"&gt;.</summary>
    public static void UpsertRule(XDocument doc, string locationPath, XElement rule)
    {
        XElement root = doc.Root!;
        XElement location = root.Elements("location")
            .FirstOrDefault(l => (string?)l.Attribute("path") == locationPath)
            ?? AddAndReturn(root, new XElement("location", new XAttribute("path", locationPath)));

        XElement rules = GetOrAddChild(GetOrAddChild(GetOrAddChild(location, "system.webServer"), "arrLite"), "rules");

        string name = (string)rule.Attribute("name")!;
        rules.Elements("rule").Where(r => (string?)r.Attribute("name") == name).Remove();
        rules.Add(rule);
    }

    public static XElement Rule(string name, string matchUrl, string actionType, string actionUrl, bool stopProcessing = true) =>
        new XElement("rule",
            new XAttribute("name", name),
            new XAttribute("stopProcessing", stopProcessing ? "true" : "false"),
            new XElement("match", new XAttribute("url", matchUrl)),
            new XElement("action", new XAttribute("type", actionType), new XAttribute("url", actionUrl)));

    static void EnsureSection(XElement group, string name, string overrideModeDefault, string? allowDefinition)
    {
        XElement section = GetOrAdd(group, "section", name);
        section.SetAttributeValue("overrideModeDefault", overrideModeDefault);
        if (allowDefinition != null) section.SetAttributeValue("allowDefinition", allowDefinition);
    }

    static XElement GetOrAdd(XElement parent, string elementName, string nameAttr) =>
        parent.Elements(elementName).FirstOrDefault(e => (string?)e.Attribute("name") == nameAttr)
        ?? AddAndReturn(parent, new XElement(elementName, new XAttribute("name", nameAttr)));

    static XElement GetOrAddChild(XElement parent, string name) =>
        parent.Element(name) ?? AddAndReturn(parent, new XElement(name));

    static XElement AddAndReturn(XElement parent, XElement child) { parent.Add(child); return child; }
}

// Usage
// var doc = XDocument.Load(configPath, LoadOptions.PreserveWhitespace);
// ArrLiteConfigWriter.EnsureSectionsDeclared(doc);
// ArrLiteConfigWriter.UpsertRule(doc, "MySite",
//     ArrLiteConfigWriter.Rule("api-to-backend", "^api/(.*)", "Rewrite", "http://localhost:5001/{R:1}"));
// doc.Save(configPath);
```

### 5.3 Write rules — option B: Microsoft.Web.Administration

```csharp
using Microsoft.Web.Administration;

using var sm = new ServerManager(@"C:\work\applicationhost.config");
var config = sm.GetApplicationHostConfiguration();
var rules = config.GetSection("system.webServer/arrLite/rules", "MySite").GetCollection();

var rule = rules.CreateElement("rule");
rule["name"] = "api-to-backend";
rule["stopProcessing"] = true;
rule.GetChildElement("match")["url"] = "^api/(.*)";
var action = rule.GetChildElement("action");
action["type"] = "Rewrite";                       // enum values can be set by name
action["url"]  = "http://localhost:5001/{R:1}";
rules.Add(rule);

sm.CommitChanges();   // writes <location path="MySite"><system.webServer><arrLite><rules>…
```

**Caveat ⚠ VERIFY:** Microsoft.Web.Administration validates against the schema of the config engine
registered through COM. That is usually full IIS (`%windir%\system32\inetsrv\config\schema`) or the
MSI-installed IIS Express, **not** a custom/portable copy, even when you pass the IIS Express config path.
If `GetSection` reports the section as unknown, that's why. Fix it by installing the schema in that folder
too, or use option A. Note that the section declaration (§4) must exist before option B can work at all.

### 5.4 Register the managed module

Under `<system.webServer><modules>` (no `<globalModules>` entry; that is only for native modules):

```xml
<modules>
  <add name="ArrLite" type="ArrLite.ArrLiteModule, ArrLite, Version=1.0.0.0, Culture=neutral, PublicKeyToken=…" />
</modules>
```

- **Omit** `preCondition="managedHandler"` so the module also sees static and extensionless requests.
- The app pool must use the **Integrated** pipeline with **CLR v4.0**.
- Registered server-wide in `applicationHost.config` → the assembly must be **strong-named and in the GAC**.
  Registered in a site's `web.config` → `bin` is enough.
- If you ever switch to the real `system.webServer/rewrite` section: add `<remove name="RewriteModule" />`.

### 5.5 Start IIS Express

```
iisexpress.exe /config:C:\work\applicationhost.config /site:MySite
```

---

## 6. Module (.NET Framework 4.8) — reading the rules

### 6.1 Approach

- Use the **in-process** API `Microsoft.Web.Administration.WebConfigurationManager.GetSection(HttpContext, string)`.
  It returns the **effective** config for the request URL, with `applicationHost.config`, `<location>` blocks
  and `web.config` files merged, just as URL Rewrite sees it.
  - ⚠ VERIFY: that this overload exists and works inside `iisexpress.exe` with the Microsoft.Web.Administration
    version IIS Express ships.
- **Don't** use the book's approach as written (Ch. 12, p. 380: `new ServerManager().GetApplicationHostConfiguration()` in `Init`):
  - **Wrong file:** inside IIS Express, the parameterless `ServerManager` opens **full IIS's** `applicationHost.config`.
  - **Missing overrides:** it ignores `web.config`.
  - **Cost:** it is a heavy COM call plus a full file parse.
  - Also, `Init` runs once per pooled `HttpApplication` instance, not once per request as the book says.
- Convert the config elements to plain objects (POCOs) **once** and cache them, with each regex compiled.
  Never touch `ConfigurationElement` on the per-request path.

### 6.2 Project setup

- Class library, .NET Framework 4.8, references `System.Web` and `Microsoft.Web.Administration`.
- ⚠ VERIFY: which Microsoft.Web.Administration to reference. IIS Express ships its own copy, which may have a
  different assembly version from full IIS's `system32\inetsrv` copy. Make sure that version resolves inside
  `iisexpress.exe` (check the GAC, or add a binding redirect).
- Strong-name the assembly if it will be GAC-installed (§5.4).

### 6.3 Code

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;
using Mwa = Microsoft.Web.Administration;

namespace ArrLite
{
    public enum ActionType { None = 0, Rewrite = 1, Redirect = 2, CustomResponse = 3, AbortRequest = 4 }

    public sealed class Condition
    {
        public Condition(string input, string pattern, bool negate, bool ignoreCase)
        {
            Input = input;
            Negate = negate;
            Regex = new Regex(pattern ?? string.Empty,
                RegexOptions.Compiled | RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : 0));
        }
        public string Input { get; }      // e.g. "{HTTP_HOST}"
        public Regex Regex { get; }
        public bool Negate { get; }
    }

    public sealed class Rule
    {
        public string Name;
        public bool StopProcessing;
        public Regex Match;
        public bool Negate;
        public bool MatchAny;
        public readonly List<Condition> Conditions = new List<Condition>();
        public ActionType ActionType;
        public string ActionUrl;
        public bool AppendQueryString;
        public int RedirectStatus;        // 301/302/303/307 (enum values == status codes)
    }

    public sealed class ArrLiteModule : IHttpModule
    {
        const string SectionPath = "system.webServer/arrLite/rules";

        // Effective rules depend on the config path. Cache per directory.
        // A web.config change recycles the AppDomain and clears this.
        // An applicationHost.config change may NOT (⚠ VERIFY) — restart IIS Express or add a FileSystemWatcher.
        static readonly ConcurrentDictionary<string, IReadOnlyList<Rule>> Cache =
            new ConcurrentDictionary<string, IReadOnlyList<Rule>>(StringComparer.OrdinalIgnoreCase);

        public void Init(HttpApplication app) => app.BeginRequest += OnBeginRequest;
        public void Dispose() { }

        void OnBeginRequest(object sender, EventArgs e)
        {
            HttpContext ctx = ((HttpApplication)sender).Context;
            string key = VirtualPathUtility.GetDirectory(ctx.Request.Path);
            IReadOnlyList<Rule> rules = Cache.GetOrAdd(key, _ => Load(ctx));
            RuleEngine.Execute(ctx, rules);
        }

        static IReadOnlyList<Rule> Load(HttpContext ctx)
        {
            Mwa.ConfigurationSection section = Mwa.WebConfigurationManager.GetSection(ctx, SectionPath);

            var result = new List<Rule>();
            foreach (Mwa.ConfigurationElement r in section.GetCollection())
            {
                if (!(bool)r["enabled"]) continue;

                Mwa.ConfigurationElement match  = r.GetChildElement("match");
                Mwa.ConfigurationElement action = r.GetChildElement("action");
                Mwa.ConfigurationElement conds  = r.GetChildElement("conditions");

                var rule = new Rule
                {
                    Name              = (string)r["name"],
                    StopProcessing    = (bool)r["stopProcessing"],
                    Match             = new Regex((string)match["url"],
                                            RegexOptions.Compiled | RegexOptions.CultureInvariant |
                                            ((bool)match["ignoreCase"] ? RegexOptions.IgnoreCase : 0)),
                    Negate            = (bool)match["negate"],
                    MatchAny          = (int)conds["logicalGrouping"] == 1,  // ⚠ VERIFY: enums come back as Int32
                    ActionType        = (ActionType)(int)action["type"],
                    ActionUrl         = (string)action["url"],
                    AppendQueryString = (bool)action["appendQueryString"],
                    RedirectStatus    = (int)action["redirectType"],
                };

                foreach (Mwa.ConfigurationElement c in conds.GetCollection())
                    rule.Conditions.Add(new Condition(
                        (string)c["input"], (string)c["pattern"], (bool)c["negate"], (bool)c["ignoreCase"]));

                result.Add(rule);
            }
            return result;
        }
    }
}
```

### 6.4 Rule engine — starting skeleton

This is a minimal skeleton to get the end-to-end path working. The semantics to match URL Rewrite are
listed in §7. Proxying (ARR) is a TODO.

```csharp
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;

namespace ArrLite
{
    static class RuleEngine
    {
        static readonly Regex ServerVar = new Regex(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled);
        static readonly Regex BackRef   = new Regex(@"\{([RC]):(\d+)\}", RegexOptions.Compiled);

        public static void Execute(HttpContext ctx, IReadOnlyList<Rule> rules)
        {
            // URL Rewrite matches the URL path WITHOUT the leading slash (and without the query string).
            string path = ctx.Request.Path.TrimStart('/');

            foreach (Rule rule in rules)
            {
                Match m = rule.Match.Match(path);
                if (m.Success == rule.Negate) continue;

                if (!EvaluateConditions(ctx, rule, out Match lastCond)) continue;

                string target = Expand(ctx, rule.ActionUrl ?? string.Empty, rule.Negate ? null : m, lastCond);

                switch (rule.ActionType)
                {
                    case ActionType.Rewrite:
                        if (IsAbsolute(target))
                        {
                            // TODO (ARR): forward the request to 'target' and stream the response back.
                            //             Should be gated by an arrLite/proxy enabled flag, like ARR.
                            return;
                        }
                        ctx.RewritePath("/" + target.TrimStart('/'), rebaseClientPath: false);
                        break;

                    case ActionType.Redirect:
                        string location = target;
                        if (rule.AppendQueryString && ctx.Request.QueryString.Count > 0)
                            location += (location.Contains("?") ? "&" : "?") + ctx.Request.QueryString;
                        ctx.Response.StatusCode = rule.RedirectStatus;
                        ctx.Response.RedirectLocation = location;
                        ctx.ApplicationInstance.CompleteRequest();
                        return;

                    case ActionType.AbortRequest:
                        ctx.Request.Abort();
                        return;

                    case ActionType.CustomResponse:
                        // TODO: statusCode / subStatusCode / statusReason / statusDescription attributes (not in schema yet)
                        ctx.ApplicationInstance.CompleteRequest();
                        return;
                }

                if (rule.StopProcessing) return;
            }
        }

        static bool EvaluateConditions(HttpContext ctx, Rule rule, out Match lastMatched)
        {
            lastMatched = null;
            if (rule.Conditions.Count == 0) return true;

            foreach (Condition c in rule.Conditions)
            {
                string input = ServerVar.Replace(c.Input, v => ctx.Request.ServerVariables[v.Groups[1].Value] ?? string.Empty);
                Match cm = c.Regex.Match(input);
                bool ok = cm.Success != c.Negate;
                if (ok && !c.Negate) lastMatched = cm;

                if (rule.MatchAny && ok) return true;
                if (!rule.MatchAny && !ok) return false;
            }
            return !rule.MatchAny;
        }

        static string Expand(HttpContext ctx, string template, Match rule, Match cond)
        {
            string s = BackRef.Replace(template, b =>
            {
                Match src = b.Groups[1].Value == "R" ? rule : cond;
                int n = int.Parse(b.Groups[2].Value);
                return src != null && n < src.Groups.Count ? src.Groups[n].Value : string.Empty;
            });
            return ServerVar.Replace(s, v => ctx.Request.ServerVariables[v.Groups[1].Value] ?? v.Value);
        }

        static bool IsAbsolute(string url) =>
            url.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase);
    }
}
```

---

## 7. Behavior to match (URL Rewrite semantics) — check against the official docs

- **Match input** is the URL path without the leading `/` and without the query string. It is
  **relative to the directory where the rule is defined** (rules in a subfolder `web.config` see the path
  relative to that folder). The skeleton only handles site-root rules.
- **Back-references:** `{R:0}` = whole match, `{R:n}` = rule group n. `{C:n}` = groups of the **last matched condition**.
- **Conditions:** `MatchAll` = every condition must match. `MatchAny` = at least one.
- **`appendQueryString`** applies to both Rewrite and Redirect. URL Rewrite appends the original query string
  to the rewritten URL as well; the skeleton's Rewrite path doesn't do this yet.
- **Ordering:** global rules (appHost, executed earlier in the pipeline) → distributed rules, parent → child config levels.
- **ARR proxying:** a Rewrite to an absolute `http(s)://` URL is forwarded through ARR, when the proxy is enabled.
  To design: forwarding with `HttpWebRequest` or `HttpClient`, header copying and `X-Forwarded-*`,
  response streaming, timeouts, `preserveHostHeader`, and a server-farm/load-balancing equivalent of `webFarms`.

---

## 8. Alternative considered (fallback if the schema route is blocked)

Keep the rules in our **own XML file**, using the same `<rule>/<match>/<conditions>/<action>` layout. Point to it
with an `appSetting`, load it with `XDocument` in a static initializer, and invalidate the cache with a `FileSystemWatcher`.

- Pros: no IIS schema, no admin rights, nothing under `Program Files`.
- Cons: no `web.config` inheritance or `<location>` merging, and the rules are invisible to IIS tooling (`appcmd`).

---

## 9. Verification checklist (do first)

1. [ ] Does the target IIS Express already ship URL Rewrite? Look for `rewrite.dll` in the install folder and
       `rewrite_schema.xml` in `config\schema`.
2. [ ] Process Monitor: confirm which `config\schema` folder `iisexpress.exe` reads (x64 vs x86, custom install).
3. [ ] With schema + declaration + one rule in place, IIS Express starts with no 500.19.
4. [ ] `Microsoft.Web.Administration.WebConfigurationManager.GetSection(HttpContext, string)` resolves and returns
       the section inside `iisexpress.exe`. Note which Microsoft.Web.Administration assembly version loaded.
5. [ ] Enum attributes (`type`, `redirectType`, `logicalGrouping`) come back as `Int32`.
6. [ ] Registry `HKLM\SOFTWARE\Microsoft\IISExpress\<version>\InstallPath` exists (only needed if not launching IIS Express ourselves).
7. [ ] Option B (Microsoft.Web.Administration in the tool): which schema folder its COM engine uses.
8. [ ] Editing `applicationHost.config` while running: does the AppDomain recycle, or is a cache-invalidation step needed?

---

## 10. Implementation tasks

1. Create `arrLite_schema.xml` (§3) as a content file in the solution.
2. Tool: `SchemaInstaller` (§5.1) plus resolution of the `iisexpress.exe` path (x64/x86/custom).
3. Tool: `ArrLiteConfigWriter` (§5.2): section declarations, rule upsert, module registration (§5.4).
4. Module project (.NET Framework 4.8, strong-named): `ArrLiteModule` + config→POCO loader (§6.3).
5. `RuleEngine` (§6.4), then align it with the §7 semantics. Add unit tests for matching, conditions and back-references
   by putting the engine behind an interface that doesn't depend on `HttpContext`.
6. Integration test: tool prepares the config → start `iisexpress.exe /config:… /site:…` → HTTP assertions
   (rewrite, redirect 301/302, condition on `{HTTP_HOST}`, `stopProcessing`).
7. Design and implement ARR proxying (§7, last bullet) and its config sections.

---

### Sources

- *Professional Microsoft IIS 8* (Wrox, 2012): Ch. 12 "Extending IIS Configuration", pp. 377–381;
  Ch. 18 "Direct Configuration", pp. 616–617.
- URL Rewrite: https://www.iis.net/downloads/microsoft/url-rewrite
- Application Request Routing: https://www.iis.net/downloads/microsoft/application-request-routing
