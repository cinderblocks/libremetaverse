/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2025, Sjofn LLC.
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
using System.Threading;
using System.Threading.Tasks;

namespace OpenMetaverse
{
    internal sealed class InterpolationService : IDisposable
    {
        private readonly GridClient _client;
#if NET6_0_OR_GREATER
        private System.Threading.PeriodicTimer _periodicTimer;
        private CancellationTokenSource _cts;
        private Task _loopTask;
#else
        private Timer _timer;
#endif
        private bool _started;

        public InterpolationService(GridClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public void Start()
        {
            if (!_client.Settings.USE_INTERPOLATION_TIMER || _started) return;

#if NET6_0_OR_GREATER
            _cts = new CancellationTokenSource();
            _periodicTimer = new System.Threading.PeriodicTimer(TimeSpan.FromMilliseconds(_client.Settings.INTERPOLATION_INTERVAL));
            _loopTask = Task.Run(() => LoopAsync(_cts.Token));
#else
            _timer = new Timer(TimerElapsed, null, _client.Settings.INTERPOLATION_INTERVAL, Timeout.Infinite);
#endif
            _started = true;
        }

        public void Stop()
        {
            if (!_started) return;

#if NET6_0_OR_GREATER
            try { _cts?.Cancel(); } catch { }
            try { _loopTask?.Wait(500); } catch { }
            try { _periodicTimer?.Dispose(); } catch { }
            try { _cts?.Dispose(); } catch { }
            _periodicTimer = null;
            _cts = null;
            _loopTask = null;
#else
            try { _timer?.Dispose(); } catch { }
            _timer = null;
#endif
            _started = false;
        }

#if NET6_0_OR_GREATER
        private async Task LoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (await _periodicTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                {
                    PerformInterpolationPass();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
            catch (Exception ex)
            {
                Logger.Error("Interpolation loop failed: " + ex.Message, ex, _client);
            }
        }

#else
        private void TimerElapsed(object state)
        {
            PerformInterpolationPass();

            // Start the timer again. Use a minimum of a 50ms pause in between calculations
            int elapsed = _client.Self.lastInterpolation - Environment.TickCount;
            int delay = Math.Max(50, _client.Settings.INTERPOLATION_INTERVAL - elapsed);
            _timer?.Change(delay, Timeout.Infinite);
        }
#endif

        private void PerformInterpolationPass()
        {
            int elapsed = 0;

            if (_client.Network.Connected)
            {
                int start = Environment.TickCount;

                int interval = unchecked(Environment.TickCount - _client.Self.lastInterpolation);
                float seconds = interval / 1000f;

                // Iterate through all simulators
                var sims = _client.Network.Simulators.ToArray();
                foreach (var sim in sims)
                {
                    float adjSeconds = seconds * sim.Stats.Dilation;

                    // Iterate through all of this region's avatars
                    foreach (var avatar in sim.ObjectsAvatars)
                    {
                        var av = avatar.Value;

                        Vector3 velocity, acceleration, position;
                        lock (av)
                        {
                            velocity = av.Velocity;
                            acceleration = av.Acceleration;
                            position = av.Position;
                        }

                        if (acceleration != Vector3.Zero)
                        {
                            velocity += acceleration * adjSeconds;
                        }

                        if (velocity != Vector3.Zero)
                        {
                            position += velocity * adjSeconds;
                        }

                        lock (av)
                        {
                            av.Velocity = velocity;
                            av.Position = position;
                        }
                    }

                    // Iterate through all the simulator's primitives
                    foreach (var prim in sim.ObjectsPrimitives)
                    {
                        var pv = prim.Value;

                        JointType joint;
                        Vector3 angVel, velocity, acceleration, position;
                        Quaternion rotation;

                        lock (pv)
                        {
                            joint = pv.Joint;
                            angVel = pv.AngularVelocity;
                            velocity = pv.Velocity;
                            acceleration = pv.Acceleration;
                            position = pv.Position;
                            rotation = pv.Rotation;
                        }

                        switch (joint)
                        {
                            case JointType.Invalid:
                            {
                                const float omegaThresholdSquared = 0.00001f;
                                float omegaSquared = angVel.LengthSquared();

                                if (omegaSquared > omegaThresholdSquared)
                                {
                                    float omega = (float)Math.Sqrt(omegaSquared);
                                    float angle = omega * adjSeconds;
                                    Vector3 normalizedAngVel = angVel * (1.0f / omega);
                                    Quaternion dQ = Quaternion.CreateFromAxisAngle(normalizedAngVel, angle);

                                    rotation *= dQ;
                                }

                                // Only do movement interpolation (extrapolation) when there is non-zero velocity
                                // but no acceleration
                                if (velocity != Vector3.Zero && acceleration == Vector3.Zero)
                                {
                                    position += (velocity + acceleration *
                                        (0.5f * (adjSeconds - ObjectManager.HAVOK_TIMESTEP))) * adjSeconds;
                                    velocity += acceleration * adjSeconds;
                                }

                                lock (pv)
                                {
                                    pv.Position = position;
                                    pv.Velocity = velocity;
                                    pv.Rotation = rotation;
                                }

                                break;
                            }
                            case JointType.Hinge:
                                //FIXME: Hinge movement extrapolation
                                break;
                            case JointType.Point:
                                //FIXME: Point movement extrapolation
                                break;
                            default:
                                Logger.Warn($"Unhandled joint type {joint}", _client);
                                break;
                        }
                    }
                }

                // Make sure the last interpolated time is always updated
                _client.Self.lastInterpolation = Environment.TickCount;

                elapsed = _client.Self.lastInterpolation - start;
            }

            // No scheduling here; scheduling handled by caller
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
