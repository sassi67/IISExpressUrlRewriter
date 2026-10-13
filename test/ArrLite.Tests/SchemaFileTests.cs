using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ArrLite.Configuration;
using NUnit.Framework;

namespace ArrLite.Tests
{
    [TestFixture]
    public class SchemaFileTests
    {
        private static XDocument LoadSchema() =>
            XDocument.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "Schema", "arrLite_schema.xml"));

        [Test]
        public void Schema_DeclaresRulesAndGlobalRulesSections()
        {
            string[] sections = LoadSchema().Root.Elements("sectionSchema")
                .Select(s => (string)s.Attribute("name"))
                .ToArray();

            Assert.That(sections, Is.EquivalentTo(new[]
            {
                "system.webServer/arrLite/rules",
                "system.webServer/arrLite/globalRules",
            }));
        }

        [Test]
        public void Schema_ActionTypeEnum_MatchesActionTypeValues()
        {
            foreach (XElement section in LoadSchema().Root.Elements("sectionSchema"))
            {
                XElement typeAttribute = section.Descendants("element")
                    .Single(e => (string)e.Attribute("name") == "action")
                    .Elements("attribute")
                    .Single(a => (string)a.Attribute("name") == "type");

                foreach (XElement value in typeAttribute.Elements("enum"))
                {
                    var parsed = (ActionType)Enum.Parse(typeof(ActionType), (string)value.Attribute("name"));
                    Assert.That((int)parsed, Is.EqualTo((int)value.Attribute("value")), (string)section.Attribute("name"));
                }
            }
        }
    }
}
