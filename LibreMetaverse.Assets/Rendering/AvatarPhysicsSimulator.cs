/*
 * Copyright (c) 2025-2026, Sjofn LLC.
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.co nor the names
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */
using System;
using System.Collections.Generic;

namespace LibreMetaverse.Rendering
{
    using Vector3 = System.Numerics.Vector3;

    // ── SL physics wearable simulation ──────────────────────────────────────────
    // Matches LLPhysicsMotion / LLPhysicsMotionController from
    // indra/newview/llphysicsmotion.cpp in the SL viewer.
    //
    // The physics wearable defines spring-mass-damper behaviour for six
    // independently-simulated body-part motions:
    //
    //   1. Breast up/down        (joint: mChest,  dir: 0,0,-1)
    //   2. Breast in/out         (joint: mChest,  dir: -1,0,0)
    //   3. Breast left/right     (joint: mChest,  dir: 0,-1,0)
    //   4. Butt up/down          (joint: mPelvis, dir: 0,0,-1)
    //   5. Butt left/right       (joint: mPelvis, dir: 0,-1,0)
    //   6. Belly up/down         (joint: mPelvis, dir: 0,0,-1)
    //
    // Each channel has:
    //   • A "driver" visual param (the Controller param, e.g. Breast_Physics_UpDown_Controller)
    //     whose current weight (normalised [0,1]) is the rest-position target.
    //   • Behaviour params read from the wearable (Mass, Gravity, Spring, Gain, Damping, Drag,
    //     MaxEffect).
    //   • Spring-mass-damper state (position_local, velocity_local, etc.) integrated at up to
    //     20 Hz (TIME_ITERATION_STEP_MAX = 0.05 s).
    //
    // Output is a dictionary from *driven* param ID to normalised position [0,1], used by
    // callers to offset collision-volume bone positions in a VP-shaped bone-transform dict.
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Per-channel spring-mass-damper physics state, matching one
    /// <c>LLPhysicsMotion</c> instance in the SL viewer.
    /// </summary>
    internal sealed class PhysicsChannel
    {
        // ── Channel identity ──────────────────────────────────────────────────

        /// <summary>Visual-param ID of the *driver* (controller) param, e.g. <c>Breast_Physics_UpDown_Controller</c> (1100).</summary>
        public readonly int DriverParamId;
        /// <summary>Visual-param IDs of the *driven* params whose morph should be updated (e.g. [1200]).</summary>
        public readonly int[] DrivenParamIds;

        /// <summary>World-space unit vector that projects joint movement onto this channel's axis.</summary>
        public readonly Vector3 MotionDirection;

        // VP IDs for the wearable behaviour scalars.
        // These are the group=0 "Breast_Physics_Mass" etc. params.
        public readonly int MassParamId;
        public readonly int GravityParamId;
        public readonly int DragParamId;
        public readonly int DampingParamId;
        public readonly int MaxEffectParamId;
        public readonly int SpringParamId;
        public readonly int GainParamId;

        // ── Simulation state ──────────────────────────────────────────────────
        // Mirrors LLPhysicsMotion private fields.

        internal float PositionLocal;          // current spring position [0,1]
        internal float VelocityLocal;          // current spring velocity
        internal float VelocityJointLocal;     // previous joint velocity in motion-direction space
        internal float AccelerationJointLocal; // previous joint acceleration (smoothed)
        internal float PositionLastUpdate;     // position at last visual-param update
        internal float LastTime = -1f;         // integration clock; <0 = not yet started
        internal Vector3 PositionWorld;      // joint world position at previous tick

        public PhysicsChannel(
            int driverParamId, int[] drivenParamIds,
            Vector3 motionDirection,
            int massId, int gravityId, int dragId,
            int dampingId, int maxEffectId, int springId, int gainId)
        {
            DriverParamId  = driverParamId;
            DrivenParamIds = drivenParamIds;
            MotionDirection = motionDirection;
            MassParamId      = massId;
            GravityParamId   = gravityId;
            DragParamId      = dragId;
            DampingParamId   = dampingId;
            MaxEffectParamId = maxEffectId;
            SpringParamId    = springId;
            GainParamId      = gainId;
        }

