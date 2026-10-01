using LiteDB;

namespace LiteDbExplorer.Core
{
    public static class JsonSerializerExtension
    {

        public static string SerializeDecoded(this BsonValue bsonValue, bool pretty = false)
        {
            // LiteDB 5.0 - JsonSerializer.Serialize only takes BsonValue
            var json = JsonSerializer.Serialize(bsonValue);

            return EncodingExtensions.DecodeEncodedNonAsciiCharacters(json);
        }
    }
}