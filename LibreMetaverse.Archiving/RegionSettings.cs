using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace LibreMetaverse.Assets
{
    public class RegionSettings
    {
        public bool AllowDamage;
        public bool AllowLandResell;
        public bool AllowLandJoinDivide;
        public bool BlockFly;
        public bool BlockLandShowInSearch;
        public bool BlockTerraform;
        public bool DisableCollisions;
        public bool DisablePhysics;
        public bool DisableScripts;
        public int MaturityRating;
        public bool RestrictPushing;
        public int AgentLimit;
        public float ObjectBonus;

        public UUID TerrainDetail0;
        public UUID TerrainDetail1;
        public UUID TerrainDetail2;
        public UUID TerrainDetail3;
        public float TerrainHeightRange00;
        public float TerrainHeightRange01;
        public float TerrainHeightRange10;
        public float TerrainHeightRange11;
        public float TerrainStartHeight00;
        public float TerrainStartHeight01;
        public float TerrainStartHeight10;
        public float TerrainStartHeight11;

        public float WaterHeight;
        public float TerrainRaiseLimit;
        public float TerrainLowerLimit;
        public bool UseEstateSun;
        public bool FixedSun;

        public static RegionSettings FromStream(Stream stream)
        {
            // The names are unique across the sections, so read them all and look each one up. That
            // does not depend on the order, on white space between the elements, or on an element being there.
            var values = new Dictionary<string, string>();

            using (XmlTextReader xtr = new XmlTextReader(ArchiveXml.OpenText(stream)))
            {
                xtr.MoveToContent();
                xtr.ReadStartElement("RegionSettings");

                while (!xtr.EOF && xtr.NodeType != XmlNodeType.EndElement)
                {
                    if (xtr.NodeType != XmlNodeType.Element)
                    {
                        xtr.Read();
                        continue;
                    }

                    // A section such as General, GroundTextures or Terrain
                    if (xtr.IsEmptyElement)
                    {
                        xtr.Read();
                        continue;
                    }

                    xtr.ReadStartElement();
                    while (!xtr.EOF && xtr.NodeType != XmlNodeType.EndElement)
                    {
                        if (xtr.NodeType == XmlNodeType.Element)
                        {
                            string name = xtr.Name;
                            if (xtr.IsEmptyElement)
                            {
                                xtr.Read();
                            }
                            else
                            {
                                try { values[name] = xtr.ReadElementContentAsString(); }
                                catch (XmlException) { xtr.Skip(); }
                            }
                        }
                        else
                        {
                            xtr.Read();
                        }
                    }

                    if (xtr.NodeType == XmlNodeType.EndElement)
                        xtr.Read();
                }
            }

            bool Bool(string name) => values.TryGetValue(name, out var v) && bool.TryParse(v.Trim(), out var b) && b;
            int Int(string name) => values.TryGetValue(name, out var v) && int.TryParse(v.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0;
            float Float(string name) => values.TryGetValue(name, out var v) && float.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
            UUID Id(string name) => values.TryGetValue(name, out var v) && UUID.TryParse(v.Trim(), out var id) ? id : UUID.Zero;

            return new RegionSettings
            {
                AllowDamage = Bool("AllowDamage"),
                AllowLandResell = Bool("AllowLandResell"),
                AllowLandJoinDivide = Bool("AllowLandJoinDivide"),
                BlockFly = Bool("BlockFly"),
                BlockLandShowInSearch = Bool("BlockLandShowInSearch"),
                BlockTerraform = Bool("BlockTerraform"),
                DisableCollisions = Bool("DisableCollisions"),
                DisablePhysics = Bool("DisablePhysics"),
                DisableScripts = Bool("DisableScripts"),
                MaturityRating = Int("MaturityRating"),
                RestrictPushing = Bool("RestrictPushing"),
                AgentLimit = Int("AgentLimit"),
                ObjectBonus = Float("ObjectBonus"),

                TerrainDetail0 = Id("Texture1"),
                TerrainDetail1 = Id("Texture2"),
                TerrainDetail2 = Id("Texture3"),
                TerrainDetail3 = Id("Texture4"),
                TerrainStartHeight00 = Float("ElevationLowSW"),
                TerrainStartHeight01 = Float("ElevationLowNW"),
                TerrainStartHeight10 = Float("ElevationLowSE"),
                TerrainStartHeight11 = Float("ElevationLowNE"),
                TerrainHeightRange00 = Float("ElevationHighSW"),
                TerrainHeightRange01 = Float("ElevationHighNW"),
                TerrainHeightRange10 = Float("ElevationHighSE"),
                TerrainHeightRange11 = Float("ElevationHighNE"),

                WaterHeight = Float("WaterHeight"),
                TerrainRaiseLimit = Float("TerrainRaiseLimit"),
                TerrainLowerLimit = Float("TerrainLowerLimit"),
                UseEstateSun = Bool("UseEstateSun"),
                FixedSun = Bool("FixedSun")
            };
        }

        public void ToXML(string filename)
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter writer = new XmlTextWriter(sw) { Formatting = Formatting.Indented };
            writer.WriteStartDocument();

            writer.WriteStartElement(string.Empty, "RegionSettings", string.Empty);
            writer.WriteStartElement(string.Empty, "General", string.Empty);

            WriteBoolean(writer, "AllowDamage", AllowDamage);
            WriteBoolean(writer, "AllowLandResell", AllowLandResell);
            WriteBoolean(writer, "AllowLandJoinDivide", AllowLandJoinDivide);
            WriteBoolean(writer, "BlockFly", BlockFly);
            WriteBoolean(writer, "BlockLandShowInSearch", BlockLandShowInSearch);
            WriteBoolean(writer, "BlockTerraform", BlockTerraform);
            WriteBoolean(writer, "DisableCollisions", DisableCollisions);
            WriteBoolean(writer, "DisablePhysics", DisablePhysics);
            WriteBoolean(writer, "DisableScripts", DisableScripts);
            writer.WriteElementString("MaturityRating", MaturityRating.ToString());
            WriteBoolean(writer, "RestrictPushing", RestrictPushing);
            writer.WriteElementString("AgentLimit", AgentLimit.ToString());
            writer.WriteElementString("ObjectBonus", ObjectBonus.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();

            writer.WriteStartElement(string.Empty, "GroundTextures", string.Empty);
                
            writer.WriteElementString("Texture1", TerrainDetail0.ToString());
            writer.WriteElementString("Texture2", TerrainDetail1.ToString());
            writer.WriteElementString("Texture3", TerrainDetail2.ToString());
            writer.WriteElementString("Texture4", TerrainDetail3.ToString());
            writer.WriteElementString("ElevationLowSW", TerrainStartHeight00.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("ElevationLowNW", TerrainStartHeight01.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("ElevationLowSE", TerrainStartHeight10.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("ElevationLowNE", TerrainStartHeight11.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("ElevationHighSW", TerrainHeightRange00.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("ElevationHighNW", TerrainHeightRange01.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("ElevationHighSE", TerrainHeightRange10.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("ElevationHighNE", TerrainHeightRange11.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
                
            writer.WriteStartElement(string.Empty, "Terrain", string.Empty);

            writer.WriteElementString("WaterHeight", WaterHeight.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("TerrainRaiseLimit", TerrainRaiseLimit.ToString(CultureInfo.InvariantCulture));
            writer.WriteElementString("TerrainLowerLimit", TerrainLowerLimit.ToString(CultureInfo.InvariantCulture));
            WriteBoolean(writer, "UseEstateSun", UseEstateSun);
            WriteBoolean(writer, "FixedSun", FixedSun);

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.Close();
            sw.Close();
            File.WriteAllText(filename, sw.ToString());
        }

        private void WriteBoolean(XmlTextWriter writer, string name, bool value)
        {
            writer.WriteElementString(name, value ? "True" : "False");
        }
    }
}
