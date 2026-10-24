using System;
using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Objects;
using ToyChest.Gameplay.Scene;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Resources;
using UnityEngine;

namespace ToyChest.Gameplay.Enemy
{
    /// <summary>The state of an <see cref="EliteEncounter"/>.</summary>
    public enum EliteEncounterState
    {
        /// <summary>Waiting for the player to step onto the challenge pad.</summary>
        Armed,

        /// <summary>The elite (and its escorts) are in the fight.</summary>
        Active,

        /// <summary>The elite has fallen; the pad re-arms (for a rematch) once the player steps off it.</summary>
        Victorious,
    }

    /// <summary>
    /// The thin scene adapter for one elite encounter: a challenge pad that, when the player steps onto it, composes the
    /// elite and its escorts through the canonical <see cref="GameplayObjectSpawner.Compose"/> path (as
    /// <see cref="EnemySpawnTrigger"/> does), calls reinforcements once when the elite is worn down, and, the moment the
    /// elite dies, completes the encounter. It is a bridge from Unity contact and enemy death to existing gameplay
    /// operations, <b>not</b> an encounter or boss framework.
    ///
    /// <para><b>Completion is authoritative gameplay state, not adapter state.</b> The pad's sibling Gameplay Object (the
    /// encounter object) owns a one-shot charge Resource and a reward ability whose cost is that charge — the Supply
    /// Crate's loot pattern. Victory activates the reward ability with the player as its target, so the reward (an item,
    /// via <c>AddItemEffect</c>) is granted exactly once by the Ability System's own cost check, and the spent charge is
    /// what the Save System persists as "this elite has been defeated". A rematch plays the full encounter again, but
    /// the reward ability is refused (CannotAfford), so it can never pay out twice.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameplayObjectBehaviour))]
    public sealed class EliteEncounter : MonoBehaviour, IGameplaySceneParticipant
    {
        [SerializeField]
        [Tooltip("Unity tag identifying the player who triggers the encounter and receives the reward.")]
        private string _playerTag = "Player";

        [SerializeField]
        [Tooltip("The encounter object's behaviour (owns the defeated charge and the reward ability). Defaults to the sibling.")]
        private GameplayObjectBehaviour _behaviour;

        [Header("Elite")]
        [SerializeField]
        [Tooltip("The elite prefab. Must carry a GameplayObjectSpawner, GameplayObjectBehaviour, and EnemyCombatant.")]
        private GameObject _elitePrefab;

        [SerializeField]
        [Tooltip("Where the elite appears. Falls back to this pad.")]
        private Transform _eliteSpawnPoint;

        [Header("Escorts")]
        [SerializeField]
        [Tooltip("The prefab escorting the elite (an ordinary Grunt).")]
        private GameObject _escortPrefab;

        [SerializeField]
        [Tooltip("One escort spawns at each point when the encounter starts.")]
        private Transform[] _escortSpawnPoints = new Transform[0];

        [SerializeField]
        [Tooltip("One more escort spawns at each point, once, when the elite's health first falls to the threshold below.")]
        private Transform[] _reinforcementSpawnPoints = new Transform[0];

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Fraction of the elite's maximum health at which reinforcements arrive. 0 = never.")]
        private float _reinforcementHealthFraction = 0.5f;

        [Header("Completion")]
        [SerializeField]
        [Tooltip("Definition id of the encounter object's reward ability (Provided target = the player; cost = the charge).")]
        private string _rewardAbilityId = "ability.elite_reward_brute";

        [SerializeField]
        [Tooltip("Definition id of the encounter object's one-shot charge. Spent = the elite has been defeated.")]
        private string _chargeResourceId = "resource.encounter_charge";

        [SerializeField]
        [Tooltip("Display name of the reward, for the completion message.")]
        private string _rewardDisplayName = "Brute's Horn";

        private static readonly DefinitionId Health = new DefinitionId("resource.health");

        private GameplaySceneContext _context;
        private EliteEncounterState _state = EliteEncounterState.Armed;
        private EnemyCombatant _elite;
        private ResourceValue _eliteHealth;
        private bool _reinforced;
        private bool _playerOnPad;
        private readonly List<EnemyCombatant> _escorts = new List<EnemyCombatant>();

        /// <summary>Raised when the player starts the encounter and the elite appears.</summary>
        public event Action<EnemyCombatant> Started;

        /// <summary>Raised once when reinforcements arrive.</summary>
        public event Action Reinforced;

        /// <summary>
        /// Raised once per fight, the moment the elite dies, with the reward ability's result: Activated when the reward
        /// was granted (the first victory); CannotAfford on a rematch after the charge is spent.
        /// </summary>
        public event Action<AbilityActivationResult> Completed;

        /// <summary>The encounter's current state.</summary>
        public EliteEncounterState State => _state;

        /// <summary>The elite in the current fight, or null.</summary>
        public EnemyCombatant Elite => _elite;

        /// <summary>Live escorts and reinforcements spawned by this encounter (dead ones are skipped).</summary>
        public IReadOnlyList<EnemyCombatant> Escorts => _escorts;

        /// <summary>Whether this encounter's elite has ever been defeated (its charge is spent — persists through save).</summary>
        public bool HasBeenDefeated => TryGetCharge(out ResourceValue charge) && charge.Current <= 0f;

        /// <summary>The reward's display name.</summary>
        public string RewardDisplayName => _rewardDisplayName;

        /// <summary>The result of the most recent completion (meaningful once <see cref="Completed"/> has fired).</summary>
        public AbilityActivationResult LastResult { get; private set; }

