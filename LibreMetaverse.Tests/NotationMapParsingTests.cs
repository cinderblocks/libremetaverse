using System.IO;
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

        [TestCase("{}")]
        [TestCase("[]")]
        [TestCase("{'a':{},'b':i2}")]
        [TestCase("{'a':[],'b':i2}")]
        [TestCase("[{},i2]")]
        [TestCase("[[],i2]")]
        [TestCase("{'a':{'b':[]},'c':i2}")]
        [TestCase("[{},[],{'x':[]},[{}]]")]
        [TestCase("{'id':i1,'od':[{},{}],'te':[i0,i1]}")]
        [TestCase("[ { } , [ ] ]")]
        public void EmptyContainers_ConsumeTheirClosingDelimiter(string notation)
        {
            using (var reader = new StringReader(notation))
            {
                var osd = OSDParser.DeserializeLLSDNotation(reader);

                Assert.That(reader.Peek(), Is.LessThan(0), "all of the input was read");
                var reparsed = OSDParser.DeserializeLLSDNotation(OSDParser.SerializeLLSDNotation(osd));
                Assert.That(OSDParser.SerializeLLSDNotation(reparsed), Is.EqualTo(OSDParser.SerializeLLSDNotation(osd)));
            }
        }

        [Test]
        public void MaterialOverridePayload_WithEmptyOverrides_Parses()
        {
            var map = (OSDMap)OSDParser.DeserializeLLSDNotation("{'id':i1,'od':[{},{}],'te':[i0,i1]}");

            Assert.That(map["id"].AsInteger(), Is.EqualTo(1));
            Assert.That(((OSDArray)map["od"]).Count, Is.EqualTo(2));
            Assert.That(((OSDMap)((OSDArray)map["od"])[0]).Count, Is.EqualTo(0));
            Assert.That(((OSDArray)map["te"]).Count, Is.EqualTo(2));
            Assert.That(((OSDArray)map["te"])[1].AsInteger(), Is.EqualTo(1));
        }

        [TestCase("[i1 i2]", "Invalid delimiter in array")]
        [TestCase("{'a':i1 'b':i2}", "Invalid delimiter in map")]
        [TestCase("[{}", "Unexpected end of array")]
        [TestCase("{'a':[]", "Unexpected end of map")]
        public void MalformedContainers_AreRejected(string notation, string message)
        {
            var ex = Assert.Throws<OSDException>(() => OSDParser.DeserializeLLSDNotation(notation));

            Assert.That(ex.Message, Does.Contain(message));
        }
    }
}
