using System.Text;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// Parsing recurses once per level of nesting, and a stack overflow cannot be caught, so every LLSD
    /// parser has to refuse input nested deeper than any real document.
    /// </summary>
    [TestFixture]
    public class LLSDNestingTests
    {
        private static string XmlNested(int depth, string open, string close, string leaf)
        {
            var sb = new StringBuilder("<llsd>");
            for (int i = 0; i < depth; i++) sb.Append(open);
            sb.Append(leaf);
            for (int i = 0; i < depth; i++) sb.Append(close);
            return sb.Append("</llsd>").ToString();
        }

        private static string NotationNested(int depth, string open, string close, string leaf) =>
            new StringBuilder().Insert(0, open, depth).Append(leaf).Insert(depth + leaf.Length, close, depth).ToString();

        [Test]
        public void Xml_DeeplyNestedArrays_AreRefused()
        {
            var xml = XmlNested(100000, "<array>", "</array>", "<string>x</string>");
            Assert.Throws<OSDException>(() => OSDParser.DeserializeLLSDXml(xml));
        }

        [Test]
        public void Xml_DeeplyNestedMaps_AreRefused()
        {
            var sb = new StringBuilder("<llsd>");
            for (int i = 0; i < 100000; i++) sb.Append("<map><key>k</key>");
            sb.Append("<string>x</string>");
            for (int i = 0; i < 100000; i++) sb.Append("</map>");
            sb.Append("</llsd>");
            Assert.Throws<OSDException>(() => OSDParser.DeserializeLLSDXml(sb.ToString()));
        }

        [Test]
        public void Xml_NestingWithinTheLimit_Parses()
        {
            var xml = XmlNested(OSDParser.MaxXmlNestingDepth, "<array>", "</array>", "<string>leaf</string>");
            OSD osd = OSDParser.DeserializeLLSDXml(xml);
            for (int i = 0; i < OSDParser.MaxXmlNestingDepth; i++) osd = ((OSDArray)osd)[0];
            Assert.That(osd.AsString(), Is.EqualTo("leaf"));
        }

        [Test]
        public void Xml_NestingOneBeyondTheLimit_IsRefused()
        {
            var xml = XmlNested(OSDParser.MaxXmlNestingDepth + 1, "<array>", "</array>", "<string>leaf</string>");
            Assert.Throws<OSDException>(() => OSDParser.DeserializeLLSDXml(xml));
        }

        [Test]
        public void Notation_DeeplyNestedArrays_AreRefused()
        {
            Assert.Throws<OSDException>(() => OSDParser.DeserializeLLSDNotation(NotationNested(100000, "[", "]", "'x'")));
        }

        [Test]
        public void Notation_DeeplyNestedMaps_AreRefused()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 100000; i++) sb.Append("{'k':");
            sb.Append("'x'");
            for (int i = 0; i < 100000; i++) sb.Append('}');
            Assert.Throws<OSDException>(() => OSDParser.DeserializeLLSDNotation(sb.ToString()));
        }

        [Test]
        public void Notation_NestingWithinTheLimit_Parses()
        {
            OSD osd = OSDParser.DeserializeLLSDNotation(NotationNested(OSDParser.MaxNotationNestingDepth, "[", "]", "'leaf'"));
            for (int i = 0; i < OSDParser.MaxNotationNestingDepth; i++) osd = ((OSDArray)osd)[0];
            Assert.That(osd.AsString(), Is.EqualTo("leaf"));
        }

        [Test]
        public void Notation_NestingOneBeyondTheLimit_IsRefused()
        {
            Assert.Throws<OSDException>(() => OSDParser.DeserializeLLSDNotation(NotationNested(OSDParser.MaxNotationNestingDepth + 1, "[", "]", "'leaf'")));
        }
    }
}
