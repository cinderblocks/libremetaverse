using System;
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
            global::System.Threading.AutoResetEvent waitBalance = new global::System.Threading.AutoResetEvent(false);

            void balance(object sender, BalanceEventArgs e)
            {
                waitBalance.Set();
            }

            Client.Self.MoneyBalance += balance;            
            Client.Self.RequestBalance();
            string result = "Timeout waiting for balance reply";
            if (waitBalance.WaitOne(TimeSpan.FromSeconds(10), false))
            {
                result = Client + " has L$: " + Client.Self.Balance;
            }
            Client.Self.MoneyBalance -= balance;
            return result;            
		}
    }
}
