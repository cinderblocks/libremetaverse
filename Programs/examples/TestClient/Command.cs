using System;
using System.Threading.Tasks;
using OpenMetaverse;

namespace TestClient
{
    public enum CommandCategory
    {
        Parcel,
        Appearance,
        Movement,
        Simulator,
        Communication,
        Inventory,
        Objects,
        Voice,
        TestClient,
        Friends,
        Groups,
        Other,
        Unknown,
        Search
    }

    public abstract class Command : IComparable
    {
		public string Name;
		public string Description;
        public CommandCategory Category;

		public TestClient Client;

		// Existing synchronous API that most commands implement
		public abstract string Execute(string[] args, UUID fromAgentID);

		// Async-friendly API. By default this will run the synchronous Execute on the threadpool
		// so existing commands continue to work. Commands that perform IO should override this
		// to provide a true async implementation.
		public virtual Task<string> ExecuteAsync(string[] args, UUID fromAgentID)
		{
			return Task.Run(() => Execute(args, fromAgentID));
		}

		/// <summary>
		/// When set to true, think will be called.
		/// </summary>
		public bool Active;

		/// <summary>
		/// Called twice per second, when Command.Active is set to true.
		/// </summary>
		public virtual void Think()
		{
		}

        public int CompareTo(object obj)
        {
            if (obj is Command c2)
            {
                return Category.CompareTo(c2.Category);
            }
            else
                throw new ArgumentException("Object is not of type Command.");
        }

    }
}
