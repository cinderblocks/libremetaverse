using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class NotationMapParsingTests
    {
        [Test]
        public void Map_WithKeyValueDelimiter_Parses()
        {
            var map = (OSDMap)OSDParser.DeserializeLLSDNotation("{'a':i1,'b':'two'}");

            Assert.That(map["a"].AsInteger(), Is.EqualTo(1));
            Assert.That(map["b"].AsString(), Is.EqualTo("two"));
        }

        [Test]
        public void Map_WrongDelimiterAfterKey_ReportsInvalidDelimiter()
        {
            var ex = Assert.Throws<OSDException>(() => OSDParser.DeserializeLLSDNotation("{'a' i1}"));

            Assert.That(ex.Message, Does.Contain("Invalid delimiter in map"));
        }

        [Test]
        public void Map_EndsAfterKey_ReportsUnexpectedEnd()
        {
            var ex = Assert.Throws<OSDException>(() => OSDParser.DeserializeLLSDNotation("{'a'"));

            Assert.That(ex.Message, Does.Contain("Unexpected end of stream in map"));
        }

        [TestCase("r1.5", 1.5)]
        [TestCase("r-1.5", -1.5)]
        [TestCase("r+1.5", 1.5)]
        [TestCase("r2.5e3", 2500d)]
        [TestCase("r-2.5E-1", -0.25)]
        public void Real_ParsesSignedValues(string notation, double expected)
        {
            Assert.That(OSDParser.DeserializeLLSDNotation(notation).AsReal(), Is.EqualTo(expected));
        }
    }
}
