/*
 * Copyright (c) 2026, Sjofn LLC
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

namespace LibreMetaverse
{
    /// <summary>
    /// CPU-side live state for a single particle in a <see cref="ParticleSimulator"/>.
    /// </summary>
    public struct LiveParticle
    {
        /// <summary>Current position relative to the emitter's origin.</summary>
        public Vector3 Position;

        /// <summary>Current velocity in metres per second.</summary>
        public Vector3 Velocity;

        /// <summary>Elapsed age of this particle in seconds.</summary>
        public float Age;

        /// <summary>
        /// Normalised age [0, 1] where 1 means the particle has reached its maximum
        /// configured lifetime.  Use for colour/scale interpolation.
        /// </summary>
        public float NormalizedAge;

        /// <summary>
        /// Interpolated RGBA colour for this tick.  Channels are in [0, 1].
        /// </summary>
        public Color4 Color;

        /// <summary>Interpolated scale (width, height) of the particle quad in metres.</summary>
        public float ScaleX;

        /// <summary>Interpolated scale (height) of the particle quad in metres.</summary>
        public float ScaleY;

        /// <summary>Interpolated glow strength [0, 1].</summary>
        public float Glow;
    }

    /// <summary>
    /// Purely CPU-side simulator for a Second Life / OpenSim particle system.
    /// Faithfully implements the <see cref="Primitive.ParticleSystem"/> rules from the
    /// SL viewer (lltextureentry, llviewerpartdata, llviewerpartsourceScript) so that
    /// any LibreMetaverse client — rendering or otherwise — can obtain the live
    /// particle positions, colours and sizes each frame.
    /// </summary>
    /// <remarks>
    /// Thread-safety: <see cref="Tick"/> is not thread-safe; call it from a single
    /// dedicated update thread.  <see cref="GetParticles"/> copies the internal state
    /// and is therefore safe to call from any thread after the update returns.
    /// </remarks>
    public sealed class ParticleSimulator
    {
        // Maximum hard particle count matching the SL viewer cap.
        private const int MaxParticles = 4096;

        private readonly Primitive.ParticleSystem _sys;
        private readonly Random _rng;
        private readonly List<LiveParticle> _particles = new List<LiveParticle>(256);

        private float _systemAge;
        private float _burstTimer;
        private bool  _active;

        // Cached flags for hot-path perf.
        private readonly bool _interpColor;
        private readonly bool _interpScale;
        private readonly bool _bounce;
        private readonly bool _wind;
        private readonly bool _followSrc;
        private readonly bool _targetPos;
        private readonly bool _targetLinear;
        private readonly bool _objRelative;

        /// <summary>The emitter origin in whatever coordinate space the caller uses.</summary>
        public Vector3 SourcePosition { get; set; }

        /// <summary>Optional target position (world space).</summary>
        public Vector3 TargetPosition { get; set; }

        /// <summary>
        /// Rotation of the emitter.  Used when <see cref="Primitive.ParticleSystem.ParticleFlags.ObjectRelative"/>
        /// is set to transform the initial velocity into world space.
        /// </summary>
        public Quaternion SourceRotation { get; set; } = Quaternion.Identity;

        /// <summary>Wind velocity, applied when the <c>Wind</c> particle flag is set.</summary>
        public Vector3 Wind { get; set; } = Vector3.Zero;

        /// <summary>Elapsed seconds since the system was started.</summary>
        public float SystemAge => _systemAge;

        /// <summary>
        /// True while the system is within its configured <see cref="Primitive.ParticleSystem.MaxAge"/>
        /// window.  When MaxAge == 0 the system runs forever.
        /// </summary>
        public bool IsActive => _active;

        /// <summary>
        /// Initialise a simulator for the given particle system definition.
        /// </summary>
        /// <param name="sys">The <see cref="Primitive.ParticleSystem"/> as decoded from the network.</param>
        /// <param name="seed">Optional RNG seed for reproducible tests.</param>
        public ParticleSimulator(Primitive.ParticleSystem sys, int seed = 0)
        {
            _sys    = sys;
            _rng    = seed == 0 ? new Random() : new Random(seed);
            _active = true;

            var df = sys.PartDataFlags;
            _interpColor  = (df & Primitive.ParticleSystem.ParticleDataFlags.InterpColor)  != 0;
            _interpScale  = (df & Primitive.ParticleSystem.ParticleDataFlags.InterpScale)  != 0;
            _bounce       = (df & Primitive.ParticleSystem.ParticleDataFlags.Bounce)       != 0;
            _wind         = (df & Primitive.ParticleSystem.ParticleDataFlags.Wind)         != 0;
            _followSrc    = (df & Primitive.ParticleSystem.ParticleDataFlags.FollowSrc)    != 0;
            _targetPos    = (df & Primitive.ParticleSystem.ParticleDataFlags.TargetPos)    != 0;
            _targetLinear = (df & Primitive.ParticleSystem.ParticleDataFlags.TargetLinear) != 0;
            _objRelative  = (sys.PartFlags & (uint)Primitive.ParticleSystem.ParticleFlags.ObjectRelative) != 0;
        }

        /// <summary>
        /// Advance the simulation by <paramref name="dt"/> seconds.
        /// Call this at a fixed interval (e.g. 30 Hz) from your update loop.
        /// </summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            // --- System lifetime ---
            if (_sys.MaxAge > 0f)
            {
                _systemAge += dt;
                if (_systemAge > _sys.MaxAge)
                {
                    _active = false;
                    // Existing particles continue to live out their age.
                }
            }
            else
            {
                _systemAge += dt;
            }

            // --- Age & physics existing particles ---
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Age += dt;

                if (p.Age >= _sys.PartMaxAge)
                {
                    _particles.RemoveAt(i);
                    continue;
                }

                // Physics
                p.Velocity += new Vector3(
                    _sys.PartAcceleration.X,
                    _sys.PartAcceleration.Y,
                    _sys.PartAcceleration.Z) * dt;

                if (_wind)
                    p.Velocity = Vector3.Lerp(p.Velocity, Wind, dt * 2f);

                p.Position += p.Velocity * dt;

                if (_bounce && p.Position.Z < 0f)
                {
                    p.Position = new Vector3(p.Position.X, p.Position.Y, 0f);
                    p.Velocity = new Vector3(p.Velocity.X, p.Velocity.Y, -p.Velocity.Z * 0.5f);
                }

                if (_targetPos || _targetLinear)
                {
                    var toTarget = TargetPosition - (SourcePosition + p.Position);
                    if (_targetLinear)
                        p.Velocity = Vector3.Normalize(toTarget) *
                                     ((_sys.BurstSpeedMin + _sys.BurstSpeedMax) * 0.5f);
                    else
                        p.Velocity += Vector3.Normalize(toTarget) * dt * 2f;
                }

                // Interpolate colour/scale/glow
                float t = _sys.PartMaxAge > 0f ? p.Age / _sys.PartMaxAge : 0f;
                t = Math.Max(0f, Math.Min(1f, t));

                if (_interpColor)
                    p.Color = Color4.Lerp(_sys.PartStartColor, _sys.PartEndColor, t);
                else
                    p.Color = _sys.PartStartColor;

                if (_interpScale)
                {
                    p.ScaleX = Lerp(_sys.PartStartScaleX, _sys.PartEndScaleX, t);
                    p.ScaleY = Lerp(_sys.PartStartScaleY, _sys.PartEndScaleY, t);
                }
                else
                {
                    p.ScaleX = _sys.PartStartScaleX;
                    p.ScaleY = _sys.PartStartScaleY;
                }

                p.Glow = Lerp(_sys.PartStartGlow, _sys.PartEndGlow, t);
                p.NormalizedAge = t;

                _particles[i] = p;
            }

            // --- Emit new particles ---
            if (_active && _systemAge >= _sys.StartAge)
            {
                _burstTimer += dt;
                float burstRate = Math.Max(_sys.BurstRate, 0.01f);
                while (_burstTimer >= burstRate)
                {
                    _burstTimer -= burstRate;
                    EmitBurst();
                }
            }
        }

        /// <summary>
        /// Returns a snapshot of the currently live particle list.
        /// Safe to call from any thread immediately after <see cref="Tick"/> returns.
        /// </summary>
        public IReadOnlyList<LiveParticle> GetParticles() => _particles.AsReadOnly();

        // ── Private helpers ───────────────────────────────────────────────────────

        private void EmitBurst()
        {
            int count = _sys.BurstPartCount;
            for (int i = 0; i < count && _particles.Count < MaxParticles; i++)
            {
                var vel = ComputeInitialVelocity();

                var p = new LiveParticle
                {
                    Position    = ComputeInitialPosition(),
                    Velocity    = vel,
                    Age         = 0f,
                    NormalizedAge = 0f,
                    Color       = _sys.PartStartColor,
                    ScaleX      = _sys.PartStartScaleX,
                    ScaleY      = _sys.PartStartScaleY,
                    Glow        = _sys.PartStartGlow,
                };
                _particles.Add(p);
            }
        }

        private Vector3 ComputeInitialPosition()
        {
            if (_sys.BurstRadius <= 0f) return Vector3.Zero;

            float r     = (float)_rng.NextDouble() * _sys.BurstRadius;
            float theta = (float)_rng.NextDouble() * 2f * MathPi;
            float phi   = (float)((_rng.NextDouble() - 0.5) * MathPi);
            return new Vector3(
                r * (float)Math.Cos(phi) * (float)Math.Cos(theta),
                r * (float)Math.Cos(phi) * (float)Math.Sin(theta),
                r * (float)Math.Sin(phi));
        }

        private Vector3 ComputeInitialVelocity()
        {
            float speed = Lerp(_sys.BurstSpeedMin, _sys.BurstSpeedMax, (float)_rng.NextDouble());
            Vector3 dir;

            switch (_sys.Pattern)
            {
                case Primitive.ParticleSystem.SourcePattern.Drop:
                    dir = Vector3.Zero;
                    break;

                case Primitive.ParticleSystem.SourcePattern.Explode:
                    dir = RandomUnitSphere();
                    break;

                case Primitive.ParticleSystem.SourcePattern.Angle:
                {
                    float outer = _sys.OuterAngle;
                    float inner = _sys.InnerAngle;
                    float angle = inner + (float)_rng.NextDouble() * (outer - inner);
                    float rot   = (float)_rng.NextDouble() * 2f * MathPi;
                    dir = new Vector3(
                        (float)Math.Sin(angle) * (float)Math.Cos(rot),
                        (float)Math.Sin(angle) * (float)Math.Sin(rot),
                        (float)Math.Cos(angle));
                    break;
                }

                case Primitive.ParticleSystem.SourcePattern.AngleCone:
                {
                    float outer = _sys.OuterAngle;
                    float inner = _sys.InnerAngle;
                    float angle = inner + (float)_rng.NextDouble() * (outer - inner);
                    float rot   = (float)_rng.NextDouble() * 2f * MathPi;
                    dir = new Vector3(
                        (float)Math.Sin(angle) * (float)Math.Cos(rot),
                        (float)Math.Sin(angle) * (float)Math.Sin(rot),
                        (float)Math.Cos(angle));
                    break;
                }

                case Primitive.ParticleSystem.SourcePattern.AngleConeEmpty:
                {
                    float inner = _sys.OuterAngle;  // outside the defined cone
                    float outer = MathPi;
                    float angle = inner + (float)_rng.NextDouble() * (outer - inner);
                    float rot   = (float)_rng.NextDouble() * 2f * MathPi;
                    dir = new Vector3(
                        (float)Math.Sin(angle) * (float)Math.Cos(rot),
                        (float)Math.Sin(angle) * (float)Math.Sin(rot),
                        (float)Math.Cos(angle));
                    break;
                }

                default:
                    dir = Vector3.UnitZ;
                    break;
            }

            if (_objRelative && dir != Vector3.Zero)
                dir = dir * SourceRotation;

            return dir * speed;
        }

        private Vector3 RandomUnitSphere()
        {
            float theta = (float)_rng.NextDouble() * 2f * MathPi;
            float phi   = (float)(Math.Acos(2.0 * _rng.NextDouble() - 1.0));
            return new Vector3(
                (float)(Math.Sin(phi) * Math.Cos(theta)),
                (float)(Math.Sin(phi) * Math.Sin(theta)),
                (float)Math.Cos(phi));
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private const float MathPi = 3.14159265358979323846f;
    }
}
