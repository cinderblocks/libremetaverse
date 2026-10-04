using System;
using System.Globalization;
using LibreMetaverse.StructuredData;
using NUnit.Framework;

namespace LibreMetaverse.Tests
{
    /// <summary>
    /// LLSD numbers always use '.' as the decimal separator. Reading them back out of an
    /// <see cref="OSDString"/> must not depend on the culture of the machine.
    /// </summary>
    [TestFixture]
    public class OSDStringCultureTests
    {
        private CultureInfo _original;

        [SetUp]
        public void SetUp()
        {
            _original = CultureInfo.CurrentCulture;

            // A comma-decimal culture such as de-DE or fr-FR. Built by hand so the test doesn't
            // depend on which cultures the runtime has data for.
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ",";
            culture.NumberFormat.NumberGroupSeparator = ".";
            CultureInfo.CurrentCulture = culture;
        }

        [TearDown]
        public void TearDown() => CultureInfo.CurrentCulture = _original;

        [Test]
        public void AsReal_ParsesDotDecimalRegardlessOfCulture()
        {
            Assert.That(OSD.FromString("1.5").AsReal(), Is.EqualTo(1.5));
            Assert.That(OSD.FromString("0.25").AsReal(), Is.EqualTo(0.25));
            Assert.That(OSD.FromString("-3.75").AsReal(), Is.EqualTo(-3.75));
            Assert.That(OSD.FromString("1e3").AsReal(), Is.EqualTo(1000d));
        }

        [Test]
        public void IntegerAccessors_DoNotTreatDotAsThousandsSeparator()
        {
            // With a comma-decimal culture "0.5" used to parse as 5 and "12.7" as 127
            Assert.That(OSD.FromString("0.5").AsInteger(), Is.EqualTo(0));
            Assert.That(OSD.FromString("12.7").AsInteger(), Is.EqualTo(12));
            Assert.That(OSD.FromString("12.7").AsUInteger(), Is.EqualTo(12u));
            Assert.That(OSD.FromString("12.7").AsLong(), Is.EqualTo(12L));
            Assert.That(OSD.FromString("12.7").AsULong(), Is.EqualTo(12UL));
        }

        [Test]
        public void WholeNumbers_StillParse()
        {
            Assert.That(OSD.FromString("42").AsInteger(), Is.EqualTo(42));
            Assert.That(OSD.FromString("4294967295").AsUInteger(), Is.EqualTo(uint.MaxValue));
            Assert.That(OSD.FromString("-9000000000").AsLong(), Is.EqualTo(-9000000000L));
        }

        [Test]
        public void NonNumericStrings_StillYieldZero()
        {
            Assert.That(OSD.FromString("abc").AsReal(), Is.EqualTo(0d));
            Assert.That(OSD.FromString("").AsInteger(), Is.EqualTo(0));
        }

        [Test]
        public void RealRoundTripsThroughStringInAnyCulture()
        {
            var text = OSD.FromReal(1234.5678).AsString();
            Assert.That(OSD.FromString(text).AsReal(), Is.EqualTo(1234.5678));
        }
    }
}
