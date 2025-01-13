
using System;
using System.Threading;

namespace OpenMetaverse.TestClient
{
    public class VoiceAccountCommand : Command
    {
        private AutoResetEvent ProvisionEvent = new AutoResetEvent(false);
        private string VoiceAccount = null;
        private string VoicePassword = null;

        public VoiceAccountCommand(TestClient testClient)
        {
            Name = "voiceaccount";
            Description = "obtain voice account info. Usage: voiceaccount";
            Category = CommandCategory.Voice;

            Client = testClient;
        }

        private bool registered = false;

        private bool IsVoiceManagerRunning()
        {
            if (null == Client.VoiceManager) return false;

            if (!registered)
            {
                Client.VoiceManager.OnProvisionAccount += Voice_OnProvisionAccount;
                registered = true;
            }
            return true;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (!IsVoiceManagerRunning())
                return $"VoiceManager not running for {Client.Self.Name}";

            if (!Client.VoiceManager.RequestProvisionAccount())
            {
                return "RequestProvisionAccount failed. Not available for the current grid?";
            }
            ProvisionEvent.WaitOne(TimeSpan.FromSeconds(30), false);

            if (string.IsNullOrEmpty(VoiceAccount) && string.IsNullOrEmpty(VoicePassword))
            {
                return $"Voice account information lookup for {Client.Self.Name} failed.";
            }

            return $"Voice Account for {Client.Self.Name}: user \"{VoiceAccount}\", password \"{VoicePassword}\"";
        }

        void Voice_OnProvisionAccount(string username, string password)
        {
            VoiceAccount = username;
            VoicePassword = password;

            ProvisionEvent.Set();
        }
    }
}