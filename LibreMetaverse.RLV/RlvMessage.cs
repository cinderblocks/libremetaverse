using System;

namespace LibreMetaverse.RLV
{
    internal sealed class RlvMessage
    {
        public string Behavior { get; }
        public Guid Sender { get; }
        public string SenderName { get; }
        public string Option { get; }
        public string Param { get; }

        public RlvMessage(string behavior, Guid sender, string senderName, string option, string param)
        {
            Behavior = behavior;
            Sender = sender;
            SenderName = senderName;
            Option = option;
            Param = param;
        }

        public override string ToString()
        {
            return $"{Behavior} from {SenderName} ({Sender})";
        }
    }
}