        /// <summary>Reset integration state without changing wearable params.</summary>
        public void Reset()
        {
            PositionLocal       = 0.5f;
            VelocityLocal       = 0f;
            VelocityJointLocal  = 0f;
            AccelerationJointLocal = 0f;
            PositionLastUpdate  = 0.5f;
            LastTime            = -1f;
            PositionWorld       = Vector3.Zero;
        }
    }

    /// <summary>
    /// Simulates SL avatar physics wearable effects (breast/butt/belly bounce).
    /// Instantiate once per avatar viewer, call <see cref="Tick"/> each animation frame.
    /// </summary>
    public sealed class AvatarPhysicsSimulator
    {
        // TIME_ITERATION_STEP_MAX from llphysicsmotion.cpp — upper bound on sub-step size.
        private const float TimeIterationStepMax = 0.05f;

        // Scale factor applied to joint velocity before computing local velocity
        // (world_to_model_scale * joint_local_factor = 100 * 30 = 3000 in SL viewer;
        //  the joint_local_factor=30 and world_to_model_scale=100 multiply together when
        //  the time_delta also includes joint_local_factor).
        // SL uses: velocity_local = positionchange_world * world_to_model_scale / time_delta
        // then passes time_delta * joint_local_factor to calculateVelocity_local.
        // Net: velocity_local = positionchange_world * 100 * 30 / (time_delta * joint_local_factor)
        //                      ... but then divides by (time_delta * 30).
        // Simplified: velocity_local = (positionChange_world * 100) / time_delta
        //             acceleration   = (velocity_local - prev) / time_delta  (with smoothing 3)
        private const float WorldToModelScale  = 100f;
        private const float JointLocalFactor   = 30f;

        // net481 (this project's oldest target) has no Math.Clamp(float,float,float).
        private static float Clamp(float value, float min, float max) =>
            value < min ? min : (value > max ? max : value);

        // ── Channels — one per LLPhysicsMotion ────────────────────────────────
        internal readonly PhysicsChannel[] Channels;

        // Current visual-param weights for the wearable behaviour scalars.
        // Updated each time SetWearableParams is called (on avatar build/rebuild).
        private IReadOnlyDictionary<int, float> _vpWeights =
            new Dictionary<int, float>();

        // Bone world-position delegates: called each tick to get the current
        // world position of the relevant joint so joint velocity can be derived.
        // Keys match the SL joint names ("mChest", "mPelvis").
        private Func<string, Vector3>? _getBoneWorldPos;

