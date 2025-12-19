/**
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
using OpenMetaverse.Packets;

namespace OpenMetaverse
{
    public partial class AgentManager
    {
        public void PointAtEffect(UUID sourceAvatar, UUID targetObject, Vector3d globalOffset, PointAtType type,
            UUID effectID)
        {
            ViewerEffectPacket effect = new ViewerEffectPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Effect = new ViewerEffectPacket.EffectBlock[1]
            };


            effect.Effect[0] = new ViewerEffectPacket.EffectBlock
            {
                AgentID = Client.Self.AgentID,
                Color = new byte[4],
                Duration = (type == PointAtType.Clear) ? 0.0f : float.MaxValue / 4.0f,
                ID = effectID,
                Type = (byte)EffectType.PointAt
            };

            byte[] typeData = new byte[57];
            if (sourceAvatar != UUID.Zero)
                Buffer.BlockCopy(sourceAvatar.GetBytes(), 0, typeData, 0, 16);
            if (targetObject != UUID.Zero)
                Buffer.BlockCopy(targetObject.GetBytes(), 0, typeData, 16, 16);
            Buffer.BlockCopy(globalOffset.GetBytes(), 0, typeData, 32, 24);
            typeData[56] = (byte)type;

            effect.Effect[0].TypeData = typeData;

            Client.Network.SendPacket(effect);
        }

        public void LookAtEffect(UUID sourceAvatar, UUID targetObject, Vector3d globalOffset, LookAtType type,
            UUID effectID)
        {
            ViewerEffectPacket effect = new ViewerEffectPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                }
            };


            float duration;

            switch (type)
            {
                case LookAtType.Clear:
                    duration = 2.0f;
                    break;
                case LookAtType.Hover:
                    duration = 1.0f;
                    break;
                case LookAtType.FreeLook:
                    duration = 2.0f;
                    break;
                case LookAtType.Idle:
                    duration = 3.0f;
                    break;
                case LookAtType.AutoListen:
                case LookAtType.Respond:
                    duration = 4.0f;
                    break;
                case LookAtType.None:
                case LookAtType.Select:
                case LookAtType.Focus:
                case LookAtType.Mouselook:
                    duration = float.MaxValue / 2.0f;
                    break;
                default:
                    duration = 0.0f;
                    break;
            }

            effect.Effect = new ViewerEffectPacket.EffectBlock[1];
            effect.Effect[0] = new ViewerEffectPacket.EffectBlock
            {
                AgentID = Client.Self.AgentID,
                Color = new byte[4],
                Duration = duration,
                ID = effectID,
                Type = (byte)EffectType.LookAt
            };

            byte[] typeData = new byte[57];
            Buffer.BlockCopy(sourceAvatar.GetBytes(), 0, typeData, 0, 16);
            Buffer.BlockCopy(targetObject.GetBytes(), 0, typeData, 16, 16);
            Buffer.BlockCopy(globalOffset.GetBytes(), 0, typeData, 32, 24);
            typeData[56] = (byte)type;

            effect.Effect[0].TypeData = typeData;

            Client.Network.SendPacket(effect);
        }

        public void BeamEffect(UUID sourceAvatar, UUID targetObject, Vector3d globalOffset, Color4 color,
            float duration, UUID effectID)
        {
            ViewerEffectPacket effect = new ViewerEffectPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Effect = new ViewerEffectPacket.EffectBlock[1]
            };


            effect.Effect[0] = new ViewerEffectPacket.EffectBlock
            {
                AgentID = Client.Self.AgentID,
                Color = color.GetBytes(),
                Duration = duration,
                ID = effectID,
                Type = (byte)EffectType.Beam
            };

            byte[] typeData = new byte[56];
            Buffer.BlockCopy(sourceAvatar.GetBytes(), 0, typeData, 0, 16);
            Buffer.BlockCopy(targetObject.GetBytes(), 0, typeData, 16, 16);
            Buffer.BlockCopy(globalOffset.GetBytes(), 0, typeData, 32, 24);

            effect.Effect[0].TypeData = typeData;

            Client.Network.SendPacket(effect);
        }

        public void SphereEffect(Vector3d globalOffset, Color4 color, float duration, UUID effectID)
        {
            ViewerEffectPacket effect = new ViewerEffectPacket
            {
                AgentData =
                {
                    AgentID = Client.Self.AgentID,
                    SessionID = Client.Self.SessionID
                },
                Effect = new ViewerEffectPacket.EffectBlock[1]
            };


            effect.Effect[0] = new ViewerEffectPacket.EffectBlock
            {
                AgentID = Client.Self.AgentID,
                Color = color.GetBytes(),
                Duration = duration,
                ID = effectID,
                Type = (byte)EffectType.Sphere
            };

            byte[] typeData = new byte[56];
            Buffer.BlockCopy(UUID.Zero.GetBytes(), 0, typeData, 0, 16);
            Buffer.BlockCopy(UUID.Zero.GetBytes(), 0, typeData, 16, 16);
            Buffer.BlockCopy(globalOffset.GetBytes(), 0, typeData, 32, 24);

            effect.Effect[0].TypeData = typeData;

            Client.Network.SendPacket(effect);
        }
    }
}