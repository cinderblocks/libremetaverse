using System;
using System.Text;
using OpenMetaverse;

namespace TestClient.Commands.Land
{
    public class ParcelDetailsCommand : Command
    {
        public ParcelDetailsCommand(TestClient testClient)
        {
            Name = "parceldetails";
            Description = "Displays parcel details from the ParcelTracker dictionary. Usage: parceldetails parcelID";
            Category = CommandCategory.Parcel;
        }

        public override string Execute(string[] args, UUID fromAgentID)
        {
            if (args.Length < 1)
                return "Usage: parceldetails parcelID (use parcelinfo to get ID)";

            int parcelID;
            Parcel parcel;

            // test argument that is is a valid integer, then verify we have that parcel data stored in the dictionary
            if (int.TryParse(args[0], out parcelID) && Client.Network.CurrentSim.Parcels.TryGetValue(parcelID, out parcel))
            {
                // this request will update the parcels dictionary
                Client.Parcels.RequestParcelProperties(Client.Network.CurrentSim, parcelID, 0);
                
                // Use reflection to dynamically get the fields from the Parcel struct
                Type t = parcel.GetType();
                global::System.Reflection.FieldInfo[] fields = t.GetFields(global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.Public);

                StringBuilder sb = new StringBuilder();
                foreach (global::System.Reflection.FieldInfo field in fields)
                {
                    sb.AppendFormat("{0} = {1}" + global::System.Environment.NewLine, field.Name, field.GetValue(parcel));
                }
                return sb.ToString();
            }
            else
            {
                return
                    $"Unable to find Parcel {args[0]} in Parcels Dictionary, Did you run parcelinfo to populate the dictionary first?";
            }
        }
    }
}
