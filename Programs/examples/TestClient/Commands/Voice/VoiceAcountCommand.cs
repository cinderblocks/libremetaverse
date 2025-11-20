using System;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Voice
{
    public class VoiceAccountCommand : Command
    {
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
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            if (!IsVoiceManagerRunning())
                return $"VoiceManager not running for {Client.Self.Name}";

            if (!Client.VoiceManager.RequestProvisionAccount())
            {
                return "RequestProvisionAccount failed. Not available for the current grid?";
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            LibreMetaverse.Voice.Vivox.VoiceManager.ProvisionAccountCallback handler = null;
            handler = (username, password) =>
            {
                VoiceAccount = username;
                VoicePassword = password;
                tcs.TrySetResult(true);
            };

            try
            {
                Client.VoiceManager.OnProvisionAccount += handler;

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
                if (completed != tcs.Task)
                {
                    if (string.IsNullOrEmpty(VoiceAccount) && string.IsNullOrEmpty(VoicePassword))
                        return $"Voice account information lookup for {Client.Self.Name} failed.";
                }
            }
            finally
            {
                Client.VoiceManager.OnProvisionAccount -= handler;
            }

            return $"Voice Account for {Client.Self.Name}: user \"{VoiceAccount}\", password \"{VoicePassword}\"";
        }

        void Voice_OnProvisionAccount(string username, string password)
        {
            VoiceAccount = username;
            VoicePassword = password;
        }
    }
}