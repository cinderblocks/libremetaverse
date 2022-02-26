using System;
using System.Threading;

namespace OpenMetaverse.TestClient
{
    public class ParcelVoiceInfoCommand : Command
    {
        private AutoResetEvent ParcelVoiceInfoEvent = new AutoResetEvent(false);
        private string VoiceRegionName = null;
        private int VoiceLocalID = -1;
        private string VoiceChannelURI = null;

        public ParcelVoiceInfoCommand(TestClient testClient)
        {
            Name = "voiceparcel";
            Description = "obtain parcel voice info. Usage: voiceparcel";
            Category = CommandCategory.Other;

            Client = testClient;
        }

        private bool registered = false;

        private bool IsVoiceManagerRunning() 
        {
            if (null == Client.VoiceManager) return false;
            
            if (!registered)
            {
                Client.VoiceManager.OnParcelVoiceInfo += Voice_OnParcelVoiceInfo;
                registered = true;
            }
            return true;           
        }


        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (!IsVoiceManagerRunning()) 
                return $"VoiceManager not running for {fromAgentID}";

            if (!Client.VoiceManager.RequestParcelVoiceInfo()) 
            {
                return "RequestParcelVoiceInfo failed. Not available for the current grid?";
            }
            ParcelVoiceInfoEvent.WaitOne(30 * 1000, false);

            if (string.IsNullOrEmpty(VoiceRegionName) && -1 == VoiceLocalID)
            {
                return $"Parcel Voice Info request for {Client.Self.Name} failed.";
            }

            return
                $"Parcel Voice Info request for {Client.Self.Name}: region name \"{VoiceRegionName}\", parcel local id {VoiceLocalID}, channel URI {VoiceChannelURI}";
        }

        void Voice_OnParcelVoiceInfo(string regionName, int localID, string channelURI)
        {
            VoiceRegionName = regionName;
            VoiceLocalID = localID;
            VoiceChannelURI = channelURI;

            ParcelVoiceInfoEvent.Set();
        }
    }
}