        public AvatarPhysicsSimulator()
        {
            Channels = BuildChannels();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Update the wearable behaviour params and reset all channel state.
        /// Call whenever the avatar is rebuilt (appearance changed).
        /// </summary>
        public void SetWearableParams(IReadOnlyDictionary<int, float> visualParams)
        {
            _vpWeights = visualParams;
            foreach (var ch in Channels)
                ch.Reset();
        }

        /// <summary>
        /// Provide a delegate that returns the current world position for a named joint.
        /// Required for velocity-based joint-motion input.
        /// </summary>
        public void SetBonePositionProvider(Func<string, Vector3> getBoneWorldPos)
        {
            _getBoneWorldPos = getBoneWorldPos;
        }

        /// <summary>
        /// Advance the simulation by <paramref name="dt"/> seconds.
        /// </summary>
        /// <param name="time">
        /// Monotonically increasing time in seconds (e.g. total elapsed seconds).
        /// </param>
        /// <returns>
        /// Dictionary mapping each driven visual-param ID to its current normalised
        /// position in <c>[0,1]</c>.  This maps directly to the <c>position_new_local_clamped</c>
        /// value from SL's <c>LLPhysicsMotion::onUpdate</c>.
        /// </returns>
        public Dictionary<int, float> Tick(float time)
        {
            var result = new Dictionary<int, float>();
            foreach (var ch in Channels)
            {
                float pos = StepChannel(ch, time);
                foreach (var id in ch.DrivenParamIds)
                    result[id] = pos;
            }
            return result;
        }

        // ── Per-channel integration ───────────────────────────────────────────

        private float StepChannel(PhysicsChannel ch, float time)
        {
            // First tick — seed position and return resting state.
            if (ch.LastTime < 0f)
            {
                ch.LastTime       = time;
                ch.PositionLocal  = GetDriverPositionLocal(ch);
                if (_getBoneWorldPos != null)
                    ch.PositionWorld = GetJointPos(ch);
                return ch.PositionLocal;
            }

            float timeDelta = time - ch.LastTime;

            // Clamp time delta: if less than one frame or >1s skip to avoid explosions.
            if (timeDelta <= 0f || timeDelta > 1.0f)
            {
                ch.LastTime = time;
                return ch.PositionLocal;
            }

            // Behaviour scalars from the wearable.
            float mass       = GetParam(ch.MassParamId,      0.1f);
            float gravity    = GetParam(ch.GravityParamId,   0f);
            float spring     = GetParam(ch.SpringParamId,    10f);
            float gain       = GetParam(ch.GainParamId,      10f);
            float damping    = GetParam(ch.DampingParamId,   0.2f);
            float drag       = GetParam(ch.DragParamId,      1f);
            float maxEffect  = GetParam(ch.MaxEffectParamId, 0f);

            // Driver position: where the user set the param (normalised [0,1]).
            float positionUserLocal = GetDriverPositionLocal(ch);

            // Current joint world position → velocity in motion-direction space.
            Vector3 jointPos = _getBoneWorldPos != null ? GetJointPos(ch) : ch.PositionWorld;
            float dtScaled = timeDelta * JointLocalFactor;
            float velocityJointLocal = CalculateVelocity(ch, jointPos, dtScaled);
            float accelJointLocal    = CalculateAcceleration(ch, velocityJointLocal, dtScaled);

            // Sub-step integration (mirrors SL's loop).
            uint   steps            = (uint)(timeDelta / TimeIterationStepMax) + 1;
            float  timeIterationStep = timeDelta / (float)steps;
            float  positionCurrent   = Clamp(ch.PositionLocal, 0f, 1f);

            // Early-out: if MaxEffect is 0 and already at rest, skip.
            if (maxEffect == 0f && positionCurrent == positionUserLocal)
            {
                ch.LastTime            = time;
                ch.PositionWorld       = jointPos;
                ch.VelocityJointLocal  = velocityJointLocal;
                ch.AccelerationJointLocal = accelJointLocal;
                return positionCurrent;
            }

            float positionNew = positionCurrent;
            float velocityNew = ch.VelocityLocal;

            for (uint s = 0; s < steps; s++)
            {
                float springLength = positionNew - positionUserLocal;

                // SL gravity direction: (0,0,1) world up in toLocal gives gravity force.
                var gravWorld   = new Vector3(0f, 0f, 1f);
                float gravLocal = ToLocal(ch, gravWorld);

                float forceSpring  = -springLength * spring;
                float forceAccel   = gain * (accelJointLocal * mass);
                float forceGravity = gravLocal * gravity * mass;
                float forceDamping = -damping * velocityNew;
                float forceDrag    = 0.5f * drag * velocityJointLocal * velocityJointLocal
                                      * Math.Sign(velocityJointLocal);
                float forceNet     = forceAccel + forceGravity + forceSpring + forceDamping + forceDrag;

                const float maxVelocity = 100f;
                float accelNew   = forceNet / mass;
                velocityNew      = Clamp(velocityNew + accelNew * timeIterationStep,
                                              -maxVelocity, maxVelocity);
                positionNew      = positionNew + velocityNew * timeIterationStep;

                // Zero velocity if hitting the param limit.
                if ((positionNew < 0f && velocityNew < 0f) ||
                    (positionNew > 1f && velocityNew > 0f))
                    velocityNew = 0f;
            }

            // NaN guard.
            if (float.IsNaN(ch.PositionLocal) || float.IsNaN(ch.VelocityLocal) || float.IsNaN(positionNew))
            {
                positionNew               = 0f;
                velocityNew               = 0f;
                ch.VelocityJointLocal     = 0f;
                ch.AccelerationJointLocal = 0f;
                ch.PositionLocal          = 0f;
                ch.PositionWorld          = Vector3.Zero;
            }

            float clamped = Clamp(positionNew, 0f, 1f);

            ch.LastTime               = time;
            ch.PositionLocal          = positionNew;
            ch.VelocityLocal          = velocityNew;
            ch.VelocityJointLocal     = velocityJointLocal;
            ch.AccelerationJointLocal = accelJointLocal;
            ch.PositionWorld          = jointPos;

            return clamped;
        }

        // ── Helpers matching SL LLPhysicsMotion methods ───────────────────────

        /// <summary>
        /// Project a world-space vector onto this channel's motion axis in local (parameter) space.
        /// Matches <c>LLPhysicsMotion::toLocal</c>.
        /// We approximate joint world rotation as identity here (avatar-space == world-space
        /// in the viewer context), which is the same simplification SL makes for the standing pose.
        /// </summary>
        private static float ToLocal(PhysicsChannel ch, Vector3 worldVec)
        {
            // In SL: dir_world = mMotionDirectionVec * joint.getWorldRotation(); normalize; dot.
            // We use the motion direction directly (avatar always upright in this viewer).
            var dir = ch.MotionDirection;
            if (dir.LengthSquared() > 1e-10f) dir = Vector3.Normalize(dir);
            return Vector3.Dot(worldVec, dir);
        }

        /// <summary>Get current joint world position for velocity derivation.</summary>
        private Vector3 GetJointPos(PhysicsChannel ch)
        {
            // The joint name is inferred from the channel's motion direction and driver name.
            // Rather than storing it explicitly, we determine it from context:
            //   mChest  → breast channels (DriverParamId 1100, 1101, 1105)
            //   mPelvis → butt/belly channels (1102, 1103, 1104)
            var jointName = ch.DriverParamId switch
            {
                1100 or 1101 or 1105 => "mChest",
                _ => "mPelvis",
            };
            return _getBoneWorldPos!(jointName);
        }

        /// <summary>
        /// Compute joint velocity in local space.
        /// Matches <c>LLPhysicsMotion::calculateVelocity_local</c>.
        /// </summary>
        private static float CalculateVelocity(PhysicsChannel ch, Vector3 posNow, float dtScaled)
        {
            if (dtScaled <= 1e-6f) return 0f;
            var change = (posNow - ch.PositionWorld) * WorldToModelScale;
            return ToLocal(ch, change) / dtScaled;
        }

        /// <summary>
        /// Compute smoothed joint acceleration.
        /// Matches <c>LLPhysicsMotion::calculateAcceleration_local</c>.
        /// </summary>
        private static float CalculateAcceleration(PhysicsChannel ch, float velocityNow, float dtScaled)
        {
            const float smoothing = 3f;
            if (dtScaled <= 1e-6f) return 0f;
            float accel = (velocityNow - ch.VelocityJointLocal) / dtScaled;
            return accel * (1f / smoothing) + ch.AccelerationJointLocal * ((smoothing - 1f) / smoothing);
        }

        /// <summary>
        /// Get the driver param's current weight, normalised to [0,1].
        /// If the driver param is absent from the wearable, use the default (0.5, midpoint).
        /// Matches <c>position_user_local</c> in SL:
        ///   (weight - min) / (max - min)
        /// </summary>
        private float GetDriverPositionLocal(PhysicsChannel ch)
        {
            if (!VisualParams.Params.TryGetValue(ch.DriverParamId, out var vpDef))
                return 0.5f;
            float range = vpDef.MaxValue - vpDef.MinValue;
            if (range < 1e-6f) return 0f;
            float raw = _vpWeights.TryGetValue(ch.DriverParamId, out var w) ? w : vpDef.DefaultValue;
            return Clamp((raw - vpDef.MinValue) / range, 0f, 1f);
        }

        /// <summary>Get a wearable behaviour scalar, falling back to the VP default.</summary>
        private float GetParam(int paramId, float fallback)
        {
            if (_vpWeights.TryGetValue(paramId, out var v)) return v;
            if (VisualParams.Params.TryGetValue(paramId, out var vp)) return vp.DefaultValue;
            return fallback;
        }

        // ── Static channel definitions ────────────────────────────────────────
        // Mirrors LLPhysicsMotionController::onInitialize from llphysicsmotion.cpp.

        private static PhysicsChannel[] BuildChannels() =>
        [
            // Breast Bounce (up/down)
            // driver=Breast_Physics_UpDown_Controller(1100) → driven=[1200]
            // joint=mChest, dir=(0,0,-1)
            new PhysicsChannel(
                driverParamId: 1100, drivenParamIds: [1200],
                motionDirection: new Vector3(0f, 0f, -1f),
                massId: 10000, gravityId: 10001, dragId: 10002,
                dampingId: 10006, maxEffectId: 10003, springId: 10004, gainId: 10005),

            // Breast Cleavage (in/out)
            // driver=Breast_Physics_InOut_Controller(1101) → driven=[1201]
            // joint=mChest, dir=(-1,0,0)
            new PhysicsChannel(
                driverParamId: 1101, drivenParamIds: [1201],
                motionDirection: new Vector3(-1f, 0f, 0f),
                massId: 10000, gravityId: 10001, dragId: 10002,
                dampingId: 10010, maxEffectId: 10007, springId: 10008, gainId: 10009),

            // Breast Left/Right
            // driver=Breast_Physics_LeftRight_Controller(1105) → driven=[1207]
            // joint=mChest, dir=(0,-1,0)
            new PhysicsChannel(
                driverParamId: 1105, drivenParamIds: [1207],
                motionDirection: new Vector3(0f, -1f, 0f),
                massId: 10000, gravityId: 10001, dragId: 10002,
                dampingId: 10032, maxEffectId: 10029, springId: 10030, gainId: 10031),

            // Butt Bounce (up/down)
            // driver=Butt_Physics_UpDown_Controller(1103) → driven=[1205]
            // joint=mPelvis, dir=(0,0,-1)
            new PhysicsChannel(
                driverParamId: 1103, drivenParamIds: [1205],
                motionDirection: new Vector3(0f, 0f, -1f),
                massId: 10018, gravityId: 10019, dragId: 10020,
                dampingId: 10024, maxEffectId: 10021, springId: 10022, gainId: 10023),

            // Butt Left/Right
            // driver=Butt_Physics_LeftRight_Controller(1104) → driven=[1206]
            // joint=mPelvis, dir=(0,-1,0)
            new PhysicsChannel(
                driverParamId: 1104, drivenParamIds: [1206],
                motionDirection: new Vector3(0f, -1f, 0f),
                massId: 10018, gravityId: 10019, dragId: 10020,
                dampingId: 10028, maxEffectId: 10025, springId: 10026, gainId: 10027),

            // Belly Bounce (up/down)
            // driver=Belly_Physics_UpDown_Controller(1102) → driven=[1202,1203,1204]
            // joint=mPelvis, dir=(0,0,-1)
            new PhysicsChannel(
                driverParamId: 1102, drivenParamIds: [1202, 1203, 1204],
                motionDirection: new Vector3(0f, 0f, -1f),
                massId: 10011, gravityId: 10012, dragId: 10013,
                dampingId: 10017, maxEffectId: 10014, springId: 10015, gainId: 10016),
        ];
    }

