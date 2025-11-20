using System;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient.Commands.Inventory
{
    public class BalanceCommand: Command
    {
        public BalanceCommand(TestClient testClient)
		{
			Name = "balance";
			Description = "Shows the amount of L$.";
            Category = CommandCategory.Other;
		}

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return ExecuteAsync(args, fromAgentID).GetAwaiter().GetResult();
        }

        public override async Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<BalanceEventArgs> handler = null;
            handler = (sender, e) =>
            {
                tcs.TrySetResult(true);
            };

            try
            {
                Client.Self.MoneyBalance += handler;
                Client.Self.RequestBalance();

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
                if (completed == tcs.Task)
                {
                    return Client + " has L$: " + Client.Self.Balance;
                }
                else
                {
                    return "Timeout waiting for balance reply";
                }
            }
            finally
            {
                Client.Self.MoneyBalance -= handler;
            }
        }
    }
}
