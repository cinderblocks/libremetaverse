using System;
using System.Collections.Generic;
using System.Threading;

namespace OpenMetaverse.TestClient
{
    public class CloneProfileCommand : Command
    {
        Avatar.AvatarProperties Properties;
        Avatar.Interests Interests;
        List<UUID> Groups = new List<UUID>();
        bool ReceivedProperties = false;
        bool ReceivedInterests = false;
        bool ReceivedGroups = false;
        ManualResetEvent ReceivedProfileEvent = new ManualResetEvent(false);

        public CloneProfileCommand(TestClient testClient)
        {
            testClient.Avatars.AvatarInterestsReply += Avatars_AvatarInterestsReply;
            testClient.Avatars.AvatarPropertiesReply += Avatars_AvatarPropertiesReply;
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
            if (args.Length != 1)
                return Description;

            UUID targetID;
            ReceivedProperties = false;
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

            // Request all of the packets that make up an avatar profile
            Client.Avatars.RequestAvatarProperties(targetID);

            //Request all of the avatars pics
            Client.Avatars.RequestAvatarPicks(Client.Self.AgentID);
            Client.Avatars.RequestAvatarPicks(targetID);

            // Wait for all the packets to arrive
            ReceivedProfileEvent.Reset();
            ReceivedProfileEvent.WaitOne(TimeSpan.FromSeconds(5), false);

            // Check if everything showed up
            if (!ReceivedInterests || !ReceivedProperties || !ReceivedGroups)
                return "Failed to retrieve a complete profile for that UUID";

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

            return "Synchronized our profile to the profile of " + targetID;
        }                           

        void Groups_OnGroupJoined(object sender, GroupOperationEventArgs e)
        {
            Console.WriteLine(Client + (e.Success ? " joined " : " failed to join ") +
                e.GroupID);

            if (e.Success)
            {
                Console.WriteLine(Client + " setting " + e.GroupID +
                    " as the active group");
                Client.Groups.ActivateGroup(e.GroupID);
            }
        }

        void Avatars_PickInfoReply(object sender, PickInfoReplyEventArgs e)
        {
            Client.Self.PickInfoUpdate(e.PickID, e.Pick.TopPick, e.Pick.ParcelID, e.Pick.Name, e.Pick.PosGlobal, e.Pick.SnapshotID, e.Pick.Desc);
        }

        void Avatars_AvatarPicksReply(object sender, AvatarPicksReplyEventArgs e)
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

        void Avatars_AvatarGroupsReply(object sender, AvatarGroupsReplyEventArgs e)
        {
            lock (ReceivedProfileEvent)
            {
                foreach (AvatarGroup group in e.Groups)
                {
                    Groups.Add(group.GroupID);
                }

                ReceivedGroups = true;

                if (ReceivedInterests && ReceivedProperties && ReceivedGroups)
                    ReceivedProfileEvent.Set();
            }
        }

        void Avatars_AvatarPropertiesReply(object sender, AvatarPropertiesReplyEventArgs e)
        {
            lock (ReceivedProfileEvent)
            {
                Properties = e.Properties;
                ReceivedProperties = true;

                if (ReceivedInterests && ReceivedProperties && ReceivedGroups)
                    ReceivedProfileEvent.Set();
            }
        }

        void Avatars_AvatarInterestsReply(object sender, AvatarInterestsReplyEventArgs e)
        {
            lock (ReceivedProfileEvent)
            {
                Interests = e.Interests;
                ReceivedInterests = true;

                if (ReceivedInterests && ReceivedProperties && ReceivedGroups)
                    ReceivedProfileEvent.Set();
            }
        }        


    }
}