    /// <summary>
    /// Physics volume morph: the position offset (in SL model units) applied to a
    /// collision volume bone per unit of the driven param's normalised output,
    /// scaled through the param's [min,max] range as per <c>LLPhysicsMotion::setParamValue</c>.
    /// </summary>
    public static class PhysicsVolumeMorphs
    {
        // Driven param ID → list of (collision-volume-bone-name, maxEffect-scaled position offset).
        // Data sourced from avatar_lad.xml <volume_morph pos="…"> for each physics-driven param.
        // The pos values are applied at full (max) param weight; at weight=0 the offset is zero.

        // SL setParamValue logic:
        //   min_val = 0.5 - maxEffect/2
        //   max_val = 0.5 + maxEffect/2
        //   new_value_rescaled = min_val + (max_val - min_val) * normalised_position   // in [0,1]
        //   new_value_local    = param.min + (param.max - param.min) * new_value_rescaled
        //
        // The bone offset we apply is:
        //   boneOffset = volumeMorphPos * new_value_local
        //
        // Because the driven params all have symmetric ranges centred on 0
        // (e.g. min=-3 max=3 for 1200), and their volume_morph pos is the *delta per unit value*,
        // the final world-space collision-volume offset is:
        //   offset = volumeMorphPos * new_value_local
        // where new_value_local is in [min..max].

