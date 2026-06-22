/*
 * Copyright (c) 2006-2016, openmetaverse.co
 * Copyright (c) 2019-2025, Sjofn LLC
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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LibreMetaverse.Assets;
using LibreMetaverse.Packets;

namespace LibreMetaverse
{
    public partial class AgentManager
    {
        /// <summary>
        /// Plays a gesture
        /// </summary>
        /// <param name="gestureID">Asset <see cref="UUID"/> of the gesture</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public Task PlayGestureAsync(UUID gestureID, CancellationToken cancellationToken = default)
        {
            return Task.Run(async () =>
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));

                var asset = await Client.Assets.RequestAssetAsync(gestureID, AssetType.Gesture, true, cts.Token)
                    .ConfigureAwait(false);

                if (!(asset is AssetGesture gesture) || !gesture.Decode()) return;
                foreach (GestureStep step in gesture.Sequence)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    switch (step.GestureStepType)
                    {
                        case GestureStepType.Chat:
                            string text = ((GestureStepChat) step).Text;
                            int channel = 0;
                            Match m;

                            if (
                            (m =
                                Regex.Match(text, @"^/(?<channel>-?[0-9]+)\s*(?<text>.*)",
                                    RegexOptions.CultureInvariant)).Success)
                            {
                                if (int.TryParse(m.Groups["channel"].Value, out channel))
                                {
                                    text = m.Groups["text"].Value;
                                }
                            }

                            Chat(text, channel, ChatType.Normal);
                            break;

                        case GestureStepType.Animation:
                            GestureStepAnimation anim = (GestureStepAnimation) step;

                            if (anim.AnimationStart)
                            {
                                if (SignaledAnimations.ContainsKey(anim.ID))
                                {
                                    AnimationStop(anim.ID, true);
                                }
                                AnimationStart(anim.ID, true);
                            }
                            else
                            {
                                AnimationStop(anim.ID, true);
                            }
                            break;

                        case GestureStepType.Sound:
                            Client.Sound.PlaySound(((GestureStepSound) step).ID);
                            break;

                        case GestureStepType.Wait:
                            GestureStepWait wait = (GestureStepWait) step;
                            if (wait.WaitForTime)
                            {
                                await Task.Delay((int)(1000f * wait.WaitTime), cts.Token).ConfigureAwait(false);
                            }
                            if (wait.WaitForAnimation)
                            {
                                // TODO: implement waiting for all animations to end that were triggered
                                // during playing of this gesture sequence
                            }
                            break;
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Mark gesture active
        /// </summary>
        /// <param name="invID">Inventory <see cref="UUID"/> of the gesture</param>
        /// <param name="assetID">Asset <see cref="UUID"/> of the gesture</param>
        public void ActivateGesture(UUID invID, UUID assetID)
        {
            ActivateGesturesPacket packet = new ActivateGesturesPacket
            {
                AgentData =
                {
                    AgentID = AgentID,
                    SessionID = SessionID,
                    Flags = 0x00
                }
            };


            ActivateGesturesPacket.DataBlock block = new ActivateGesturesPacket.DataBlock
            {
                ItemID = invID,
                AssetID = assetID,
                GestureFlags = 0x00
            };

            packet.Data = new ActivateGesturesPacket.DataBlock[1];
            packet.Data[0] = block;

            Client.Network.SendPacket(packet);
            ActiveGestures[invID] = assetID;
        }

        /// <summary>
        /// Mark gesture inactive
        /// </summary>
        /// <param name="invID">Inventory <see cref="UUID"/> of the gesture</param>
        public void DeactivateGesture(UUID invID)
        {
            DeactivateGesturesPacket p = new DeactivateGesturesPacket
            {
                AgentData =
                {
                    AgentID = AgentID,
                    SessionID = SessionID,
                    Flags = 0x00
                }
            };


            DeactivateGesturesPacket.DataBlock b = new DeactivateGesturesPacket.DataBlock
            {
                ItemID = invID,
                GestureFlags = 0x00
            };

            p.Data = new DeactivateGesturesPacket.DataBlock[1];
            p.Data[0] = b;

            Client.Network.SendPacket(p);
            ActiveGestures.TryRemove(invID, out _);
        }
    }
}
