namespace ArrLite.Engine
{
    public enum RuleResultKind
    {
        /// <summary>No rule changed the request.</summary>
        None,

        /// <summary>The request must be rewritten to <see cref="RuleResult.Url"/> (server-relative).</summary>
        Rewrite,

        /// <summary>The client must be redirected to <see cref="RuleResult.Url"/> with <see cref="RuleResult.StatusCode"/>.</summary>
        Redirect,

        /// <summary>The request must be proxied to the absolute <see cref="RuleResult.Url"/> (ARR).</summary>
        Proxy,

        CustomResponse,

        AbortRequest,
    }

    /// <summary>Outcome of <see cref="RuleEngine.Evaluate"/>; applied to the real request by the module.</summary>
    public sealed class RuleResult
    {
        public static readonly RuleResult None = new RuleResult(RuleResultKind.None, null, null, 0);

        private RuleResult(RuleResultKind kind, string ruleName, string url, int statusCode)
        {
            Kind = kind;
            RuleName = ruleName;
            Url = url;
            StatusCode = statusCode;
        }

        public RuleResultKind Kind { get; }

        /// <summary>Name of the last rule whose action produced this result.</summary>
        public string RuleName { get; }

        public string Url { get; }

        public int StatusCode { get; }

        public static RuleResult Rewrite(string ruleName, string url) => new RuleResult(RuleResultKind.Rewrite, ruleName, url, 0);

        public static RuleResult Redirect(string ruleName, string url, int statusCode) => new RuleResult(RuleResultKind.Redirect, ruleName, url, statusCode);

        public static RuleResult Proxy(string ruleName, string url) => new RuleResult(RuleResultKind.Proxy, ruleName, url, 0);

        public static RuleResult CustomResponse(string ruleName) => new RuleResult(RuleResultKind.CustomResponse, ruleName, null, 0);

        public static RuleResult AbortRequest(string ruleName) => new RuleResult(RuleResultKind.AbortRequest, ruleName, null, 0);
    }
}
