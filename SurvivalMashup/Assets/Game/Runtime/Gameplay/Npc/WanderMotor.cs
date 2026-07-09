using System;
using UnityEngine;

namespace ToyChest.Gameplay.Npc
{
    /// <summary>
    /// Pure, deterministic autonomous-movement planner, separated from any MonoBehaviour so it is
    /// testable without a scene (Engine Principle 17, Favor Deterministic Behavior). It is the NPC
    /// counterpart to <c>LocomotionMotor</c>: given a spawn anchor, the NPC's current planar
    /// position, its movement speed, and elapsed time, it decides where the NPC should steer next.
    ///
    /// The behaviour is intentionally minimal — idle, then wander to a random point within a radius
    /// of the anchor, then idle again. It is <em>not</em> a behaviour tree, state-machine framework,
    /// or scheduler: it is a two-phase planner holding only its own tiny state. Randomness is
    /// injected (<see cref="System.Random"/>), so a given seed always produces the same walk, which
    /// is what the tests assert. It reads no engine clock; time is passed in by the caller.
    /// </summary>
    public sealed class WanderMotor
    {
        /// <summary>The two phases of the wander cycle.</summary>
        public enum Phase
        {
            /// <summary>Standing still, counting down until the next destination is chosen.</summary>
            Idle,

            /// <summary>Steering toward the current destination until it is reached.</summary>
            Moving
        }

        private readonly WanderSettings _settings;
        private readonly System.Random _random;

        private Phase _phase;
        private float _idleTimer;
        private Vector2 _destination;

        /// <summary>Creates a motor with a seeded RNG, so the walk is reproducible.</summary>
        public WanderMotor(WanderSettings settings, int seed)
            : this(settings, new System.Random(seed))
        {
        }

        /// <summary>Creates a motor with an injected RNG (tests supply a controlled source).</summary>
        public WanderMotor(WanderSettings settings, System.Random random)
        {
            _settings = settings;
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _phase = Phase.Idle;
            _idleTimer = NextIdleDuration();
        }

        /// <summary>The current phase; exposed so tests can assert idle/move transitions.</summary>
        public Phase CurrentPhase => _phase;

        /// <summary>The destination being steered toward while moving; exposed for tests.</summary>
        public Vector2 Destination => _destination;

        /// <summary>
        /// Advances the plan by <paramref name="deltaSeconds"/> and returns the desired planar
        /// velocity for this step. While idle it counts down and returns no motion; when the idle
        /// timer elapses it picks a new destination within <see cref="WanderSettings.Radius"/> of
        /// <paramref name="anchor"/> and begins moving; on arrival it returns to idle. Speed is
        /// injected (an authored Movement Speed attribute at the call site); a non-positive speed
        /// yields no motion.
        /// </summary>
        public WanderStep Tick(Vector2 anchor, Vector2 position, float speed, float deltaSeconds)
        {
            if (deltaSeconds < 0f)
            {
                deltaSeconds = 0f;
            }

            if (_phase == Phase.Idle)
            {
                _idleTimer -= deltaSeconds;
                if (_idleTimer > 0f)
                {
                    return WanderStep.Idle;
                }

                _destination = PickDestination(anchor);
                _phase = Phase.Moving;
            }

            Vector2 toDestination = _destination - position;
            if (toDestination.sqrMagnitude <= _settings.ArriveThreshold * _settings.ArriveThreshold)
            {
                _phase = Phase.Idle;
                _idleTimer = NextIdleDuration();
                return WanderStep.Idle;
            }

            if (speed <= 0f)
            {
                return WanderStep.Idle;
            }

            return new WanderStep(toDestination.normalized * speed, true);
        }

        private Vector2 PickDestination(Vector2 anchor)
        {
            // Uniform point inside the radius: sqrt on the radial term avoids clustering at the center.
            double angle = _random.NextDouble() * Math.PI * 2.0;
            double distance = _settings.Radius * Math.Sqrt(_random.NextDouble());
            return anchor + new Vector2(
                (float)(Math.Cos(angle) * distance),
                (float)(Math.Sin(angle) * distance));
        }

        private float NextIdleDuration()
        {
            float min = _settings.MinIdleSeconds;
            float max = _settings.MaxIdleSeconds;
            return min + (float)_random.NextDouble() * (max - min);
        }
    }
}
