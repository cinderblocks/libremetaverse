using System;
using System.Collections.Generic;
using System.Linq;
using LibreMetaverse;
using OpenMetaverse.Packets;

namespace OpenMetaverse.TestClient
{
    public class CloneCommand : Command
    {
        uint _serialNum = 2;
        readonly CacheDictionary<UUID, AvatarAppearancePacket> Appearances = 
            new CacheDictionary<UUID, AvatarAppearancePacket>(100, new LruRemovalStrategy<UUID>());

        public CloneCommand(TestClient testClient)
        {
            Name = "clone";
            Description = "Clone the appearance of a nearby avatar. Usage: clone [name]";
            Category = CommandCategory.Appearance;

            testClient.Network.RegisterCallback(PacketType.AvatarAppearance, AvatarAppearanceHandler);
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            string targetName = string.Empty;
            List<DirectoryManager.AgentSearchData> matches;

            targetName = args.Aggregate(targetName, (current, t) => current + t + " ");
            targetName = targetName.TrimEnd();

            if (targetName.Length == 0)
                return "Usage: clone [name]";

#pragma warning disable CS0618 // Type or member is obsolete
            if (Client.Directory.PeopleSearch(DirectoryManager.DirFindFlags.People, targetName, 0, 1000 * 10,
                out matches) && matches.Count > 0)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                UUID target = matches[0].AgentID;
                targetName += $" ({target})";

                if (Appearances.ContainsKey(target))
                {
                    #region AvatarAppearance to AgentSetAppearance

                    AvatarAppearancePacket appearance = Appearances[target];

                    AgentSetAppearancePacket set = new AgentSetAppearancePacket
                    {
                        AgentData =
                        {
                            AgentID = Client.Self.AgentID,
                            SessionID = Client.Self.SessionID,
                            SerialNum = _serialNum++,
                            Size = new Vector3(2f, 2f, 2f) // HACK
                        },
                        WearableData = Array.Empty<AgentSetAppearancePacket.WearableDataBlock>(),
                        VisualParam = new AgentSetAppearancePacket.VisualParamBlock[appearance.VisualParam.Length]
                    };

                    for (var i = 0; i < appearance.VisualParam.Length; ++i)
                    {
                        set.VisualParam[i] = new AgentSetAppearancePacket.VisualParamBlock
                        {
                            ParamValue = appearance.VisualParam[i].ParamValue
                        };
                    }

                    set.ObjectData.TextureEntry = appearance.ObjectData.TextureEntry;

                    #endregion AvatarAppearance to AgentSetAppearance

                    // Detach everything we are currently wearing
                    Client.Appearance.AddAttachments(new List<InventoryItem>(), true);

                    // Send the new appearance packet
                    Client.Network.SendPacket(set);

                    return $"Cloned {targetName}";
                }
                else
                {
                    return $"Don't have an appearance cached for {targetName}";
                }
            }
            else
            {
                return $"Could not find {targetName}";
            }
        }

        private void AvatarAppearanceHandler(object sender, PacketReceivedEventArgs e)
        {
            AvatarAppearancePacket appearance = (AvatarAppearancePacket)e.Packet;

            lock (Appearances)
            {
                Appearances[appearance.Sender.ID] = appearance;
            }
        }
    }
}
