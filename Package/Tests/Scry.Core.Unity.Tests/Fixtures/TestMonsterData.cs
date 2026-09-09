using System.Collections.Generic;
using UnityEngine;

namespace Scry.Core.Unity.Tests.Fixtures
{
    public class TestMonsterData : ScriptableObject
    {
        public string monsterName;
        public List<TestDropEntry> dropTable = new List<TestDropEntry>();
    }
}