        /// <summary>The elite's current and maximum health while it lives; false otherwise.</summary>
        public bool TryGetEliteHealth(out float current, out float maximum)
        {
            if (_eliteHealth != null && _elite != null && !_elite.IsDead)
            {
                current = _eliteHealth.Current;
                maximum = _eliteHealth.Maximum;
                return true;
            }

            current = maximum = 0f;
            return false;
        }

        /// <summary>Caches the composition services once the gameplay scene is ready. Nothing spawns here.</summary>
        public void OnGameplaySceneReady(GameplaySceneContext context)
        {
            _context = context;
        }

        private void Awake()
        {
            if (_behaviour == null)
            {
                _behaviour = GetComponent<GameplayObjectBehaviour>();
            }
        }

        private void OnDisable()
        {
            UnwatchElite();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || !other.CompareTag(_playerTag))
            {
                return;
            }

            _playerOnPad = true;
            if (_state == EliteEncounterState.Armed)
            {
                Begin();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null || !other.CompareTag(_playerTag))
            {
                return;
            }

            _playerOnPad = false;

            // A rematch needs a deliberate return to the pad: stepping off after a victory re-arms it.
            if (_state == EliteEncounterState.Victorious)
            {
                _state = EliteEncounterState.Armed;
            }
        }

        private void Update()
        {
            if (_state != EliteEncounterState.Active || _reinforced || _eliteHealth == null || _reinforcementHealthFraction <= 0f)
            {
                return;
            }

            if (_eliteHealth.Current > 0f && _eliteHealth.Current <= _eliteHealth.Maximum * _reinforcementHealthFraction)
            {
                _reinforced = true;
                SpawnEscorts(_reinforcementSpawnPoints);
                Reinforced?.Invoke();
            }
        }

        private void Begin()
        {
            if (_context == null)
            {
                Debug.LogWarning($"[EliteEncounter] '{name}' has no scene context yet; cannot start.", this);
                return;
            }

            if (_elitePrefab == null)
            {
                Debug.LogError($"[EliteEncounter] '{name}' has no elite prefab assigned.", this);
                return;
            }

            Transform point = _eliteSpawnPoint != null ? _eliteSpawnPoint : transform;
            GameObject spawned = Spawn(_elitePrefab, point);
            _elite = spawned != null ? spawned.GetComponent<EnemyCombatant>() : null;
            if (_elite == null || _elite.ComposedObject == null)
            {
                Debug.LogError($"[EliteEncounter] '{name}': the elite prefab did not compose an EnemyCombatant.", this);
                return;
            }

            _escorts.Clear();
            _reinforced = false;
            SpawnEscorts(_escortSpawnPoints);

            _elite.Died += OnEliteDied;
            if (_elite.ComposedObject.TryGet(out ResourceSet resources))
            {
                _eliteHealth = resources.GetResource(Health);
            }

            _state = EliteEncounterState.Active;
            Started?.Invoke(_elite);
        }

        private void SpawnEscorts(Transform[] points)
        {
            if (_escortPrefab == null || points == null)
            {
                return;
            }

            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null)
                {
                    continue;
                }

                GameObject escort = Spawn(_escortPrefab, points[i]);
                var combatant = escort != null ? escort.GetComponent<EnemyCombatant>() : null;
                if (combatant != null)
                {
                    _escorts.Add(combatant);
                }
            }
        }

        // The same composition a scene-authored enemy gets: instantiate, then drive its own spawner with the scene context.
        private GameObject Spawn(GameObject prefab, Transform point)
        {
            GameObject spawned = Instantiate(prefab, point.position, point.rotation);
            var spawner = spawned.GetComponent<GameplayObjectSpawner>();
            if (spawner == null)
            {
                Debug.LogError($"[EliteEncounter] spawned '{spawned.name}' has no GameplayObjectSpawner; it will not compose.", spawned);
                return spawned;
            }

            spawner.Compose(_context);
            return spawned;
        }

        // EnemyCombatant raises Died exactly once per enemy; the state check makes completion once per fight regardless.
        private void OnEliteDied()
        {
            if (_state != EliteEncounterState.Active)
            {
                return;
            }

            UnwatchElite();

            // Victorious until the player steps off the pad (so a win on the pad never instantly restarts the fight);
            // already off it, the pad is armed for a rematch straight away.
            _state = _playerOnPad ? EliteEncounterState.Victorious : EliteEncounterState.Armed;

            LastResult = GrantReward();
            Completed?.Invoke(LastResult);
        }

        private AbilityActivationResult GrantReward()
        {
            GameplayObject encounter = _behaviour != null ? _behaviour.Object : null;
            GameplayObject player = FindPlayerObject();
            if (encounter == null || player == null || !encounter.TryGet(out AbilitySet abilities))
            {
                return AbilityActivationResult.InvalidTarget;
            }

            return abilities.TryActivate(new DefinitionId(_rewardAbilityId), EffectTarget.From(player));
        }

        private GameplayObject FindPlayerObject()
        {
            GameObject found = GameObject.FindGameObjectWithTag(_playerTag);
            var behaviour = found != null ? found.GetComponentInParent<GameplayObjectBehaviour>() : null;
            return behaviour != null && behaviour.Object != null && behaviour.Object.IsActive ? behaviour.Object : null;
        }

        private bool TryGetCharge(out ResourceValue charge)
        {
            charge = null;
            GameplayObject encounter = _behaviour != null ? _behaviour.Object : null;
            if (encounter != null && encounter.TryGet(out ResourceSet resources))
            {
                charge = resources.GetResource(new DefinitionId(_chargeResourceId));
            }

            return charge != null;
        }

        private void UnwatchElite()
        {
            if (_elite != null)
            {
                _elite.Died -= OnEliteDied;
            }

            _eliteHealth = null;
        }
    }
}
