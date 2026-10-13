using System;
using System.Collections.Generic;
using ArrLite.Engine;

namespace ArrLite.Tests
{
    internal sealed class FakeRequestContext : IRequestContext
    {
        private readonly Dictionary<string, string> _serverVariables =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public FakeRequestContext(string path, string queryString = "")
        {
            Path = path;
            QueryString = queryString;
        }

        public string Path { get; }

        public string QueryString { get; }

        public FakeRequestContext WithServerVariable(string name, string value)
        {
            _serverVariables[name] = value;
            return this;
        }

        public string GetServerVariable(string name) =>
            _serverVariables.TryGetValue(name, out string value) ? value : null;
    }
}
