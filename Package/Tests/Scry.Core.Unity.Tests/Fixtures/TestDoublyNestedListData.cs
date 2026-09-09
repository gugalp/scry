using System.Collections.Generic;
using UnityEngine;

namespace Scry.Core.Unity.Tests.Fixtures
{
    public class TestDoublyNestedListData : ScriptableObject
    {
        public List<List<TestDropEntry>> doublyNested = new List<List<TestDropEntry>>();
    }
}
