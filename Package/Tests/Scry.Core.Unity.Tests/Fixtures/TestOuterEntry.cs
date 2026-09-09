using System;
using System.Collections.Generic;

namespace Scry.Core.Unity.Tests.Fixtures
{
    [Serializable]
    public class TestOuterEntry
    {
        public List<TestDropEntry> inner;
    }
}
