using MessagePack;
using MessagePack.Formatters;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace OpenMetaverse.Formatters
{
    public class UUIDFormatter : IMessagePackFormatter<UUID>
    {
        public static readonly UUIDFormatter Instance = new UUIDFormatter();

        public void Serialize(ref MessagePackWriter writer, UUID value, MessagePackSerializerOptions options)
        {
            writer.Write(value.GetBytes());
        }

        public UUID Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var sequence = reader.ReadBytes();
            if (sequence == null)
            {
                throw new MessagePackSerializationException("Invalid UUID");
            }

            var bytes = sequence.Value.ToArray();
            return new UUID(bytes, 0);
        }
    }
}
