using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LibreMetaverse;
using LibreMetaverse.Packets;
#nullable enable

namespace TestClient.Commands.Appearance
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
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            string targetName = string.Empty;

            targetName = args.Aggregate(targetName, (current, t) => current + t + " ");
            targetName = targetName.TrimEnd();

            if (targetName.Length == 0)
                return "Usage: clone [name]";

            var tcs = new TaskCompletionSource<List<DirectoryManager.AgentSearchData>>();
            void OnDirPeople(object? sender, DirPeopleReplyEventArgs e) => tcs.TrySetResult(e.MatchedPeople);
            Client.Directory.DirPeopleReply += OnDirPeople;
            List<DirectoryManager.AgentSearchData> matches;
            try
            {
                Client.Directory.StartPeopleSearch(targetName, 0);
                using var cts = new global::System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                cts.Token.Register(() => tcs.TrySetResult(new List<DirectoryManager.AgentSearchData>()));
                matches = await tcs.Task;
            }
            finally
            {
                Client.Directory.DirPeopleReply -= OnDirPeople;
            }

            if (matches.Count > 0)
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

        private void AvatarAppearanceHandler(object? sender, PacketReceivedEventArgs e)
        {
            AvatarAppearancePacket appearance = (AvatarAppearancePacket)e.Packet;

            lock (Appearances)
            {
                Appearances[appearance.Sender.ID] = appearance;
            }
        }
    }
}
