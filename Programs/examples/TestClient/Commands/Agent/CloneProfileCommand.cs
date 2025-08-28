using System;
using System.Collections.Generic;
using System.Threading;

namespace OpenMetaverse.TestClient
{
    public class CloneProfileCommand : Command
    {
        private Avatar.AvatarProperties Properties;
        private Avatar.Interests Interests;
        private readonly List<UUID> Groups = new List<UUID>();
        private bool ReceivedProfile = false;
        private bool ReceivedInterests = false;
        private bool ReceivedGroups = false;
        private readonly ManualResetEvent ReceivedProfileEvent = new ManualResetEvent(false);

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
            if (args.Length != 1) { return Description; }

            UUID targetID;
            ReceivedInterests = false;
            ReceivedGroups = false;

            try
            {
                targetID = new UUID(args[0]);
            }
            catch (Exception)
            {
                return Description;
            }

            // Request all packets that make up an avatar profile
            Client.Avatars.RequestAgentProfile(targetID, (success, profile) =>
            {
                if (success)
                {
                    ReceivedProfile = true;
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
            }).Wait(TimeSpan.FromSeconds(5));

            //Request all avatars pics
            Client.Avatars.RequestAvatarPicks(Client.Self.AgentID);
            Client.Avatars.RequestAvatarPicks(targetID);

            // Wait for all the packets to arrive
            ReceivedProfileEvent.Reset();
            ReceivedProfileEvent.WaitOne(TimeSpan.FromSeconds(5), false);

            // Check if everything showed up
            if (!ReceivedProfile || !ReceivedInterests || !ReceivedGroups)
            {
                return "Failed to retrieve a complete profile for that UUID";
            }

            // Synchronize our profile
            Client.Self.UpdateInterests(Interests);
            Client.Self.UpdateProfile(Properties);

            // TODO: Leave all the groups we're currently a member of? This could
            // break TestClient connectivity that might be relying on group authentication

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
            lock (ReceivedProfileEvent)
            {
                foreach (AvatarGroup group in e.Groups)
                {
                    Groups.Add(group.GroupID);
                }

                ReceivedGroups = true;

                if (ReceivedInterests && ReceivedGroups)
                    ReceivedProfileEvent.Set();
            }
        }

        private void Avatars_AvatarInterestsReply(object sender, AvatarInterestsReplyEventArgs e)
        {
            lock (ReceivedProfileEvent)
            {
                Interests = e.Interests;
                ReceivedInterests = true;

                if (ReceivedInterests && ReceivedGroups)
                    ReceivedProfileEvent.Set();
            }
        }        


    }
}
