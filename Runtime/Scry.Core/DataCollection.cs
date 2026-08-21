using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry.Core
{
    public sealed class DataCollection
    {
        public Schema Schema { get; }
        public IReadOnlyList<DataRecord> Records { get; }

        public DataCollection(Schema schema, IEnumerable<DataRecord> records)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            Records = (records ?? throw new ArgumentNullException(nameof(records))).ToList();
        }
    }
}
