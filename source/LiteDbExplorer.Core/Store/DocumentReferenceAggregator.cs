using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;

namespace LiteDbExplorer.Core
{
    public class DocumentReferenceAggregator
    {
        private readonly IEnumerable<DocumentReference> _documents;
        private BsonArray _value;

        public DocumentReferenceAggregator(IEnumerable<DocumentReference> documents)
        {
            _documents = documents;
        }

        public BsonArray Value
        {
            get
            {
                if (_value == null)
                {
                    _value = new BsonArray(_documents.Select(a => a.LiteDocument));
                }
                return _value;
            }
        }

        public string Serialize(bool pretty = false, bool writeBinary = true)
        {
            // LiteDB 5.0 JsonSerializer.Serialize API only takes BsonValue, no pretty/writeBinary params
            return JsonSerializer.Serialize(Value);
        }

        public void Serialize(TextWriter writer, bool pretty = false, bool writeBinary = true)
        {
            // LiteDB 5.0 JsonSerializer.Serialize API for TextWriter
            JsonSerializer.Serialize(Value, writer);
        }
    }
}