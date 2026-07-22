using System;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Resources;
using UnityEngine;

namespace ToyChest.Gameplay.Enemy
{
    /// <summary>
    /// The thin adapter that makes an ordinary enemy Gameplay Object serve as a combat training dummy: it
    /// keeps the object alive by restoring its health after damage, so a designer can punch it for minutes
    /// while judging timing, spacing, and impact readability.
    ///
    /// It is deliberately <b>not</b> a special combat path. The dummy is composed exactly like any other
    /// enemy — same Gameplay Object, same authored health resource, same <c>DamageEffect</c> arriving
    /// through the ordinary Ability → Gameplay Effect pipeline, same hit flash / hit animation / impact
    /// VFX / camera feedback — and this component only tops its health back up. There is no
    /// TrainingDummyManager, no bespoke damage handling, and no gameplay rule here. It never attacks or
    /// moves because it is <em>authored</em> inert (zero aggro range), not because of code in this class.
    ///
    /// Death is prevented primarily by data: the dummy's authored Maximum Health is large enough that a
    /// blow cannot deplete it. This restore is the second layer, keeping the resource pristine between
    /// punches (and recovering it if a future ability ever did deplete it).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameplayObjectBehaviour))]
    public sealed class TrainingDummy : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The behaviour whose composed object is this dummy. Defaults to the sibling.")]
        private GameplayObjectBehaviour _behaviour;

        [SerializeField]
        [Tooltip("Definition id of the health resource kept topped up.")]
        private string _healthResourceId = "resource.health";

        private DefinitionId _healthResource;
        private ResourceValue _health;
        private bool _restoring;

        /// <summary>Raised whenever the dummy absorbs damage — presentation may read this. Never gameplay.</summary>
        public event Action Damaged;

        /// <summary>Total blows absorbed since spawn. Debug/tooling readout only.</summary>
        public int HitsAbsorbed { get; private set; }

        private void Awake()
        {
            if (_behaviour == null)
            {
                _behaviour = GetComponent<GameplayObjectBehaviour>();
            }

            _healthResource = new DefinitionId(_healthResourceId);
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.ValueChanged -= OnHealthChanged;
                _health.Depleted -= OnDepleted;
                _health = null;
            }
        }

        private void Update()
        {
            EnsureBound();
        }

        private void EnsureBound()
        {
            if (_health != null || _behaviour == null || _behaviour.Object == null || !_behaviour.Object.IsActive)
            {
                return;
            }

            if (_behaviour.Object.TryGet(out ResourceSet resources))
            {
                _health = resources.GetResource(_healthResource);
                if (_health != null)
                {
                    _health.ValueChanged += OnHealthChanged;
                    _health.Depleted += OnDepleted;
                }
            }
        }

        private void OnHealthChanged(float previous, float current, float maximum)
        {
            // Ignore the change our own restore causes, so topping up cannot recurse.
            if (_restoring || current >= previous)
            {
                return;
            }

            HitsAbsorbed++;
            Damaged?.Invoke();
            RestoreToFull();
        }

        private void OnDepleted()
        {
            // Safety net: the dummy must never die even if something out-damages its authored health pool.
            RestoreToFull();
        }

        private void RestoreToFull()
        {
            if (_health == null || _restoring)
            {
                return;
            }

            _restoring = true;
            _health.Restore(_health.Maximum);
            _restoring = false;
        }
    }
}
