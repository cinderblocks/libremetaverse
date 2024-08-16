namespace OpenMetaverse.TestClient
{
    public class MD5Command : Command
    {
        public MD5Command(TestClient testClient)
        {
            Name = "md5";
            Description = "Creates an MD5 hash from a given password. Usage: md5 [password]";
            Category = CommandCategory.Other;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            return args.Length == 1 ? Utils.MD5(args[0]) : "Usage: md5 [password]";
        }
    }
}