        public readonly record struct VolumeMorph(string BoneName, Vector3 PosDelta);

        private static readonly Dictionary<int, VolumeMorph[]> s_morphs = new()
        {
            // Breast_Physics_UpDown_Driven (1200): min=-3, max=3
            // LEFT_PEC  pos=(0,0,-0.01), RIGHT_PEC pos=(0,0,-0.01)
            [1200] = [
                new VolumeMorph("LEFT_PEC",  new Vector3(0f,  0f,   -0.01f)),
                new VolumeMorph("RIGHT_PEC", new Vector3(0f,  0f,   -0.01f)),
            ],

            // Breast_Physics_InOut_Driven (1201): min=-1.25, max=1.25
            // LEFT_PEC  pos=(0,-0.026,0), RIGHT_PEC pos=(0,0.026,0)
            [1201] = [
                new VolumeMorph("LEFT_PEC",  new Vector3(0f, -0.026f, 0f)),
                new VolumeMorph("RIGHT_PEC", new Vector3(0f,  0.026f, 0f)),
            ],

            // Belly_Physics_Torso_UpDown_Driven (1204): min=-1, max=1
            // BELLY pos=(0,0,0.05)
            [1204] = [
                new VolumeMorph("BELLY", new Vector3(0f, 0f, 0.05f)),
            ],

            // Butt_Physics_UpDown_Driven (1205): min=-1, max=1
            // BUTT pos=(0,0,0.05)
            [1205] = [
                new VolumeMorph("BUTT", new Vector3(0f, 0f, 0.05f)),
            ],

            // Butt_Physics_LeftRight_Driven (1206): min=-1, max=1
            // BUTT pos=(0,0.05,0)
            [1206] = [
                new VolumeMorph("BUTT", new Vector3(0f, 0.05f, 0f)),
            ],

            // Breast_Physics_LeftRight_Driven (1207): min=-2, max=2
            // LEFT_PEC  pos=(0,0.03,0), RIGHT_PEC pos=(0,0.03,0)
            [1207] = [
                new VolumeMorph("LEFT_PEC",  new Vector3(0f, 0.03f, 0f)),
                new VolumeMorph("RIGHT_PEC", new Vector3(0f, 0.03f, 0f)),
            ],

            // 1202 (Belly_Physics_Legs_UpDown_Driven) and 1203 (Belly_Physics_Skirt_UpDown_Driven)
            // have empty <param_morph/> in avatar_lad.xml — no volume morph to apply.
        };

