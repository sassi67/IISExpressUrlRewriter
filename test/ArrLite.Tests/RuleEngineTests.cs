using System.Collections.Generic;
using ArrLite.Configuration;
using ArrLite.Engine;
using NUnit.Framework;

namespace ArrLite.Tests
{
    [TestFixture]
    public class RuleEngineTests
    {
        private static RuleResult Evaluate(IRequestContext request, params Rule[] rules) =>
            RuleEngine.Evaluate(request, new List<Rule>(rules));

        [Test]
        public void NoRules_ReturnsNone()
        {
            RuleResult result = Evaluate(new FakeRequestContext("/anything"));

            Assert.That(result.Kind, Is.EqualTo(RuleResultKind.None));
        }

        [Test]
        public void NonMatchingRule_ReturnsNone()
        {
            var rule = new Rule("r", "^api/(.*)", ActionType.Rewrite, "backend/{R:1}");

            RuleResult result = Evaluate(new FakeRequestContext("/home"), rule);

            Assert.That(result.Kind, Is.EqualTo(RuleResultKind.None));
        }

        [Test]
        public void Match_IsAgainstPathWithoutLeadingSlash()
        {
            var rule = new Rule("r", "^api$", ActionType.Rewrite, "backend");

            RuleResult result = Evaluate(new FakeRequestContext("/api"), rule);

            Assert.That(result.Kind, Is.EqualTo(RuleResultKind.Rewrite));
            Assert.That(result.Url, Is.EqualTo("/backend"));
        }

        [Test]
        public void Match_IgnoresCaseByDefault()
        {
            var rule = new Rule("r", "^api$", ActionType.Rewrite, "backend");

            Assert.That(Evaluate(new FakeRequestContext("/API"), rule).Kind, Is.EqualTo(RuleResultKind.Rewrite));
        }

        [Test]
        public void Match_IsCaseSensitive_WhenIgnoreCaseIsFalse()
        {
            var rule = new Rule("r", "^api$", ActionType.Rewrite, "backend", ignoreCase: false);

            Assert.That(Evaluate(new FakeRequestContext("/API"), rule).Kind, Is.EqualTo(RuleResultKind.None));
        }

        [Test]
        public void NegatedMatch_FiresWhenPatternDoesNotMatch()
        {
            var rule = new Rule("r", "^index\\.html$", ActionType.Rewrite, "index.html", negate: true);

            Assert.That(Evaluate(new FakeRequestContext("/other"), rule).Url, Is.EqualTo("/index.html"));
            Assert.That(Evaluate(new FakeRequestContext("/index.html"), rule).Kind, Is.EqualTo(RuleResultKind.None));
        }

        [Test]
        public void Rewrite_ExpandsRuleBackReferences()
        {
            var rule = new Rule("r", "^products/(\\w+)/(\\d+)$", ActionType.Rewrite, "product.aspx?cat={R:1}&id={R:2}&all={R:0}");

            RuleResult result = Evaluate(new FakeRequestContext("/products/books/42"), rule);

            Assert.That(result.Url, Is.EqualTo("/product.aspx?cat=books&id=42&all=products/books/42"));
        }

        [Test]
        public void Rewrite_AppendsOriginalQueryString_ByDefault()
        {
            var rule = new Rule("r", "^a$", ActionType.Rewrite, "b?x=1");

            RuleResult result = Evaluate(new FakeRequestContext("/a", "y=2"), rule);

            Assert.That(result.Url, Is.EqualTo("/b?x=1&y=2"));
        }

        [Test]
        public void Rewrite_DropsOriginalQueryString_WhenAppendQueryStringIsFalse()
        {
            var rule = new Rule("r", "^a$", ActionType.Rewrite, "b", appendQueryString: false);

            RuleResult result = Evaluate(new FakeRequestContext("/a", "y=2"), rule);

            Assert.That(result.Url, Is.EqualTo("/b"));
        }

        [Test]
        public void Rewrite_WithoutStopProcessing_FeedsRewrittenPathToNextRule()
        {
            var first = new Rule("first", "^a$", ActionType.Rewrite, "b");
            var second = new Rule("second", "^b$", ActionType.Rewrite, "c");

            RuleResult result = Evaluate(new FakeRequestContext("/a"), first, second);

            Assert.That(result.Url, Is.EqualTo("/c"));
            Assert.That(result.RuleName, Is.EqualTo("second"));
        }

