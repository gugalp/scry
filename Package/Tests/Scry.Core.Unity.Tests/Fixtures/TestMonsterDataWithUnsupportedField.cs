using System.Collections.Generic;
using UnityEngine;

namespace Scry.Core.Unity.Tests.Fixtures
{
    public class TestMonsterDataWithUnsupportedField : ScriptableObject
    {
        public List<TestDropEntryWithUnsupportedField> dropTable = new List<TestDropEntryWithUnsupportedField>();
    }
}
