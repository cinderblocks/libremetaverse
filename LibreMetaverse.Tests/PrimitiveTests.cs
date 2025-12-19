using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace LibreMetaverse.Tests
{
    [TestFixture]
    public class PrimitiveTests
    {
        [Test]
        public void GetOSD_FromOSD_Roundtrip_Minimal()
        {
            Primitive prim = new Primitive();
            prim.ID = UUID.Random();
            prim.LocalID = 12345;
            prim.ParentID = 54321;
            prim.Flags = PrimFlags.Phantom | PrimFlags.Physics | PrimFlags.CastShadows;
            prim.Position = new Vector3(1.1f, 2.2f, 3.3f);
            prim.Rotation = new Quaternion(0.1f, 0.2f, 0.3f, 0.9f);
            prim.Scale = new Vector3(0.5f, 0.6f, 0.7f);

            var cd = new Primitive.ConstructionData();
            cd.Material = Material.Wood; // enum
            cd.PCode = PCode.Prim;
            cd.State = 7;
            cd.PathCurve = PathCurve.Circle;
            cd.PathScaleX = 1.2f;
            cd.PathScaleY = 0.8f;
            cd.ProfileBegin = 0.0f;
            cd.ProfileEnd = 1.0f;
            cd.ProfileHollow = 0.1f;
            cd.ProfileCurve = ProfileCurve.Square;
            cd.ProfileHole = (HoleType)1;

            prim.PrimData = cd;

            prim.Properties = new Primitive.ObjectProperties();
            prim.Properties.Name = "TestPrim";
            prim.Properties.Description = "A primitive used in unit tests";

            OSD osd = prim.GetOSD();
            Primitive prim2 = Primitive.FromOSD(osd);

            Assert.That(prim2.ID, Is.EqualTo(prim.ID));
            Assert.That(prim2.LocalID, Is.EqualTo(prim.LocalID));
            Assert.That(prim2.ParentID, Is.EqualTo(prim.ParentID));
            Assert.That((prim2.Flags & PrimFlags.Phantom) != 0, Is.EqualTo(true));
            Assert.That((prim2.Flags & PrimFlags.Physics) != 0, Is.EqualTo(true));
            Assert.That((prim2.Flags & PrimFlags.CastShadows) != 0, Is.EqualTo(true));

            Assert.That(prim2.Position, Is.EqualTo(prim.Position));
            Assert.That(prim2.Rotation, Is.EqualTo(prim.Rotation));
            Assert.That(prim2.Scale, Is.EqualTo(prim.Scale));

            Assert.That(prim2.PrimData.Material, Is.EqualTo(prim.PrimData.Material));
            Assert.That(prim2.PrimData.PCode, Is.EqualTo(prim.PrimData.PCode));
            Assert.That(prim2.PrimData.State, Is.EqualTo(prim.PrimData.State));
            Assert.That(prim2.PrimData.PathCurve, Is.EqualTo(prim.PrimData.PathCurve));
            Assert.That(prim2.PrimData.PathScaleX, Is.EqualTo(prim.PrimData.PathScaleX));
            Assert.That(prim2.PrimData.PathScaleY, Is.EqualTo(prim.PrimData.PathScaleY));
            Assert.That(prim2.PrimData.ProfileBegin, Is.EqualTo(prim.PrimData.ProfileBegin));
            Assert.That(prim2.PrimData.ProfileEnd, Is.EqualTo(prim.PrimData.ProfileEnd));
            Assert.That(prim2.PrimData.ProfileHollow, Is.EqualTo(prim.PrimData.ProfileHollow));
            Assert.That(prim2.PrimData.ProfileCurve, Is.EqualTo(prim.PrimData.ProfileCurve));
            Assert.That(prim2.PrimData.ProfileHole, Is.EqualTo(prim.PrimData.ProfileHole));

            Assert.That(prim2.Properties, Is.Not.Null);
            Assert.That(prim2.Properties.Name, Is.EqualTo(prim.Properties.Name));
            Assert.That(prim2.Properties.Description, Is.EqualTo(prim.Properties.Description));
        }

        [Test]
        public void GetOSD_FromOSD_Roundtrip_Extras()
        {
            Primitive prim = new Primitive();
            prim.ID = UUID.Random();

            // Flexible
            prim.Flexible = new Primitive.FlexibleData();
            prim.Flexible.Softness = 3;
            prim.Flexible.Tension = 1.5f;
            prim.Flexible.Drag = 0.7f;
            prim.Flexible.Gravity = -9.8f;
            prim.Flexible.Wind = 0.2f;
            prim.Flexible.Force = new Vector3(0.1f, 0.2f, 0.3f);

            // Light
            prim.Light = new Primitive.LightData();
            prim.Light.Color = new Color4(0.2f, 0.4f, 0.6f, 1f);
            prim.Light.Intensity = 0.8f;
            prim.Light.Radius = 10f;
            prim.Light.Cutoff = 0.5f;
            prim.Light.Falloff = 2f;

            // Light image
            prim.LightMap = new Primitive.LightImage();
            prim.LightMap.LightTexture = UUID.Random();
            prim.LightMap.Params = new Vector3(1f, 2f, 3f);

            // Sculpt
            prim.Sculpt = new Primitive.SculptData();
            prim.Sculpt.SculptTexture = UUID.Random();
            prim.Sculpt.Type = SculptType.Mesh;

            OSD osd = prim.GetOSD();
            Primitive prim2 = Primitive.FromOSD(osd);

            Assert.That(prim2.Flexible, Is.Not.Null);
            Assert.That(prim2.Flexible.Softness, Is.EqualTo(prim.Flexible.Softness));
            Assert.That(prim2.Flexible.Tension, Is.EqualTo(prim.Flexible.Tension));
            Assert.That(prim2.Flexible.Drag, Is.EqualTo(prim.Flexible.Drag));
            Assert.That(prim2.Flexible.Gravity, Is.EqualTo(prim.Flexible.Gravity));
            Assert.That(prim2.Flexible.Wind, Is.EqualTo(prim.Flexible.Wind));
            Assert.That(prim2.Flexible.Force, Is.EqualTo(prim.Flexible.Force));

            Assert.That(prim2.Light, Is.Not.Null);
            Assert.That(prim2.Light.Color.R, Is.EqualTo(prim.Light.Color.R));
            Assert.That(prim2.Light.Color.G, Is.EqualTo(prim.Light.Color.G));
            Assert.That(prim2.Light.Color.B, Is.EqualTo(prim.Light.Color.B));
            Assert.That(prim2.Light.Intensity, Is.EqualTo(prim.Light.Intensity));
            Assert.That(prim2.Light.Radius, Is.EqualTo(prim.Light.Radius));
            Assert.That(prim2.Light.Cutoff, Is.EqualTo(prim.Light.Cutoff));
            Assert.That(prim2.Light.Falloff, Is.EqualTo(prim.Light.Falloff));

            Assert.That(prim2.LightMap, Is.Not.Null);
            Assert.That(prim2.LightMap.LightTexture, Is.EqualTo(prim.LightMap.LightTexture));
            Assert.That(prim2.LightMap.Params, Is.EqualTo(prim.LightMap.Params));

            Assert.That(prim2.Sculpt, Is.Not.Null);
            Assert.That(prim2.Sculpt.SculptTexture, Is.EqualTo(prim.Sculpt.SculptTexture));
            Assert.That(prim2.Sculpt.Type, Is.EqualTo(prim.Sculpt.Type));
        }

        [Test]
        public void FromOSD_MissingVolume_DoesNotThrow_AndUsesDefaults()
        {
            // Create minimal OSD with identifiers but no volume
            OSDMap map = new OSDMap();
            UUID id = UUID.Random();
            map["id"] = OSD.FromUUID(id);
            map["localid"] = OSD.FromUInteger(99u);
            map["parentid"] = OSD.FromUInteger(100u);

            Primitive prim = Primitive.FromOSD(map);

            Assert.That(prim, Is.Not.Null);
            Assert.That(prim.ID, Is.EqualTo(id));
            Assert.That(prim.LocalID, Is.EqualTo(99u));
            Assert.That(prim.ParentID, Is.EqualTo(100u));

            // Defaults from GetOrDefault: PathCurve defaults to Line and ProfileCurve to Circle
            Assert.That(prim.PrimData.PathCurve, Is.EqualTo(PathCurve.Line));
            Assert.That(prim.PrimData.ProfileCurve, Is.EqualTo(ProfileCurve.Circle));
            Assert.That(prim.PrimData.ProfileEnd, Is.EqualTo(1f));
        }

        [Test]
        public void FromOSD_WrongTypes_ForTransforms_AreIgnored()
        {
            OSDMap map = new OSDMap();
            map["position"] = OSD.FromString("not-a-vector");
            map["rotation"] = OSD.FromInteger(123);
            map["scale"] = OSD.FromString("also-not-a-vector");

            // Should not throw
            Primitive prim = Primitive.FromOSD(map);

            // Transform fields should remain at defaults
            Assert.That(prim.Position, Is.EqualTo(Vector3.Zero));
            Assert.That(prim.Rotation, Is.EqualTo(Quaternion.Identity));
            Assert.That(prim.Scale, Is.EqualTo(Vector3.One));
        }

        [Test]
        public void FromOSD_PartialProfile_UsesDefaultsForMissingFields()
        {
            OSDMap profile = new OSDMap();
            profile["curve"] = OSD.FromInteger((int)ProfileCurve.EqualTriangle);

            OSDMap volume = new OSDMap();
            volume["profile"] = profile;

            OSDMap map = new OSDMap();
            map["volume"] = volume;

            Primitive prim = Primitive.FromOSD(map);

            Assert.That(prim.PrimData.ProfileCurve, Is.EqualTo(ProfileCurve.EqualTriangle));
            // Missing values should fall back to defaults
            Assert.That(prim.PrimData.ProfileBegin, Is.EqualTo(0f));
            Assert.That(prim.PrimData.ProfileEnd, Is.EqualTo(1f));
            Assert.That(prim.PrimData.ProfileHollow, Is.EqualTo(0f));
        }
    }
}