        [Test]
        public void StopProcessing_SkipsFollowingRules()
        {
            var first = new Rule("first", "^a$", ActionType.Rewrite, "b", stopProcessing: true);
            var second = new Rule("second", "^b$", ActionType.Rewrite, "c");

            RuleResult result = Evaluate(new FakeRequestContext("/a"), first, second);

            Assert.That(result.Url, Is.EqualTo("/b"));
            Assert.That(result.RuleName, Is.EqualTo("first"));
        }

        [Test]
        public void Rewrite_ToAbsoluteUrl_ReturnsProxy()
        {
            var rule = new Rule("api", "^api/(.*)", ActionType.Rewrite, "http://localhost:5001/{R:1}");

            RuleResult result = Evaluate(new FakeRequestContext("/api/orders", "page=2"), rule);

            Assert.That(result.Kind, Is.EqualTo(RuleResultKind.Proxy));
            Assert.That(result.Url, Is.EqualTo("http://localhost:5001/orders?page=2"));
        }

        [TestCase(301)]
        [TestCase(302)]
        [TestCase(303)]
        [TestCase(307)]
        public void Redirect_UsesConfiguredStatusCode(int status)
        {
            var rule = new Rule("r", "^old/(.*)", ActionType.Redirect, "/new/{R:1}", redirectStatus: status);

            RuleResult result = Evaluate(new FakeRequestContext("/old/page"), rule);

            Assert.That(result.Kind, Is.EqualTo(RuleResultKind.Redirect));
            Assert.That(result.StatusCode, Is.EqualTo(status));
            Assert.That(result.Url, Is.EqualTo("/new/page"));
        }

        [Test]
        public void Redirect_AppendsQueryString()
        {
            var rule = new Rule("r", "^old$", ActionType.Redirect, "/new?a=1");

            RuleResult result = Evaluate(new FakeRequestContext("/old", "b=2"), rule);

            Assert.That(result.Url, Is.EqualTo("/new?a=1&b=2"));
        }

        [Test]
        public void Redirect_StopsEvaluation_EvenWithoutStopProcessing()
        {
            var redirect = new Rule("redirect", "^a$", ActionType.Redirect, "/b");
            var rewrite = new Rule("rewrite", ".*", ActionType.Rewrite, "c");

            RuleResult result = Evaluate(new FakeRequestContext("/a"), redirect, rewrite);

            Assert.That(result.Kind, Is.EqualTo(RuleResultKind.Redirect));
        }

        [Test]
        public void Condition_OnServerVariable_MustMatch()
        {
            var rule = new Rule("r", ".*", ActionType.Redirect, "https://www.example.com/{R:0}",
                conditions: new[] { new Condition("{HTTP_HOST}", "^example\\.com$") });

            RuleResult hit = Evaluate(new FakeRequestContext("/x").WithServerVariable("HTTP_HOST", "example.com"), rule);
            RuleResult miss = Evaluate(new FakeRequestContext("/x").WithServerVariable("HTTP_HOST", "www.example.com"), rule);

            Assert.That(hit.Url, Is.EqualTo("https://www.example.com/x"));
            Assert.That(miss.Kind, Is.EqualTo(RuleResultKind.None));
        }

        [Test]
        public void Conditions_MatchAll_RequiresEveryCondition()
        {
            var rule = new Rule("r", ".*", ActionType.Rewrite, "ok",
                conditions: new[]
                {
                    new Condition("{HTTP_HOST}", "^a$"),
                    new Condition("{HTTPS}", "^on$"),
                });

            var request = new FakeRequestContext("/x").WithServerVariable("HTTP_HOST", "a").WithServerVariable("HTTPS", "off");

            Assert.That(Evaluate(request, rule).Kind, Is.EqualTo(RuleResultKind.None));
        }

