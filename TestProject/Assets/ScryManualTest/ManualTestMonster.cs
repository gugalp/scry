using System.Collections.Generic;
using Scry.Core.Unity;
using UnityEngine;

namespace ScryManualTest
{
    [ScryCollection]
    public class ManualTestMonster : ScriptableObject
    {
        public string monsterName;
        public int level;
        public bool isBoss;
        public List<ManualDropEntry> dropTable = new List<ManualDropEntry>();
    }
}
