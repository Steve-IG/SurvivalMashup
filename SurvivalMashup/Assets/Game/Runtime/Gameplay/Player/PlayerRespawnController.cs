using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Resources;
using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// The thin Unity adapter that respawns the player when its health is depleted. It owns no
    /// gameplay rules: it listens for the death signal the Resource System already publishes — the
    /// health <see cref="ResourceValue.Depleted"/> transition — and, on the next frame, delegates
    /// the state reset to the pure <see cref="PlayerRespawn"/> helper and moves the player to an
    /// authored spawn point. There is deliberately no respawn or checkpoint manager; a spawn point
    /// is a scene Transform and respawn is a couple of existing operations (see
    /// Docs/Systems/PLAYER.md).
    ///
    /// The respawn is deferred one frame on purpose: death fires from inside the health resource's
    /// change callback, which runs while a periodic damage status is mid-tick. Resetting state
    /// there would mutate the status set during its own iteration, so the controller records the
    /// request and services it from <see cref="Update"/> after the tick unwinds.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameplayObjectBehaviour))]
    public sealed class PlayerRespawnController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The behaviour whose composed object is respawned. Defaults to the sibling.")]
        private GameplayObjectBehaviour _behaviour;

        [SerializeField]
        [Tooltip("Where the player is placed on respawn. A plain scene Transform authored outside any hazard.")]
        private Transform _spawnPoint;

        [SerializeField]
        [Tooltip("The resource whose depletion counts as death.")]
        private string _healthResourceId = "resource.health";

        [SerializeField]
        [Tooltip("Optional controller to toggle while teleporting, so it does not overwrite the new position.")]
        private CharacterController _characterController;

        private ResourceValue _health;
        private bool _subscribed;
        private bool _respawnRequested;

        private void Awake()
        {
            if (_behaviour == null)
            {
                _behaviour = GetComponent<GameplayObjectBehaviour>();
            }

            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }
        }

        private void Update()
        {
            EnsureSubscribed();

            if (_respawnRequested)
            {
                _respawnRequested = false;
                Respawn();
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.Depleted -= OnHealthDepleted;
                _health = null;
                _subscribed = false;
            }
        }

        // The composed object is bound by the spawner shortly after scene load, and participant
        // callback order is not guaranteed, so subscribe lazily on the first frame the object is
        // live rather than assuming a ready order.
        private void EnsureSubscribed()
        {
            if (_subscribed)
            {
                return;
            }

            GameplayObject player = _behaviour != null ? _behaviour.Object : null;
            if (player == null || !player.IsActive || !player.TryGet(out ResourceSet resources))
            {
                return;
            }

            _health = resources.GetResource(new DefinitionId(_healthResourceId));
            if (_health == null)
            {
                return;
            }

            _health.Depleted += OnHealthDepleted;
            _subscribed = true;
        }

        private void OnHealthDepleted()
        {
            _respawnRequested = true;
        }

        private void Respawn()
        {
            GameplayObject player = _behaviour != null ? _behaviour.Object : null;
            if (player == null || !player.IsActive)
            {
                return;
            }

            PlayerRespawn.Restore(player);
            MoveToSpawnPoint();
        }

        private void MoveToSpawnPoint()
        {
            if (_spawnPoint == null)
            {
                return;
            }

            // A CharacterController writes the transform every frame, so disable it across the
            // teleport or the reset position is immediately overwritten.
            bool toggle = _characterController != null && _characterController.enabled;
            if (toggle)
            {
                _characterController.enabled = false;
            }

            transform.SetPositionAndRotation(_spawnPoint.position, _spawnPoint.rotation);

            if (toggle)
            {
                _characterController.enabled = true;
            }
        }
    }
}
