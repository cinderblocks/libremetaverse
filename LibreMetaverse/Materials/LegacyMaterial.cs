using System;
using System.IO;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace LibreMetaverse.Materials
{
    public class LegacyMaterial
    {
        public UUID ID { get; set; }
        
        public UUID NormalMap { get; set; }
        public double NormalMapOffsetX { get; set; }
        public double NormalMapOffsetY { get; set; }
        public double NormalMapRepeatX { get; set; }
        public double NormalMapRepeatY { get; set; }
        public double NormalMapRotation { get; set; }

        public UUID SpecularMap { get; set; }
        public double SpecularMapOffsetX { get; set; }
        public double SpecularMapOffsetY { get; set; }
        public double SpecularMapRepeatX { get; set; }
        public double SpecularMapRepeatY { get; set; }
        public double SpecularMapRotation { get; set; }

        public Color4 SpecularColor { get; set; }
        public byte SpecularExponent { get; set; }
        public byte EnvironmentIntensity { get; set; }
        public byte AlphaMaskCutoff { get; set; }

        public LegacyMaterialAlphaMode DiffuseAlphaMode { get; set; }

        public LegacyMaterial()
        {

        }

        private const double MaterialsMultiplier = 10000.0;
        
        public LegacyMaterial(OSDMap mapOrig)
        {
            if (!(mapOrig.ContainsKey("ID") && mapOrig.ContainsKey("Material")))
            {
                throw new InvalidOperationException("Legacy material needs to contain 'ID' and 'Material' keys.");
            }

            if (mapOrig["ID"] is OSDBinary idBinary)
            {
                ID = new UUID(idBinary.AsBinary(), 0);
            } 
            else if (mapOrig["ID"] is OSDArray idArray)
            {
                ID = new UUID(idArray.AsBinary(), 0);
            } 
            else if (mapOrig["ID"] is OSDUUID idUUID)
            {
                ID = idUUID.AsUUID();
            }
            else
            {
                throw new InvalidOperationException("LegacyMaterial ID is of an unknown type " + mapOrig["ID"].Type);
            }
            
            if (mapOrig["Material"] is OSDMap map)
            {
                NormalMap = map["NormMap"].AsUUID();
                NormalMapOffsetX = map["NormOffsetX"].AsInteger() / MaterialsMultiplier;
                NormalMapOffsetY = map["NormOffsetY"].AsInteger() / MaterialsMultiplier;
                NormalMapRepeatX = map["NormRepeatX"].AsInteger() / MaterialsMultiplier;
                NormalMapRepeatY = map["NormRepeatX"].AsInteger() / MaterialsMultiplier;
                NormalMapRotation = map["NormRotation"].AsInteger() / MaterialsMultiplier;

                SpecularMap = map["NormMap"].AsUUID();
                SpecularMapOffsetX = map["NormOffsetX"].AsInteger() / MaterialsMultiplier;
                SpecularMapOffsetY = map["NormOffsetY"].AsInteger() / MaterialsMultiplier;
                SpecularMapRepeatX = map["NormRepeatX"].AsInteger() / MaterialsMultiplier;
                SpecularMapRepeatY = map["NormRepeatX"].AsInteger() / MaterialsMultiplier;
                SpecularMapRotation = map["NormRotation"].AsInteger() / MaterialsMultiplier;

                SpecularColor = map["SpecColor"].AsColor4();
                SpecularExponent = (byte)map["SpecExp"].AsInteger();
                EnvironmentIntensity = (byte)map["EnvIntensity"].AsInteger();
                AlphaMaskCutoff = (byte)map["AlphaMaskCutoff"].AsInteger();
                DiffuseAlphaMode = (LegacyMaterialAlphaMode)map["DiffuseAlphaMode"].AsInteger();
            }
        }
    }
}
