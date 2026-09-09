using System.Collections.Generic;
using Scry.Core.Unity.Config;
using UnityEngine;

namespace Scry.Core.Unity
{
    [CreateAssetMenu(menuName = "Scry/Config", fileName = "ScryConfig")]
    public sealed class ScryConfig : ScriptableObject
    {
        [SerializeField] private List<TrackedCollection> trackedCollections = new List<TrackedCollection>();

        public IReadOnlyList<TrackedCollection> TrackedCollections => trackedCollections;
    }
}
