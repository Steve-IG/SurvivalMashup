using System;
using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.Equipment;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Interactions;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Resources;
using ToyChest.Systems.StatusEffects;
using ToyChest.Systems.Tags;

namespace ToyChest.Gameplay.Objects
{
    /// <summary>
    /// The composition root for Gameplay Objects (Engine Principle 22): the one authoritative
    /// place where a <see cref="GameplayObjectDefinition"/> plus injected services become a
    /// fully wired <see cref="GameplayObject"/>. Constructs capabilities in dependency order —
    /// attributes before the resources that bind to them — and seals composition at creation.
    /// The factory composes; it does not activate. The spawner calls Activate after placement.
    /// Gameplay code consumes composed objects and never constructs capabilities directly.
    ///
    /// Spawning and loading share this one path (Engine Principle 26, Construction Before
    /// Participation): <see cref="Create"/> composes a fresh object with a new identity;
    /// <see cref="Reconstruct(DefinitionId, GameplayObjectId)"/> composes a persisted object with
    /// its saved identity. Both compose behind a closed event gate, so composition is event-quiet;
    /// the Save System restores authoritative state onto the reconstructed object before it is
    /// activated. See Docs/Systems/GAMEPLAY_FRAMEWORK.md.
    /// </summary>
    public sealed class GameplayObjectFactory : IGameplayObjectReconstructor
    {
        private readonly GameplayObjectContext _context;
        private readonly GameplayEffectRunner _effectRunner = new GameplayEffectRunner();

        /// <summary>Creates the factory with its injected service context.</summary>
        public GameplayObjectFactory(GameplayObjectContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Composes a new, not-yet-activated Gameplay Object from <paramref name="definition"/>
        /// with a fresh identity. Every composed object receives a tag container (state tags
        /// arrive at runtime even when no initial tags are declared), an ability set (grants
        /// arrive at runtime), and a status effect set (statuses are universal); attribute and
        /// resource capabilities are added only when the definition declares them.
        /// </summary>
        public GameplayObject Create(GameplayObjectDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return Compose(definition, GameplayObjectId.New());
        }

        /// <summary>
        /// Rebuilds a persisted object with its original <paramref name="id"/>, resolving
        /// <paramref name="definition"/> through the Data Registry. Composes exactly as
        /// <see cref="Create"/> does — event-quiet, composed but not activated — so the load path
        /// can restore authoritative state and then activate. This is the composition root's
        /// <see cref="IGameplayObjectReconstructor"/> contract the Save System builds on.
        /// </summary>
        public GameplayObject Reconstruct(DefinitionId definition, GameplayObjectId id)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException(
                    "Reconstruction requires the persisted GameplayObjectId. Parse it from the save payload.", nameof(id));
            }

            var resolved = _context.DataRegistry.Get<GameplayObjectDefinition>(definition);
            return Compose(resolved, id);
        }

        private GameplayObject Compose(GameplayObjectDefinition definition, GameplayObjectId id)
        {
            // One gate for the whole object: capabilities and the object publish through it, and
            // it stays closed until activation, so composition and restoration are event-quiet
            // (Engine Principle 26). The GameplayObject adopts this same gate.
            var gate = new GameplayObjectEventGate(_context.EventBus);
            var capabilities = new List<IGameplayCapability>();

            // Attributes first: resources may bind their maximums to them.
            AttributeSet attributes = null;
            if (definition.Attributes.Count > 0)
            {
                attributes = new AttributeSet(gate, id);
                foreach (AttributeDefinition attribute in definition.Attributes)
                {
                    attributes.AddAttribute(attribute);
                }

                capabilities.Add(attributes);
            }

            ResourceSet resources = null;
            if (definition.Resources.Count > 0)
            {
                resources = new ResourceSet(gate, attributes, id);
                foreach (ResourceDefinition resource in definition.Resources)
                {
                    resources.AddResource(resource);
                }

                capabilities.Add(resources);
            }

            var tags = new GameplayTagContainer(_context.TagTable, gate, id);
            foreach (TagDefinition initialTag in definition.InitialTags)
            {
                tags.AddTag(_context.TagTable.GetTag(initialTag.TagPath));
            }

            capabilities.Add(tags);

            // Abilities and statuses last: both are wired to the sibling capabilities above.
            var ownerTarget = new EffectTarget(resources, attributes, tags);

            var abilities = new AbilitySet(id, gate, _context.TagTable, _effectRunner, ownerTarget);
            foreach (AbilityDefinition ability in definition.Abilities)
            {
                abilities.Grant(ability);
            }

            capabilities.Add(abilities);

            var statuses = new StatusEffectSet(
                id,
                gate,
                _context.TagTable,
                _effectRunner,
                ownerTarget);
            capabilities.Add(statuses);

            if (definition.Inventory != null)
            {
                capabilities.Add(new InventorySet(id, gate, definition.Inventory.SlotCapacity));
            }

            if (definition.EquipmentSlots.Count > 0)
            {
                capabilities.Add(new EquipmentSet(
                    id,
                    gate,
                    _context.TagTable,
                    definition.EquipmentSlots,
                    ownerTarget,
                    abilities,
                    statuses));
            }

            if (definition.Interactions.Count > 0)
            {
                // Constructed before the grants so a misconfigured interaction fails composition first.
                var interactions = new InteractionSet(definition.Interactions);
                foreach (InteractionDefinition interaction in definition.Interactions)
                {
                    if (!abilities.Has(interaction.Ability.Id))
                    {
                        abilities.Grant(interaction.Ability);
                    }
                }

                capabilities.Add(interactions);
            }

            return new GameplayObject(id, definition.Id, gate, capabilities, _context.Registry);
        }
    }
}
