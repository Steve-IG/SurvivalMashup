using System.Collections.Generic;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Scene;
using ToyChest.Systems.Interactions;
using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// The thin Unity adapter that discovers nearby interactables and routes a contextual
    /// interaction to the engine's <see cref="InteractionSystem"/>. It owns only the scene-facing
    /// discovery boundary the interaction docs describe: a proximity query producing a
    /// deterministically ordered candidate list. All validation, selection, and execution belong to
    /// the injected <see cref="InteractionSystem"/>; every gameplay consequence belongs to the
    /// interactable's ability. The input adapter calls <see cref="TryInteract"/>; this component
    /// never reads the input device.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameplayObjectBehaviour))]
    public sealed class PlayerInteractor : MonoBehaviour, IGameplaySceneParticipant
    {
        [SerializeField]
        [Tooltip("The behaviour whose composed object is the interactor. Defaults to the sibling.")]
        private GameplayObjectBehaviour _behaviour;

        [SerializeField]
        [Tooltip("Radius, in world units, within which interactables are considered.")]
        private float _interactionRadius = 2.5f;

        [SerializeField]
        [Tooltip("Physics layers searched for interactables.")]
        private LayerMask _interactableLayers = ~0;

        [SerializeField]
        [Tooltip("Maximum colliders inspected per interact query. Bounds allocation-free discovery.")]
        private int _maxCandidates = 16;

        private InteractionSystem _interactions;
        private Collider[] _overlap;
        private readonly List<Candidate> _candidates = new List<Candidate>();
        private readonly List<GameplayObject> _ordered = new List<GameplayObject>();

        private void Awake()
        {
            if (_behaviour == null)
            {
                _behaviour = GetComponent<GameplayObjectBehaviour>();
            }

            _overlap = new Collider[Mathf.Max(1, _maxCandidates)];
        }

        /// <summary>Caches the interaction orchestrator injected when the gameplay scene loads.</summary>
        public void OnGameplaySceneReady(GameplaySceneContext context)
        {
            _interactions = context.Interactions;
        }

        /// <summary>
        /// Discovers interactables in range and executes the highest-priority valid interaction,
        /// if any. Returns the interaction result, or <see cref="InteractionResult.NotInteractable"/>
        /// when nothing valid is in range. A no-op until an interactor object is bound and the scene
        /// context has been injected.
        /// </summary>
        public InteractionResult TryInteract()
        {
            GameplayObject interactor = _behaviour != null ? _behaviour.Object : null;
            if (_interactions == null || interactor == null || !interactor.IsActive)
            {
                return InteractionResult.NotInteractable;
            }

            IReadOnlyList<GameplayObject> candidates = DiscoverCandidates(interactor);
            if (!_interactions.TrySelectBest(interactor, candidates, out AvailableInteraction best))
            {
                return InteractionResult.NotInteractable;
            }

            return _interactions.TryInteract(interactor, best.Interactable, best.Interaction.Id);
        }

        // Proximity discovery: gather live, interactable objects (never the interactor itself) and
        // order them deterministically (nearest first, ties broken by stable id) so selection does
        // not depend on physics query order.
        private IReadOnlyList<GameplayObject> DiscoverCandidates(GameplayObject interactor)
        {
            _candidates.Clear();
            _ordered.Clear();

            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _interactionRadius, _overlap, _interactableLayers, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                var behaviour = _overlap[i].GetComponentInParent<GameplayObjectBehaviour>();
                GameplayObject candidate = behaviour != null ? behaviour.Object : null;
                if (candidate == null || candidate == interactor || !candidate.IsActive)
                {
                    continue;
                }

                if (AlreadyCollected(candidate))
                {
                    continue;
                }

                float sqrDistance = (_overlap[i].transform.position - transform.position).sqrMagnitude;
                _candidates.Add(new Candidate(candidate, sqrDistance));
            }

            _candidates.Sort(CompareCandidates);
            for (int i = 0; i < _candidates.Count; i++)
            {
                _ordered.Add(_candidates[i].Object);
            }

            return _ordered;
        }

        private bool AlreadyCollected(GameplayObject candidate)
        {
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (_candidates[i].Object == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareCandidates(Candidate a, Candidate b)
        {
            return Compare(a.SqrDistance, a.Object.Id.ToString(), b.SqrDistance, b.Object.Id.ToString());
        }

        /// <summary>
        /// Deterministic candidate ordering: nearest first, ties broken by ordinal id. Exposed for
        /// edit-mode tests so the determinism contract is verified without a physics scene.
        /// </summary>
        public static int Compare(float aSqrDistance, string aId, float bSqrDistance, string bId)
        {
            int byDistance = aSqrDistance.CompareTo(bSqrDistance);
            return byDistance != 0 ? byDistance : string.CompareOrdinal(aId, bId);
        }

        private readonly struct Candidate
        {
            public Candidate(GameplayObject gameplayObject, float sqrDistance)
            {
                Object = gameplayObject;
                SqrDistance = sqrDistance;
            }

            public GameplayObject Object { get; }

            public float SqrDistance { get; }
        }
    }
}
