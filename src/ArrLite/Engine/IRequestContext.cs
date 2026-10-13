namespace ArrLite.Engine
{
    /// <summary>
    /// The request data the rule engine needs. Keeps <see cref="RuleEngine"/> independent of
    /// <c>System.Web.HttpContext</c> so it can be unit tested.
    /// </summary>
    public interface IRequestContext
    {
        /// <summary>URL path, with or without the leading slash, without the query string.</summary>
        string Path { get; }

        /// <summary>Raw query string without the leading '?', or an empty string.</summary>
        string QueryString { get; }

        /// <summary>Value of a server variable (e.g. HTTP_HOST), or <c>null</c> if it isn't defined.</summary>
        string GetServerVariable(string name);
    }
}
