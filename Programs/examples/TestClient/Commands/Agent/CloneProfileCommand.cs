using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Agent
{
    public class CloneProfileCommand : Command
    {
        private Avatar.AvatarProperties Properties;
        private Avatar.Interests Interests;
        private readonly List<UUID> Groups = new List<UUID>();
        // retain existing global event subscriptions for picks and pick info

        public CloneProfileCommand(TestClient testClient)
        {
            testClient.Avatars.AvatarInterestsReply += Avatars_AvatarInterestsReply;
            testClient.Avatars.AvatarGroupsReply += Avatars_AvatarGroupsReply;
            testClient.Groups.GroupJoinedReply += Groups_OnGroupJoined;
            testClient.Avatars.AvatarPicksReply += Avatars_AvatarPicksReply;
            testClient.Avatars.PickInfoReply += Avatars_PickInfoReply;

            Name = "cloneprofile";
            Description = "Clones another avatars profile as closely as possible. WARNING: This command will " +
                "destroy your existing profile! Usage: cloneprofile [targetuuid]";
            Category = CommandCategory.Other;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (args.Length != 1) return Description;

            if (!UUID.TryParse(args[0], out var targetID)) return Description;

            Groups.Clear();

            var profileTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Request the agent profile with callback
            _ = Client.Avatars.RequestAgentProfile(targetID, (success, profile) =>
            {
                if (success)
                {
                    Properties = new Avatar.AvatarProperties
                    {
                        AboutText = profile.SecondLifeAboutText,
                        FirstLifeText = profile.FirstLifeAboutText,
                        ProfileImage = profile.SecondLifeImageID,
                        FirstLifeImage = profile.FirstLifeImageID,
                        ProfileURL = profile.HomePage,
                        AllowPublish = true,
                        MaturePublish = profile.IsMatureProfile
                    };
                }

                profileTcs.TrySetResult(success);
            });

            var profileCompleted = await Task.WhenAny(profileTcs.Task, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
            if (profileCompleted != profileTcs.Task || !profileTcs.Task.Result)
                return "Failed to retrieve profile";

            // Prepare TCS for interests and groups
            var interestsTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var groupsTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<AvatarInterestsReplyEventArgs> interestsHandler = null;
            EventHandler<AvatarGroupsReplyEventArgs> groupsHandler = null;

            interestsHandler = (s, e) =>
            {
                if (e.AvatarID == targetID)
                {
                    Interests = e.Interests;
                    interestsTcs.TrySetResult(true);
                }
            };

            groupsHandler = (s, e) =>
            {
                if (e.AvatarID == targetID)
                {
                    foreach (var g in e.Groups)
                        Groups.Add(g.GroupID);
                    groupsTcs.TrySetResult(true);
                }
            };

            try
            {
                Client.Avatars.AvatarInterestsReply += interestsHandler;
                Client.Avatars.AvatarGroupsReply += groupsHandler;

                // Request avatar picks as original code did (may trigger replies)
                Client.Avatars.RequestAvatarPicks(Client.Self.AgentID);
                Client.Avatars.RequestAvatarPicks(targetID);

                // Wait for both interests and groups, with timeout
                var combined = Task.WhenAll(interestsTcs.Task, groupsTcs.Task);
                var completedBoth = await Task.WhenAny(combined, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
                if (completedBoth != combined)
                    return "Failed to retrieve a complete profile for that UUID";
            }
            finally
            {
                Client.Avatars.AvatarInterestsReply -= interestsHandler;
                Client.Avatars.AvatarGroupsReply -= groupsHandler;
            }

            // Synchronize our profile
            Client.Self.UpdateInterests(Interests);
            Client.Self.UpdateProfile(Properties);

            // Attempt to join all the groups
            foreach (UUID groupID in Groups)
            {
                Client.Groups.RequestJoinGroup(groupID);
            }

            return $"Synchronized our profile to the profile of {targetID}";
        }

        private void Groups_OnGroupJoined(object sender, GroupOperationEventArgs e)
        {
            Console.WriteLine(Client + (e.Success ? " joined " : " failed to join ") +
                e.GroupID);

            if (e.Success)
            {
                Console.WriteLine($"{Client} setting {e.GroupID} as the active group");
                Client.Groups.ActivateGroup(e.GroupID);
            }
        }

        private void Avatars_PickInfoReply(object sender, PickInfoReplyEventArgs e)
        {
            Client.Self.PickInfoUpdate(e.PickID, e.Pick.TopPick, e.Pick.ParcelID, e.Pick.Name, e.Pick.PosGlobal,
                e.Pick.SnapshotID, e.Pick.Desc);
        }

        private void Avatars_AvatarPicksReply(object sender, AvatarPicksReplyEventArgs e)
        {
            foreach (KeyValuePair<UUID, string> kvp in e.Picks)
            {
                if (e.AvatarID == Client.Self.AgentID)
                {
                    Client.Self.PickDelete(kvp.Key);
                }
                else
                {
                    Client.Avatars.RequestPickInfo(e.AvatarID, kvp.Key);
                }
            }
        }

        private void Avatars_AvatarGroupsReply(object sender, AvatarGroupsReplyEventArgs e)
        {
            // retained for potential other users of this class
        }

        private void Avatars_AvatarInterestsReply(object sender, AvatarInterestsReplyEventArgs e)
        {
            // retained for potential other users of this class
        }
    }
}