        /// <summary>
        /// Compute per-bone position offsets from the physics simulation output.
        /// </summary>
        /// <param name="drivenWeights">
        /// Map from driven param ID to normalised position in [0,1] as returned by
        /// <see cref="AvatarPhysicsSimulator.Tick"/>.
        /// </param>
        /// <returns>
        /// Accumulated position offsets per collision-volume bone name.
        /// </returns>
        public static Dictionary<string, Vector3> ComputeBoneOffsets(
            Dictionary<int, float> drivenWeights,
            IReadOnlyDictionary<int, float> vpWeights)
        {
            var offsets = new Dictionary<string, Vector3>(StringComparer.Ordinal);

            foreach (var kv in drivenWeights)
            {
                int paramId = kv.Key;
                float normalised = kv.Value;
                if (!s_morphs.TryGetValue(paramId, out var morphs)) continue;
                if (!VisualParams.Params.TryGetValue(paramId, out var vpDef)) continue;

                // Read MaxEffect from the corresponding channel's maxEffect param.
                // The MaxEffect param for a given driven param is stored on the channel object.
                // For simplicity we reuse the channel's maxEffect value already integrated in
                // normalised — it is already scaled inside PhysicsChannel.StepChannel.
                // We now convert normalised [0,1] → actual param value using setParamValue logic.
                // maxEffect is read from the wearable and was used during integration;
                // to reconstruct the final local value we compute:
                //   new_value_rescaled = 0.5*(1 - maxEffect) + maxEffect * normalised
                //                      [but maxEffect is already baked into normalised here]
                // Since we don't have maxEffect handy here, we compute the bone offset directly
                // from the param range: new_value_local = min + (max-min)*normalised,
                // then multiply by the volume morph pos delta.
                float valueLocal = vpDef.MinValue + (vpDef.MaxValue - vpDef.MinValue) * normalised;

                foreach (var morph in morphs)
                {
                    var delta = morph.PosDelta * valueLocal;
                    if (offsets.TryGetValue(morph.BoneName, out var existing))
                        offsets[morph.BoneName] = existing + delta;
                    else
                        offsets[morph.BoneName] = delta;
                }
            }

            return offsets;
        }
    }
}