        [Test]
        public void Conditions_MatchAny_RequiresOneCondition()
        {
            var rule = new Rule("r", ".*", ActionType.Rewrite, "ok", matchAnyCondition: true,
                conditions: new[]
                {
                    new Condition("{HTTP_HOST}", "^a$"),
                    new Condition("{HTTPS}", "^on$"),
                });

            var request = new FakeRequestContext("/x").WithServerVariable("HTTP_HOST", "a").WithServerVariable("HTTPS", "off");

            Assert.That(Evaluate(request, rule).Kind, Is.EqualTo(RuleResultKind.Rewrite));
        }

        [Test]
        public void NegatedCondition_FiresWhenPatternDoesNotMatch()
        {
            var rule = new Rule("r", ".*", ActionType.Rewrite, "ok",
                conditions: new[] { new Condition("{HTTPS}", "^on$", negate: true) });

            Assert.That(Evaluate(new FakeRequestContext("/x").WithServerVariable("HTTPS", "off"), rule).Kind, Is.EqualTo(RuleResultKind.Rewrite));
            Assert.That(Evaluate(new FakeRequestContext("/x").WithServerVariable("HTTPS", "on"), rule).Kind, Is.EqualTo(RuleResultKind.None));
        }

        [Test]
        public void ConditionBackReferences_UseLastMatchedCondition()
        {
            var rule = new Rule("r", ".*", ActionType.Rewrite, "sites/{C:1}/{R:0}",
                conditions: new[]
                {
                    new Condition("{HTTP_HOST}", "^(\\w+)\\.example\\.com$"),
                    new Condition("{SERVER_PORT}", "^(\\d+)$"),
                });

            var request = new FakeRequestContext("/page").WithServerVariable("HTTP_HOST", "shop.example.com").WithServerVariable("SERVER_PORT", "8080");

            Assert.That(Evaluate(request, rule).Url, Is.EqualTo("/sites/8080/page"));
        }

        [Test]
        public void ConditionInput_CanUseRuleBackReferences()
        {
            var rule = new Rule("r", "^(\\w+)/.*", ActionType.Rewrite, "matched",
                conditions: new[] { new Condition("{R:1}", "^admin$") });

            Assert.That(Evaluate(new FakeRequestContext("/admin/users"), rule).Kind, Is.EqualTo(RuleResultKind.Rewrite));
            Assert.That(Evaluate(new FakeRequestContext("/public/users"), rule).Kind, Is.EqualTo(RuleResultKind.None));
        }

        [Test]
        public void ActionUrl_ExpandsServerVariables_AndKeepsUnknownOnesVerbatim()
        {
            var rule = new Rule("r", ".*", ActionType.Redirect, "https://{HTTP_HOST}/{R:0}/{UNKNOWN_VAR}");

            RuleResult result = Evaluate(new FakeRequestContext("/x").WithServerVariable("HTTP_HOST", "h"), rule);

            Assert.That(result.Url, Is.EqualTo("https://h/x/{UNKNOWN_VAR}"));
        }

        [Test]
        public void MissingBackReferenceGroup_ExpandsToEmpty()
        {
            var rule = new Rule("r", "^a$", ActionType.Rewrite, "b{R:5}");

            Assert.That(Evaluate(new FakeRequestContext("/a"), rule).Url, Is.EqualTo("/b"));
        }

        [Test]
        public void AbortRequest_ReturnsAbort()
        {
            var rule = new Rule("block", "^secret", ActionType.AbortRequest);

            RuleResult result = Evaluate(new FakeRequestContext("/secret/file"), rule);

            Assert.That(result.Kind, Is.EqualTo(RuleResultKind.AbortRequest));
            Assert.That(result.RuleName, Is.EqualTo("block"));
        }

        [Test]
        public void CustomResponse_ReturnsCustomResponse()
        {
            var rule = new Rule("custom", "^x$", ActionType.CustomResponse);

            Assert.That(Evaluate(new FakeRequestContext("/x"), rule).Kind, Is.EqualTo(RuleResultKind.CustomResponse));
        }

        [Test]
        public void NoneAction_DoesNothing_ButHonoursStopProcessing()
        {
            var none = new Rule("none", "^a$", ActionType.None, stopProcessing: true);
            var rewrite = new Rule("rewrite", "^a$", ActionType.Rewrite, "b");

            Assert.That(Evaluate(new FakeRequestContext("/a"), none, rewrite).Kind, Is.EqualTo(RuleResultKind.None));
        }
    }
}
