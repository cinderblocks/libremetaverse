using System.Text;

// the Namespace used for all TestClient commands
namespace OpenMetaverse.TestClient
{
    /// <summary>
    /// Shows a list of friends
    /// </summary>
    public class FriendsCommand : Command
    {        
        /// <summary>
        /// Constructor for FriendsCommand class
        /// </summary>
        /// <param name="testClient">A reference to the TestClient object</param>
        public FriendsCommand(TestClient testClient)
        {
            // The name of the command
            Name = "friends";
            // A short description of the command with usage instructions
            Description = "List avatar friends. Usage: friends";
            Category = CommandCategory.Friends;
        }

        /// <summary>
        /// Get a list of current friends
        /// </summary>
        /// <param name="args">optional testClient command arguments</param>
        /// <param name="fromAgentID">The <seealso cref="OpenMetaverse.UUID"/> 
        /// of the agent making the request</param>
        /// <returns></returns>
        public override string Execute(string[] args, UUID fromAgentID)
        {
            // initialize a StringBuilder object used to return the results
            StringBuilder sb = new StringBuilder();

            // Only iterate the Friends dictionary if we actually have friends!
            if (Client.Friends.FriendList.Count > 0)
            {
                // iterate over the InternalDictionary using a delegate to populate
                // our StringBuilder output string
                sb.AppendFormat("has {0} friends:", Client.Friends.FriendList.Count).AppendLine();
                foreach (var friend in Client.Friends.FriendList)
                {
                    // append the name of the friend to our output
                    sb.AppendFormat("{0}, {1}", friend.Value.UUID, friend.Value.Name).AppendLine();
                };
            }
            else
            {
                // we have no friends :(
                sb.AppendLine("No Friends");   
            }

            // return the result
            return sb.ToString();
        }
    }
}
