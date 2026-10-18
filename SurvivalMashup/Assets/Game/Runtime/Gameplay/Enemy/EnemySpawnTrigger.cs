using ToyChest.Gameplay.Objects;
using ToyChest.Gameplay.Scene;
using UnityEngine;

namespace ToyChest.Gameplay.Enemy
{
    /// <summary>
    /// A thin arena training-device: a visible trigger volume that, when the player steps into it, spawns a
    /// wave of enemies at authored spawn points so combat against increasing numbers can be tested without
    /// restarting the scene. It is the enemy counterpart to <c>LootPickup</c>/<c>HazardVolume</c> — a Unity
    /// contact bridged to one existing gameplay operation — <b>not</b> an encounter or spawn framework.
    ///
    /// <para>It reuses the canonical composition pipeline rather than inventing one: it caches the injected
    /// <see cref="GameplaySceneContext"/> as a scene participant, then each activation instantiates the enemy
    /// prefab (which carries its own <see cref="GameplayObjectSpawner"/>) and composes it through
    /// <see cref="GameplayObjectSpawner.Compose"/> — the exact path a scene-authored enemy uses at load,
    /// only driven at runtime. Re-arms when the player leaves, so one entry spawns one wave.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpawnTrigger : MonoBehaviour, IGameplaySceneParticipant
    {
        [SerializeField]
        [Tooltip("Unity tag identifying the player who arms the device.")]
        private string _playerTag = "Player";

        [SerializeField]
        [Tooltip("The enemy prefab spawned per point. Must carry a GameplayObjectSpawner + GameplayObjectBehaviour.")]
        private GameObject _enemyPrefab;

        [SerializeField]
        [Tooltip("Where spawned enemies appear. One enemy is spawned at each point per activation. " +
                 "Falls back to this device's own position when empty.")]
        private Transform[] _spawnPoints;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds after an activation before the device can fire again, so a lingering player does not spam waves.")]
        private float _rearmSeconds = 1f;

        private GameplaySceneContext _context;
        private bool _armed = true;
        private float _rearmTimer;

        /// <summary>Caches the composition services once the gameplay scene is ready. No spawning happens here.</summary>
        public void OnGameplaySceneReady(GameplaySceneContext context)
        {
            _context = context;
        }

        private void Update()
        {
            if (_armed)
            {
                return;
            }

            _rearmTimer -= Time.deltaTime;
            if (_rearmTimer <= 0f)
            {
                _armed = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_armed || other == null || !other.CompareTag(_playerTag))
            {
                return;
            }

            SpawnWave();
            _armed = false;
            _rearmTimer = _rearmSeconds;
        }

        private void SpawnWave()
        {
            if (_context == null)
            {
                Debug.LogWarning($"[EnemySpawnTrigger] '{name}' has no scene context yet; cannot spawn.", this);
                return;
            }

            if (_enemyPrefab == null)
            {
                Debug.LogError($"[EnemySpawnTrigger] '{name}' has no enemy prefab assigned.", this);
                return;
            }

            if (_spawnPoints == null || _spawnPoints.Length == 0)
            {
                SpawnOne(transform.position, transform.rotation);
                return;
            }

            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                Transform point = _spawnPoints[i];
                if (point != null)
                {
                    SpawnOne(point.position, point.rotation);
                }
            }
        }

        private void SpawnOne(Vector3 position, Quaternion rotation)
        {
            GameObject spawned = Instantiate(_enemyPrefab, position, rotation);

            // Compose through the enemy's own spawner using the same context a scene-authored enemy gets —
            // reuse, not a parallel spawn path. A runtime-instantiated object is not in the scene at load, so
            // the Boot layer never injects it; driving Compose here is what activates it.
            var spawner = spawned.GetComponent<GameplayObjectSpawner>();
            if (spawner != null)
            {
                spawner.Compose(_context);
            }
            else
            {
                Debug.LogError(
                    $"[EnemySpawnTrigger] spawned '{spawned.name}' has no GameplayObjectSpawner; it will not compose.", spawned);
            }
        }
    }
}
