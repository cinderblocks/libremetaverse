using System;
using System.Linq;
using System.Text;

namespace OpenMetaverse.TestClient
{
    public class ExportParticlesCommand : Command
    {
        public ExportParticlesCommand(TestClient testClient)
        {
            Name = "exportparticles";
            Description = "Reverse engineers a prim with a particle system to an LSL script. Usage: exportscript [prim-uuid]";
            Category = CommandCategory.Objects;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length != 1)
            {
                return "Usage: exportparticles [prim-uuid]";
            }

            if (!UUID.TryParse(args[0], out var id))
            {
                return "Usage: exportparticles [prim-uuid]";
            }

            lock (Client.Network.Simulators)
            {
                foreach (var exportPrim in from sim in Client.Network.Simulators 
                         select sim.ObjectsPrimitives.FirstOrDefault(
                             prim => prim.Value.ID == id) 
                         into kvp where kvp.Value != null select kvp.Value)
                {
                    if (exportPrim.ParticleSys.CRC == 0)
                    {
                        return $"Prim {exportPrim.LocalID} does not have a particle system";
                    }

                    StringBuilder lsl = new StringBuilder();

                    #region Particle System to LSL

                    lsl.Append("default" + Environment.NewLine);
                    lsl.Append("{" + Environment.NewLine);
                    lsl.Append("    state_entry()" + Environment.NewLine);
                    lsl.Append("    {" + Environment.NewLine);
                    lsl.Append("         llParticleSystem([" + Environment.NewLine);

                    lsl.Append("         PSYS_PART_FLAGS, 0");

                    if ((exportPrim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.InterpColor) != 0)
                        lsl.Append(" | PSYS_PART_INTERP_COLOR_MASK");
                    if ((exportPrim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.InterpScale) != 0)
                        lsl.Append(" | PSYS_PART_INTERP_SCALE_MASK");
                    if ((exportPrim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.Bounce) != 0)
                        lsl.Append(" | PSYS_PART_BOUNCE_MASK");
                    if ((exportPrim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.Wind) != 0)
                        lsl.Append(" | PSYS_PART_WIND_MASK");
                    if ((exportPrim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.FollowSrc) != 0)
                        lsl.Append(" | PSYS_PART_FOLLOW_SRC_MASK");
                    if ((exportPrim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.FollowVelocity) != 0)
                        lsl.Append(" | PSYS_PART_FOLLOW_VELOCITY_MASK");
                    if ((exportPrim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.TargetPos) != 0)
                        lsl.Append(" | PSYS_PART_TARGET_POS_MASK");
                    if ((exportPrim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.TargetLinear) != 0)
                        lsl.Append(" | PSYS_PART_TARGET_LINEAR_MASK");
                    if ((exportPrim.ParticleSys.PartDataFlags & Primitive.ParticleSystem.ParticleDataFlags.Emissive) != 0)
                        lsl.Append(" | PSYS_PART_EMISSIVE_MASK");

                    lsl.Append(","); lsl.Append(Environment.NewLine);
                    lsl.Append("         PSYS_SRC_PATTERN, 0");

                    if ((exportPrim.ParticleSys.Pattern & Primitive.ParticleSystem.SourcePattern.Drop) != 0)
                        lsl.Append(" | PSYS_SRC_PATTERN_DROP");
                    if ((exportPrim.ParticleSys.Pattern & Primitive.ParticleSystem.SourcePattern.Explode) != 0)
                        lsl.Append(" | PSYS_SRC_PATTERN_EXPLODE");
                    if ((exportPrim.ParticleSys.Pattern & Primitive.ParticleSystem.SourcePattern.Angle) != 0)
                        lsl.Append(" | PSYS_SRC_PATTERN_ANGLE");
                    if ((exportPrim.ParticleSys.Pattern & Primitive.ParticleSystem.SourcePattern.AngleCone) != 0)
                        lsl.Append(" | PSYS_SRC_PATTERN_ANGLE_CONE");
                    if ((exportPrim.ParticleSys.Pattern & Primitive.ParticleSystem.SourcePattern.AngleConeEmpty) != 0)
                        lsl.Append(" | PSYS_SRC_PATTERN_ANGLE_CONE_EMPTY");

                    lsl.Append("," + Environment.NewLine);

                    lsl.Append("         PSYS_PART_START_ALPHA, " +
                               $"{exportPrim.ParticleSys.PartStartColor.A:0.00000}" + "," + Environment.NewLine);
                    lsl.Append("         PSYS_PART_END_ALPHA, " +
                               $"{exportPrim.ParticleSys.PartEndColor.A:0.00000}" + "," + Environment.NewLine);
                    lsl.Append("         PSYS_PART_START_COLOR, " + exportPrim.ParticleSys.PartStartColor.ToRGBString() + "," + Environment.NewLine);
                    lsl.Append("         PSYS_PART_END_COLOR, " + exportPrim.ParticleSys.PartEndColor.ToRGBString() + "," + Environment.NewLine);
                    lsl.Append("         PSYS_PART_START_SCALE, <" +
                               $"{exportPrim.ParticleSys.PartStartScaleX:0.00000}" + ", " +
                               $"{exportPrim.ParticleSys.PartStartScaleY:0.00000}" + ", 0>, " + Environment.NewLine);
                    lsl.Append("         PSYS_PART_END_SCALE, <" +
                               $"{exportPrim.ParticleSys.PartEndScaleX:0.00000}" + ", " +
                               $"{exportPrim.ParticleSys.PartEndScaleY:0.00000}" + ", 0>, " + Environment.NewLine);
                    lsl.Append("         PSYS_PART_MAX_AGE, " + $"{exportPrim.ParticleSys.PartMaxAge:0.00000}" + "," + Environment.NewLine);
                    lsl.Append("         PSYS_SRC_MAX_AGE, " + $"{exportPrim.ParticleSys.MaxAge:0.00000}" + "," + Environment.NewLine);
                    lsl.Append("         PSYS_SRC_ACCEL, " + exportPrim.ParticleSys.PartAcceleration + "," + Environment.NewLine);
                    lsl.Append("         PSYS_SRC_BURST_PART_COUNT, " +
                               $"{exportPrim.ParticleSys.BurstPartCount:0}" + "," + Environment.NewLine);
                    lsl.Append("         PSYS_SRC_BURST_RADIUS, " +
                               $"{exportPrim.ParticleSys.BurstRadius:0.00000}" + "," + Environment.NewLine);
                    lsl.Append("         PSYS_SRC_BURST_RATE, " +
                               $"{exportPrim.ParticleSys.BurstRate:0.00000}" + "," + Environment.NewLine);
                    lsl.Append("         PSYS_SRC_BURST_SPEED_MIN, " +
                               $"{exportPrim.ParticleSys.BurstSpeedMin:0.00000}" + "," + Environment.NewLine);
                    lsl.Append("         PSYS_SRC_BURST_SPEED_MAX, " +
                               $"{exportPrim.ParticleSys.BurstSpeedMax:0.00000}" + "," + Environment.NewLine);
                    lsl.Append("         PSYS_SRC_INNERANGLE, " +
                               $"{exportPrim.ParticleSys.InnerAngle:0.00000}" + "," + Environment.NewLine);
                    lsl.Append("         PSYS_SRC_OUTERANGLE, " +
                               $"{exportPrim.ParticleSys.OuterAngle:0.00000}" + "," + Environment.NewLine);
                    lsl.Append("         PSYS_SRC_OMEGA, " + exportPrim.ParticleSys.AngularVelocity + "," + Environment.NewLine);
                    lsl.Append("         PSYS_SRC_TEXTURE, (key)\"" + exportPrim.ParticleSys.Texture + "\"," + Environment.NewLine);
                    lsl.Append("         PSYS_SRC_TARGET_KEY, (key)\"" + exportPrim.ParticleSys.Target + "\"" + Environment.NewLine);

                    lsl.Append("         ]);" + Environment.NewLine);
                    lsl.Append("    }" + Environment.NewLine);
                    lsl.Append("}" + Environment.NewLine);

                    #endregion Particle System to LSL

                    return lsl.ToString();

                }
            }

            return $"Could not find {id} object";
        }
    }
}
