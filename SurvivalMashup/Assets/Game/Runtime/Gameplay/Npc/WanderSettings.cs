using System;
using UnityEngine;

namespace ToyChest.Gameplay.Npc
{
    /// <summary>
    /// Authored tuning for autonomous wandering, kept as a small serializable value so behaviour is
    /// configured as data on the scene object (mirroring how movement <em>speed</em> is an authored
    /// attribute). It carries no gameplay rules and no framework concept — it is the steering knobs a
    /// designer sets per NPC: how far it roams from its spawn anchor, how close counts as "arrived",
    /// and how long it pauses between moves. The deterministic <see cref="WanderMotor"/> consumes it.
    /// </summary>
    [Serializable]
    public struct WanderSettings
    {
        [SerializeField]
        [Min(0f)]
        [Tooltip("Maximum distance, in world units, a wander destination may be picked from the spawn anchor.")]
        private float _radius;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Distance to a destination, in world units, at which the NPC is considered to have arrived.")]
        private float _arriveThreshold;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Minimum idle pause, in seconds, before choosing the next wander destination.")]
        private float _minIdleSeconds;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Maximum idle pause, in seconds, before choosing the next wander destination.")]
        private float _maxIdleSeconds;

        /// <summary>Builds settings from explicit values (used by tests and composition).</summary>
        public WanderSettings(float radius, float arriveThreshold, float minIdleSeconds, float maxIdleSeconds)
        {
            _radius = radius;
            _arriveThreshold = arriveThreshold;
            _minIdleSeconds = minIdleSeconds;
            _maxIdleSeconds = maxIdleSeconds;
        }

        /// <summary>Maximum roam distance from the spawn anchor, never negative.</summary>
        public float Radius => Mathf.Max(0f, _radius);

        /// <summary>Arrival distance, kept strictly positive so a destination is always reachable.</summary>
        public float ArriveThreshold => Mathf.Max(0.01f, _arriveThreshold);

        /// <summary>Minimum idle pause between moves, never negative.</summary>
        public float MinIdleSeconds => Mathf.Max(0f, _minIdleSeconds);

        /// <summary>Maximum idle pause between moves, never less than the minimum.</summary>
        public float MaxIdleSeconds => Mathf.Max(MinIdleSeconds, _maxIdleSeconds);

        /// <summary>Reasonable defaults for a village-scale wanderer.</summary>
        public static WanderSettings Default => new WanderSettings(6f, 0.35f, 1.5f, 4f);
    }
}
