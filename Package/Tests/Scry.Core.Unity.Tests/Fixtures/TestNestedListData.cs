using System.Collections.Generic;
using UnityEngine;

namespace Scry.Core.Unity.Tests.Fixtures
{
    public class TestNestedListData : ScriptableObject
    {
        public List<TestOuterEntry> outer = new List<TestOuterEntry>();
    }
}
