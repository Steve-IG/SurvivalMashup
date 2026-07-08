using System;
using UnityEngine;

namespace ToyChest.Systems.Abilities
{
    /// <summary>
    /// Authoring configuration for one resource cost of an ability (Fireball: 20 mana).
    /// Fully generic: the Ability System never distinguishes Mana from Ammo, Energy, or any
    /// future resource — a cost is a resource id and an amount. Costs are all-or-nothing:
    /// activation fails unless every cost is payable in full.
    /// </summary>
    [Serializable]
    public struct ResourceCostConfig
    {
        [SerializeField]
        [Tooltip("The resource consumed, e.g. resource.mana.")]
        private string _resourceId;

        [SerializeField]
        [Min(0f)]
        private float _amount;

        /// <summary>The resource this cost consumes.</summary>
        public readonly string ResourceId => _resourceId;

        /// <summary>The amount consumed on activation.</summary>
        public readonly float Amount => _amount;

        /// <summary>Creates a config (tests and tooling; designers author through the inspector).</summary>
        public ResourceCostConfig(string resourceId, float amount)
        {
            _resourceId = resourceId;
            _amount = amount;
        }
    }
}
