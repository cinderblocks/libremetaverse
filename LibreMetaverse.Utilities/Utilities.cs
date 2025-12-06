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

namespace OpenMetaverse.Utilities
{
    /// <summary>
    /// 
    /// </summary>
    public enum WaterType
    {
        /// <summary></summary>
        Unknown,
        /// <summary></summary>
        Dry,
        /// <summary></summary>
        Waterfront,
        /// <summary></summary>
        Underwater
    }

    public static class Realism
    {
        /// <summary>
        /// Aims at the specified position, enters mouselook, presses and
        /// releases the left mouse button, and leaves mouselook
        /// </summary>
        /// <param name="client"></param>
        /// <param name="target">Target to shoot at</param>
        /// <returns></returns>
        public static bool Shoot(GridClient client, Vector3 target)
        {
            return client.Self.Movement.TurnToward(target) && Shoot(client);
        }

        /// <summary>
        /// Enters mouselook, presses and releases the left mouse button, and leaves mouselook
        /// </summary>
        /// <returns></returns>
        public static bool Shoot(GridClient client)
        {
            if (client.Settings.SEND_AGENT_UPDATES)
            {
                client.Self.Movement.Mouselook = true;
                client.Self.Movement.MLButtonDown = true;
                client.Self.Movement.SendUpdate();

                client.Self.Movement.MLButtonUp = true;
                client.Self.Movement.MLButtonDown = false;
                client.Self.Movement.FinishAnim = true;
                client.Self.Movement.SendUpdate();

                client.Self.Movement.Mouselook = false;
                client.Self.Movement.MLButtonUp = false;
                client.Self.Movement.FinishAnim = false;
                client.Self.Movement.SendUpdate();

                return true;
            }
            else
            {
                Logger.Warn("Attempted Shoot but agent updates are disabled", client);
                return false;
            }
        }

        /// <summary>
        ///  A pseudo-realistic chat function that uses the typing sound and
        /// animation, types at three characters per second, and randomly 
        /// pauses. This function will block until the message has been sent
        /// </summary>
        /// <param name="client">A reference to the client that will chat</param>
        /// <param name="message">The chat message to send</param>
        public static void Chat(GridClient client, string message)
        {
            Chat(client, message, ChatType.Normal, 3);
        }

        /// <summary>
        /// A pseudo-realistic chat function that uses the typing sound and
        /// animation, types at a given rate, and randomly pauses. This 
        /// function will block until the message has been sent
        /// </summary>
        /// <param name="client">A reference to the client that will chat</param>
        /// <param name="message">The chat message to send</param>
        /// <param name="type">The chat type (usually Normal, Whisper or Shout)</param>
        /// <param name="cps">Characters per second rate for chatting</param>
        // Synchronous compatibility wrapper that blocks on the async implementation
        public static void Chat(GridClient client, string message, ChatType type, int cps)
        {
            ChatAsync(client, message, type, cps).GetAwaiter().GetResult();
        }

        // Async implementation using Task.Delay so callers can avoid blocking threads
        public static async Task ChatAsync(GridClient client, string message, ChatType type, int cps, CancellationToken cancellationToken = default)
        {
            var rand = new Random();
            var characters = 0;
            var typing = true;

            // Start typing
            client.Self.Chat(string.Empty, 0, ChatType.StartTyping);
            client.Self.AnimationStart(Animations.TYPE, false);

            while (characters < message.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!typing)
                {
                    // Start typing again
                    client.Self.Chat(string.Empty, 0, ChatType.StartTyping);
                    client.Self.AnimationStart(Animations.TYPE, false);
                    typing = true;
                }
                else
                {
                    // Randomly pause typing
                    if (rand.Next(10) >= 9)
                    {
                        client.Self.Chat(string.Empty, 0, ChatType.StopTyping);
                        client.Self.AnimationStop(Animations.TYPE, false);
                        typing = false;
                    }
                }

                // Async delay for a second and increase the amount of characters we've typed
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                characters += cps;
            }

            // Send the message
            client.Self.Chat(message, 0, type);

            // Stop typing
            client.Self.Chat(string.Empty, 0, ChatType.StopTyping);
            client.Self.AnimationStop(Animations.TYPE, false);
        }
    }

    public class ConnectionManager
    {
        private readonly GridClient _client;
        private ulong _simHandle;
        private Vector3 _position = Vector3.Zero;
        private CancellationTokenSource _checkCts;
        private Task _checkTask;
        private readonly int _timerFrequency;

        public ConnectionManager(GridClient client, int timerFrequency)
        {
            _client = client;

            _timerFrequency = timerFrequency;
        }

        public static bool PersistentLogin(GridClient client, string firstName, string lastName, string password,
            string userAgent, string start, string author)
        {
            int unknownLogins = 0;

        Start:

            if (client.Network.Login(firstName, lastName, password, userAgent, start, author))
            {
                Logger.Info("Logged in to " + client.Network.CurrentSim, client);
                return true;
            }
            switch (client.Network.LoginErrorKey)
            {
                case "god":
                    Logger.Warn("Grid is down, waiting 10 minutes", client);
                    LoginWait(10);
                    goto Start;
                case "key":
                    Logger.Error("Bad username or password, giving up on login", client);
                    return false;
                case "presence":
                    Logger.Warn("Server is still logging us out, waiting 1 minute", client);
                    LoginWait(1);
                    goto Start;
                case "disabled":
                    Logger.Error("This account has been banned! Giving up on login", client);
                    return false;
                case "timed out":
                case "no connection":
                    Logger.Warn("Login request timed out, waiting 1 minute", client);
                    LoginWait(1);
                    goto Start;
                case "bad response":
                    Logger.Warn("Login server returned unparsable result", client);
                    LoginWait(1);
                    goto Start;
                default:
                    ++unknownLogins;

                    if (unknownLogins < 5)
                    {
                        Logger.Warn("Unknown login error, waiting 2 minutes: " + client.Network.LoginErrorKey, client);
                        LoginWait(2);
                        goto Start;
                    }
                    Logger.Error("Too many unknown login error codes, giving up", client);
                    return false;
            }
        }

        public void StayInSim(ulong handle, Vector3 desiredPosition)
        {
            _simHandle = handle;
            _position = desiredPosition;
            StartCheckLoop();
        }

        private void StartCheckLoop()
        {
            if (_checkCts != null) return;
            _checkCts = new CancellationTokenSource();
            var token = _checkCts.Token;
            _checkTask = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            if (_simHandle != 0 && _client?.Network?.CurrentSim?.Handle != 0 && _client.Network.CurrentSim.Handle != _simHandle)
                            {
                                // Attempt to move to our target sim
                                _client.Self.Teleport(_simHandle, _position);
                             }
                        }
                        catch (Exception) { }

                        await Task.Delay(_timerFrequency, token).ConfigureAwait(false);
                    }
                }
                catch (TaskCanceledException) { }
            }, token);
        }

        public void Stop()
        {
            LibreMetaverse.DisposalHelper.SafeCancelAndDispose(_checkCts, (m, ex) => Logger.Debug(m, ex));
            _checkCts = null;
            _checkTask = null;
        }

        private static void LoginWait(int minutes)
        {
            // Keep blocking behavior for compatibility; callers that want non-blocking should use async APIs.
            Thread.Sleep(TimeSpan.FromMinutes(minutes));
        }
    }
}

