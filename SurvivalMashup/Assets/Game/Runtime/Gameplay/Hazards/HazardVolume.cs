using ToyChest.Framework.Objects;
using ToyChest.Systems.StatusEffects;
using UnityEngine;

namespace ToyChest.Gameplay.Hazards
{
    /// <summary>
    /// The thin Unity adapter that turns a trigger volume into an environmental effect: while a
    /// Gameplay Object stands inside, it applies an authored <see cref="StatusEffectDefinition"/>
    /// to that object's <see cref="StatusEffectSet"/>. What the effect does — poison damage over
    /// time, a healing aura, a spike wound — is entirely the authored status's concern; this
    /// component only bridges Unity's trigger callbacks to the existing Status Effect System.
    ///
    /// Applying a status is a caller concern, not a Gameplay Effect: a generic "apply status"
    /// effect would make the Gameplay Effect System depend on the Status Effect System, which
    /// already depends on it — the same cycle that kept equipping a caller concern in Review
    /// Group 6. So a hazard is an ordinary Gameplay Object (identity, tags, persistence) and this
    /// volume is its scene-facing behaviour, the environmental analogue of
    /// <c>PlayerInteractor</c>. It is not a trigger framework: no conditions, no responses, no
    /// registry — one authored status, applied on contact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HazardVolume : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The status applied to objects inside the volume (Poison, Warmth, Spikes).")]
        private StatusEffectDefinition _status;

        private void OnTriggerEnter(Collider other)
        {
            ApplyTo(other);
        }

        // Re-applies while the occupant remains, but only once the previous application has ended,
        // so a lingering visitor keeps taking the effect without refreshing (and re-publishing)
        // the status every physics frame.
        private void OnTriggerStay(Collider other)
        {
            ApplyTo(other);
        }

        private void ApplyTo(Collider other)
        {
            if (_status == null || other == null)
            {
                return;
            }

            var behaviour = other.GetComponentInParent<GameplayObjectBehaviour>();
            GameplayObject occupant = behaviour != null ? behaviour.Object : null;
            if (occupant == null || !occupant.IsActive || !occupant.TryGet(out StatusEffectSet statuses))
            {
                return;
            }

            if (!statuses.Has(_status.Id))
            {
                statuses.Apply(_status);
            }
        }
    }
}
