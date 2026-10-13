using System.Web;

namespace ArrLite.Engine
{
    /// <summary>Adapts <see cref="HttpRequest"/> to <see cref="IRequestContext"/>.</summary>
    internal sealed class HttpRequestContext : IRequestContext
    {
        private readonly HttpRequest _request;

        public HttpRequestContext(HttpRequest request)
        {
            _request = request;
        }

        // TODO (spec §7): rules defined in a sub-folder web.config must see the path relative to that folder.
        public string Path => _request.Path;

        // Raw query string, not Request.QueryString.ToString(), which re-encodes it.
        public string QueryString => _request.Url.Query.TrimStart('?');

        public string GetServerVariable(string name) => _request.ServerVariables[name];
    }
}
