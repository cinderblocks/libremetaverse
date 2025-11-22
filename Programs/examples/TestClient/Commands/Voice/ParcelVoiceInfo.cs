using System;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Voice
{
    public class ParcelVoiceInfoCommand : Command
    {
        // retained for compatibility but awaiting will use local TaskCompletionSource
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
                // keep a permanent subscription for backward compatibility if desired
                Client.VoiceManager.OnParcelVoiceInfo += Voice_OnParcelVoiceInfo;
                registered = true;
            }
            return true;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (!IsVoiceManagerRunning())
                return $"VoiceManager not running for {fromAgentID}";

            if (!Client.VoiceManager.RequestParcelVoiceInfo())
            {
                return "RequestParcelVoiceInfo failed. Not available for the current grid?";
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            LibreMetaverse.Voice.Vivox.VoiceManager.ParcelVoiceInfoCallback handler = null;
            handler = (regionName, localID, channelURI) =>
            {
                VoiceRegionName = regionName;
                VoiceLocalID = localID;
                VoiceChannelURI = channelURI;
                tcs.TrySetResult(true);
            };

            try
            {
                Client.VoiceManager.OnParcelVoiceInfo += handler;

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
                if (completed != tcs.Task)
                {
                    if (string.IsNullOrEmpty(VoiceRegionName) && VoiceLocalID == -1)
                        return $"Parcel Voice Info request for {Client.Self.Name} failed.";
                }
            }
            finally
            {
                Client.VoiceManager.OnParcelVoiceInfo -= handler;
            }

            return $"Parcel Voice Info request for {Client.Self.Name}: region name \"{VoiceRegionName}\", parcel local id {VoiceLocalID}, channel URI {VoiceChannelURI}";
        }

        // retained for compatibility with other code paths
        void Voice_OnParcelVoiceInfo(string regionName, int localID, string channelURI)
        {
            VoiceRegionName = regionName;
            VoiceLocalID = localID;
            VoiceChannelURI = channelURI;
        }
    }
}
