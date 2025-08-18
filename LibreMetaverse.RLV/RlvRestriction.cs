using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace LibreMetaverse.RLV
{
    public class RlvRestriction
    {
        /// <summary>
        /// The behavior type
        /// </summary>
        public RlvRestrictionType Behavior { get; }
        /// <summary>
        /// The original behavior type before getting translated into the common alias/synonym name "FarTouch" -> "TouchFar"
        /// </summary>
        public RlvRestrictionType OriginalBehavior { get; }
        /// <summary>
        /// The prim ID of the object that created this restrictions
        /// </summary>
        public Guid Sender { get; }
        /// <summary>
        /// The name of the object that created this restriction
        /// </summary>
        public string SenderName { get; }
        /// <summary>
        /// Arguments
        /// </summary>
        public ImmutableList<object> Args { get; }

        public bool IsException => IsRestrictionAnException(this);

        private const float MinimumCameraDrawDistance = 0.40f;

        public RlvRestriction(RlvRestrictionType behavior, Guid sender, string senderName, ICollection<object> args)
        {
            Behavior = GetRealRestriction(behavior);

            // HACK: I have no idea why these two secure commands have an exception variant. Exceptions come from the non-secure
            //       command, but are only valid if they come from the same sender. Just fudge it a little and treat these two as
            //       non-secure exceptions
            if (Behavior == RlvRestrictionType.SendChannelSec && args.Count == 1)
            {
                Behavior = RlvRestrictionType.SendChannel;
            }
            else if (Behavior == RlvRestrictionType.ShowNamesSec && args.Count == 1)
            {
                Behavior = RlvRestrictionType.ShowNames;
            }

            OriginalBehavior = behavior;
            Sender = sender;
            SenderName = senderName;
            Args = args.ToImmutableList();
        }

        internal static RlvRestrictionType GetRealRestriction(RlvRestrictionType restrictionType)
        {
            switch (restrictionType)
            {
                case RlvRestrictionType.CamDistMax:
                    return RlvRestrictionType.SetCamAvDistMax;
                case RlvRestrictionType.CamDistMin:
                    return RlvRestrictionType.SetCamAvDistMin;
                case RlvRestrictionType.CamUnlock:
                    return RlvRestrictionType.SetCamUnlock;
                case RlvRestrictionType.CamTextures:
                    return RlvRestrictionType.SetCamTextures;
                case RlvRestrictionType.FarTouch:
                    return RlvRestrictionType.TouchFar;
            }

            return restrictionType;
        }

        private static bool IsRestrictionAnException(RlvRestriction restriction)
        {
            switch (restriction.Behavior)
            {
                case RlvRestrictionType.RecvEmote:
                case RlvRestrictionType.RecvChat:
                case RlvRestrictionType.SendIm:
                case RlvRestrictionType.StartIm:
                case RlvRestrictionType.RecvIm:
                case RlvRestrictionType.SendChannel:
                case RlvRestrictionType.TpRequest:
                case RlvRestrictionType.TpLure:
                case RlvRestrictionType.Edit:
                case RlvRestrictionType.Share:
                case RlvRestrictionType.TouchWorld:
                case RlvRestrictionType.ShowNamesSec:
                case RlvRestrictionType.ShowNames:
                case RlvRestrictionType.ShowNameTags:
                case RlvRestrictionType.AcceptTp:
                case RlvRestrictionType.AcceptTpRequest:
                    return restriction.Args.Count > 0;

                case RlvRestrictionType.DetachThisExcept:
                case RlvRestrictionType.DetachAllThisExcept:
                case RlvRestrictionType.AttachThisExcept:
                case RlvRestrictionType.AttachAllThisExcept:
                    return true;
            }

            return false;
        }

        internal static bool ParseOptions(RlvRestrictionType behavior, string options, out List<object> parsedArgs)
        {
            parsedArgs = new List<object>();
            var args = options.Split([';'], StringSplitOptions.RemoveEmptyEntries);

            switch (behavior)
            {
                case RlvRestrictionType.Notify:
                {
                    if (args.Length < 1 || !int.TryParse(args[0], out var channel))
                    {
                        return false;
                    }
                    parsedArgs.Add(channel);

                    if (args.Length == 2)
                    {
                        parsedArgs.Add(args[1]);
                    }

                    return true;
                }

                case RlvRestrictionType.CamDrawMin:
                case RlvRestrictionType.CamDrawMax:
                {
                    if (args.Length < 1 || !float.TryParse(args[0], out var val))
                    {
                        return false;
                    }

                    if (val < MinimumCameraDrawDistance)
                    {
                        return false;
                    }

                    parsedArgs.Add(val);

                    return true;
                }

                case RlvRestrictionType.CamZoomMax:
                case RlvRestrictionType.CamZoomMin:
                case RlvRestrictionType.SetCamFovMin:
                case RlvRestrictionType.SetCamFovMax:
                case RlvRestrictionType.CamDistMax:
                case RlvRestrictionType.SetCamAvDistMax:
                case RlvRestrictionType.CamDistMin:
                case RlvRestrictionType.SetCamAvDistMin:
                case RlvRestrictionType.CamDrawAlphaMin:
                case RlvRestrictionType.CamDrawAlphaMax:
                case RlvRestrictionType.CamAvDist:
                {
                    if (args.Length < 1 || !float.TryParse(args[0], out var val))
                    {
                        return false;
                    }
                    parsedArgs.Add(val);

                    return true;
                }

                case RlvRestrictionType.SitTp:
                case RlvRestrictionType.FarTouch:
                case RlvRestrictionType.TouchFar:
                case RlvRestrictionType.TpLocal:
                {
                    if (args.Length == 0)
                    {
                        return true;
                    }

                    if (args.Length != 1 || !float.TryParse(args[0], out var val))
                    {
                        return false;
                    }

                    parsedArgs.Add(val);
                    return true;
                }

                case RlvRestrictionType.CamDrawColor:
                {
                    if (args.Length != 3)
                    {
                        return false;
                    }

                    foreach (var arg in args)
                    {
                        if (!float.TryParse(arg, out var val))
                        {
                            return false;
                        }

                        parsedArgs.Add(val);
                    }
                    return true;
                }

                case RlvRestrictionType.RedirChat:
                case RlvRestrictionType.RedirEmote:
                case RlvRestrictionType.SendChannelExcept:
                {
                    if (args.Length != 1 || !int.TryParse(args[0], out var val))
                    {
                        return false;
                    }

                    parsedArgs.Add(val);
                    return true;
                }

                case RlvRestrictionType.SendChannel:
                case RlvRestrictionType.SendChannelSec:
                {
                    if (args.Length == 0)
                    {
                        return true;
                    }

                    if (args.Length != 1 || !int.TryParse(args[0], out var val))
                    {
                        return false;
                    }

                    parsedArgs.Add(val);
                    return true;
                }

                case RlvRestrictionType.SendImTo:
                case RlvRestrictionType.RecvImFrom:
                {
                    // [Guid | string]
                    if (args.Length != 1)
                    {
                        return false;
                    }

                    if (Guid.TryParse(args[0], out var val))
                    {
                        parsedArgs.Add(val);
                    }
                    else
                    {
                        parsedArgs.Add(args[0]);
                    }

                    return true;
                }

                case RlvRestrictionType.SendIm:
                case RlvRestrictionType.RecvIm:
                {
                    // [] | [Guid | string]
                    if (args.Length == 0)
                    {
                        return true;
                    }

                    if (args.Length != 1)
                    {
                        return false;
                    }

                    if (Guid.TryParse(args[0], out var val))
                    {
                        parsedArgs.Add(val);
                    }
                    else
                    {
                        parsedArgs.Add(args[0]);
                    }

                    return true;
                }

                case RlvRestrictionType.Detach:
                {
                    // [] | [AttachmentPoint]
                    if (args.Length == 0)
                    {
                        return true;
                    }

                    if (args.Length != 1)
                    {
                        return false;
                    }

                    if (!RlvCommon.RlvAttachmentPointMap.TryGetValue(args[0], out var val))
                    {
                        return false;
                    }

                    parsedArgs.Add(val);
                    return true;
                }

                case RlvRestrictionType.AddAttach:
                case RlvRestrictionType.RemAttach:
                {
                    // [] | [AttachmentPoint]
                    if (args.Length == 0)
                    {
                        return true;
                    }

                    if (args.Length != 1)
                    {
                        return false;
                    }

                    if (!RlvCommon.RlvAttachmentPointMap.TryGetValue(args[0], out var val))
                    {
                        return false;
                    }

                    parsedArgs.Add(val);
                    return true;
                }

                case RlvRestrictionType.AddOutfit:
                case RlvRestrictionType.RemOutfit:
                {
                    // [] || [layer]
                    if (args.Length == 0)
                    {
                        return true;
                    }

                    if (args.Length != 1)
                    {
                        return false;
                    }

                    if (!RlvCommon.RlvWearableTypeMap.TryGetValue(args[0], out var val))
                    {
                        return false;
                    }

                    parsedArgs.Add(val);
                    return true;
                }


                case RlvRestrictionType.DetachThis:
                case RlvRestrictionType.DetachAllThis:
                case RlvRestrictionType.AttachThis:
                case RlvRestrictionType.AttachAllThis:
                {
                    // [] || [layer | attachpt | string]
                    if (args.Length == 0)
                    {
                        return true;
                    }

                    if (args.Length != 1)
                    {
                        return false;
                    }

                    if (RlvCommon.RlvWearableTypeMap.TryGetValue(args[0], out var wearableType))
                    {
                        parsedArgs.Add(wearableType);
                        return true;
                    }
                    else if (RlvCommon.RlvAttachmentPointMap.TryGetValue(args[0], out var attachmentPoint))
                    {
                        parsedArgs.Add(attachmentPoint);
                        return true;
                    }

                    parsedArgs.Add(args[0]);
                    return true;
                }

                case RlvRestrictionType.DetachThisExcept:
                case RlvRestrictionType.DetachAllThisExcept:
                case RlvRestrictionType.AttachThisExcept:
                case RlvRestrictionType.AttachAllThisExcept:
                {
                    // [string]
                    if (args.Length != 1)
                    {
                        return false;
                    }

                    parsedArgs.Add(args[0]);
                    return true;
                }

                case RlvRestrictionType.CamTextures:
                case RlvRestrictionType.SetCamTextures:
                case RlvRestrictionType.RecvChat:
                case RlvRestrictionType.RecvEmote:
                case RlvRestrictionType.StartIm:
                case RlvRestrictionType.TpLure:
                case RlvRestrictionType.AcceptTp:
                case RlvRestrictionType.AcceptTpRequest:
                case RlvRestrictionType.TpRequest:
                case RlvRestrictionType.Edit:
                case RlvRestrictionType.Share:
                case RlvRestrictionType.TouchWorld:
                case RlvRestrictionType.TouchAttachOther:
                case RlvRestrictionType.TouchHud:
                case RlvRestrictionType.ShowNames:
                case RlvRestrictionType.ShowNamesSec:
                case RlvRestrictionType.ShowNameTags:
                {
                    // [] [Guid]
                    if (args.Length == 0)
                    {
                        return true;
                    }

                    if (args.Length != 1)
                    {
                        return false;
                    }

                    if (!Guid.TryParse(args[0], out var uuid))
                    {
                        return false;
                    }

                    parsedArgs.Add(uuid);
                    return true;
                }

                case RlvRestrictionType.RecvChatFrom:
                case RlvRestrictionType.RecvEmoteFrom:
                case RlvRestrictionType.StartImTo:
                case RlvRestrictionType.EditObj:
                case RlvRestrictionType.TouchThis:
                case RlvRestrictionType.ShowHoverText:
                {
                    // [Guid]
                    if (args.Length != 1)
                    {
                        return false;
                    }

                    if (!Guid.TryParse(args[0], out var uuid))
                    {
                        return false;
                    }

                    parsedArgs.Add(uuid);
                    return true;
                }

                case RlvRestrictionType.Permissive:
                case RlvRestrictionType.SendChat:
                case RlvRestrictionType.ChatShout:
                case RlvRestrictionType.ChatNormal:
                case RlvRestrictionType.ChatWhisper:
                case RlvRestrictionType.Emote:
                case RlvRestrictionType.RecvChatSec:
                case RlvRestrictionType.RecvEmoteSec:
                case RlvRestrictionType.SendGesture:
                case RlvRestrictionType.SendImSec:
                case RlvRestrictionType.RecvImSec:
                case RlvRestrictionType.TpLureSec:
                case RlvRestrictionType.TpRequestSec:
                case RlvRestrictionType.ShareSec:
                case RlvRestrictionType.Fly:
                case RlvRestrictionType.Jump:
                case RlvRestrictionType.TempRun:
                case RlvRestrictionType.AlwaysRun:
                case RlvRestrictionType.CamUnlock:
                case RlvRestrictionType.SetCamUnlock:
                case RlvRestrictionType.TpLm:
                case RlvRestrictionType.TpLoc:
                case RlvRestrictionType.StandTp:
                case RlvRestrictionType.ShowInv:
                case RlvRestrictionType.ViewNote:
                case RlvRestrictionType.ViewScript:
                case RlvRestrictionType.ViewTexture:
                case RlvRestrictionType.Unsit:
                case RlvRestrictionType.Sit:
                case RlvRestrictionType.DefaultWear:
                case RlvRestrictionType.SetGroup:
                case RlvRestrictionType.SetDebug:
                case RlvRestrictionType.SetEnv:
                case RlvRestrictionType.AllowIdle:
                case RlvRestrictionType.ShowWorldMap:
                case RlvRestrictionType.ShowMiniMap:
                case RlvRestrictionType.ShowLoc:
                case RlvRestrictionType.ShowNearby:
                case RlvRestrictionType.EditWorld:
                case RlvRestrictionType.EditAttach:
                case RlvRestrictionType.Rez:
                case RlvRestrictionType.DenyPermission:
                case RlvRestrictionType.AcceptPermission:
                case RlvRestrictionType.UnsharedWear:
                case RlvRestrictionType.UnsharedUnwear:
                case RlvRestrictionType.SharedWear:
                case RlvRestrictionType.SharedUnwear:
                case RlvRestrictionType.TouchAll:
                case RlvRestrictionType.TouchMe:
                case RlvRestrictionType.TouchAttach:
                case RlvRestrictionType.TouchAttachSelf:
                case RlvRestrictionType.Interact:
                case RlvRestrictionType.ShowHoverTextAll:
                case RlvRestrictionType.ShowHoverTextHud:
                case RlvRestrictionType.ShowHoverTextWorld:
                    // []
                    return args.Length == 0;
                default:
                    throw new NotImplementedException();
            }
        }

        public override bool Equals(object obj)
        {
            return obj is RlvRestriction restriction &&
                   Behavior == restriction.Behavior &&
                   Sender.Equals(restriction.Sender) &&
                   Args.SequenceEqual(restriction.Args);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(Behavior);
            hashCode.Add(Sender);
            foreach (var item in Args)
            {
                hashCode.Add(item);
            }

            return hashCode.ToHashCode();
        }

        public override string ToString()
        {
            var argsString = "";
            if (Args != null)
            {
                argsString = string.Join(", ", Args);
            }

            return $"RlvRestriction: Behavior={Behavior} SenderName=\"{SenderName}\" Args=[{argsString}]";
        }
    }
}
