# IISExpressUrlRewriter

**ArrLite** — a managed IIS module (.NET Framework 4.8.1) for IIS Express that replicates the parts of
URL Rewrite and ARR we need. Design and open questions: [doc/IISEXPRESS_REWRITE_MODULE_SPEC.md](doc/IISEXPRESS_REWRITE_MODULE_SPEC.md).

## Layout

| Path | Content |
| --- | --- |
| `src/ArrLite` | The module (strong-named class library, `net481`) and `Schema/arrLite_schema.xml` |
| `test/ArrLite.Tests` | NUnit tests for the rule engine and the schema file |
| `deploy/Install-ArrLite.ps1` | Installs schema + GAC assembly + `applicationHost.config` entries into IIS Express |

`src/ArrLite`:

- `ArrLiteModule` — `IHttpModule`; on `BeginRequest` loads the effective rules (cached per directory) and applies the result.
- `Configuration/` — rule POCOs (`Rule`, `Condition`, `ActionType`) and `RuleLoader` (reads `system.webServer/arrLite/rules` through `Microsoft.Web.Administration.WebConfigurationManager`).
- `Engine/` — `RuleEngine`, independent of `HttpContext` (works on `IRequestContext`), returning a `RuleResult`.

## Build and test

```bash
dotnet test ArrLite.sln
```

```bash
dotnet build ArrLite.sln -c Release
```

## Install into IIS Express

From an **elevated Windows PowerShell 5.1** prompt (not `pwsh`), after a Release build:

```bash
powershell -ExecutionPolicy Bypass -File deploy\Install-ArrLite.ps1 -ConfigPath "C:\work\applicationhost.config"
```

Defaults: `-IisExpressPath "%ProgramFiles%\IIS Express"`, `-ConfigPath` = the per-user
`Documents\IISExpress\config\applicationhost.config`. Repeat with `-IisExpressPath "%ProgramFiles(x86)%\IIS Express"`
for the x86 instance. Visual Studio projects use `.vs\<solution>\config\applicationhost.config`.

Then add rules in the site's `web.config` (or a `<location>` in `applicationHost.config`):

```xml
<system.webServer>
  <arrLite>
    <rules>
      <rule name="old-to-new" stopProcessing="true">
        <match url="^old/(.*)" />
        <action type="Redirect" url="/new/{R:1}" redirectType="Found" />
      </rule>
    </rules>
  </arrLite>
</system.webServer>
```

The site must run in an **Integrated** pipeline app pool with **CLR v4.0**.

## Status

Implemented: rule matching (negate, ignoreCase), conditions (MatchAll/MatchAny, negate), `{R:n}`/`{C:n}`/`{SERVER_VARIABLE}`
expansion, Rewrite (chained, with `appendQueryString`), Redirect (301/302/303/307), AbortRequest, CustomResponse (no status yet).

Not yet: ARR proxying (a Rewrite to an absolute URL currently returns **501**), `globalRules` execution,
rules relative to sub-folder `web.config`, rewrite maps, outbound rules.
