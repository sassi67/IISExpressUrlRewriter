using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Web;
using ArrLite.Configuration;
using ArrLite.Engine;

namespace ArrLite
{
    /// <summary>
    /// Managed IIS module (Integrated pipeline, CLR v4.0). Register under &lt;system.webServer&gt;&lt;modules&gt;
    /// WITHOUT preCondition="managedHandler" so it also sees static and extensionless requests.
    /// </summary>
    public sealed class ArrLiteModule : IHttpModule
    {
        // Effective rules depend on the config path, so they are cached per directory.
        // A web.config change recycles the AppDomain and clears this.
        // An applicationHost.config change may NOT (⚠ VERIFY spec §9.8): restart IIS Express.
        private static readonly ConcurrentDictionary<string, IReadOnlyList<Rule>> Cache =
            new ConcurrentDictionary<string, IReadOnlyList<Rule>>(StringComparer.OrdinalIgnoreCase);

        public void Init(HttpApplication application)
        {
            application.BeginRequest += OnBeginRequest;
        }

        public void Dispose()
        {
        }

        private static void OnBeginRequest(object sender, EventArgs e)
        {
            HttpContext context = ((HttpApplication)sender).Context;

            string key = VirtualPathUtility.GetDirectory(context.Request.Path);
            IReadOnlyList<Rule> rules = Cache.GetOrAdd(key, _ => RuleLoader.Load(context));

            RuleResult result = RuleEngine.Evaluate(new HttpRequestContext(context.Request), rules);
            Apply(context, result);
        }

        private static void Apply(HttpContext context, RuleResult result)
        {
            switch (result.Kind)
            {
                case RuleResultKind.Rewrite:
                    context.RewritePath(result.Url, rebaseClientPath: false);
                    break;

                case RuleResultKind.Redirect:
                    context.Response.StatusCode = result.StatusCode;
                    context.Response.RedirectLocation = result.Url;
                    context.ApplicationInstance.CompleteRequest();
                    break;

                case RuleResultKind.Proxy:
                    // TODO (ARR, spec §7): forward the request to result.Url and stream the response back.
                    context.Response.StatusCode = 501;
                    context.Response.StatusDescription = "ArrLite proxying not implemented";
                    context.ApplicationInstance.CompleteRequest();
                    break;

                case RuleResultKind.CustomResponse:
                    // TODO: statusCode / subStatusCode / statusReason / statusDescription (not in schema yet).
                    context.ApplicationInstance.CompleteRequest();
                    break;

                case RuleResultKind.AbortRequest:
                    context.Request.Abort();
                    break;
            }
        }
    }
}
