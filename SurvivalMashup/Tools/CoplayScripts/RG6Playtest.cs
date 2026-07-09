using System.Collections.Generic;
using System.Text;
using ToyChest.Boot;
using ToyChest.Core.Logging;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Objects;
using ToyChest.Gameplay.Player;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.Equipment;
using ToyChest.Systems.Interactions;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Save;
using ToyChest.Systems.StatusEffects;
using ToyChest.Systems.Tags;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Review Group 6 "manual gameplay verification" as a deterministic, human-readable trace (the
/// same style as the RG4 playtest). Boots the real engine over the authored definitions, loots the
/// Equipment Cache, equips through the player's equip caller, and prints every contribution the
/// equipment activated through the systems that own them, then round-trips the whole loadout
/// through the Save System and prints the restored state. Logs one report block to the console.
/// </summary>
public static class RG6Playtest
{
    private const string DefinitionsFolder = "Assets/Game/Content/Definitions";

    private const string PlayerId = "object.player";
    private const string CacheId = "object.equipment_cache";
    private const string LootInteractionId = "interaction.loot_equipment";
    private const string BootsItemId = "item.boots_of_swiftness";
    private const string CharmItemId = "item.lucky_charm";
    private const string BootsSlotId = "slot.boots";
    private const string CharmSlotId = "slot.charm";
    private const string MoveSpeedId = "attribute.movement_speed";
    private const string MaxHealthId = "attribute.max_health";
    private const string HealthId = "resource.health";
    private const string SwiftTag = "Equipment.Swift";
    private const string LuckyTag = "Equipment.Lucky";
    private const string SecondWindAbilityId = "ability.second_wind";
    private const string LuckyRegenStatusId = "status.lucky_regen";

    public static string Execute()
    {
        var report = new StringBuilder();
        report.AppendLine("===== RG6 PLAYTEST BEGIN =====");

        RuntimeServices services = Boot();
        GameplayObject player = Spawn(services, PlayerId);
        GameplayObject cache = Spawn(services, CacheId);

        AttributeSet attributes = player.Get<AttributeSet>();
        InventorySet bag = player.Get<InventorySet>();
        EquipmentSet equipment = player.Get<EquipmentSet>();

        report.AppendLine($"Player slots: boots={equipment.HasSlot(new DefinitionId(BootsSlotId))}, charm={equipment.HasSlot(new DefinitionId(CharmSlotId))}");
        report.AppendLine($"Baseline: moveSpeed={attributes.GetValue(new DefinitionId(MoveSpeedId))}, maxHealth={attributes.GetValue(new DefinitionId(MaxHealthId))}, health.max={player.Get<ResourceSet>().GetResource(new DefinitionId(HealthId)).Maximum}");

        var interactions = new InteractionSystem(services.EventBus, services.TagTable);
        var loot = interactions.TryInteract(player, cache, new DefinitionId(LootInteractionId));
        report.AppendLine($"Loot cache: {loot}; bag boots={bag.QuantityOf(new DefinitionId(BootsItemId))}, charm={bag.QuantityOf(new DefinitionId(CharmItemId))}");

        EquipmentSlotDefinition[] managed =
        {
            services.DataRegistry.Get<EquipmentSlotDefinition>(new DefinitionId(BootsSlotId)),
            services.DataRegistry.Get<EquipmentSlotDefinition>(new DefinitionId(CharmSlotId)),
        };
        int equipped = 0;
        while (InventoryEquip.TryEquipFromInventory(bag, equipment, managed, out _, out _))
        {
            equipped++;
        }

        report.AppendLine($"Equipped {equipped} item(s).");
        report.AppendLine(Describe("After equip", player, services));

        // Save -> reboot -> restore.
        SaveManager save = services.SaveManager;
        SaveData captured = save.FromJson(save.ToJson(save.Capture(services.Objects)));
        RuntimeServices reload = Boot();
        IReadOnlyList<GameplayObject> restored = reload.SaveManager.Restore(captured, reload.Factory, reload.DataRegistry);
        GameplayObject restoredPlayer = FindByDefinition(restored, PlayerId);
        report.AppendLine(Describe("After save/reload", restoredPlayer, reload));

        report.AppendLine("===== RG6 PLAYTEST END =====");
        string text = report.ToString();
        Debug.Log(text);
        return text;
    }

    private static string Describe(string label, GameplayObject player, RuntimeServices services)
    {
        AttributeSet attributes = player.Get<AttributeSet>();
        GameplayTagContainer tags = player.Get<GameplayTagContainer>();
        EquipmentSet equipment = player.Get<EquipmentSet>();
        var sb = new StringBuilder();
        sb.AppendLine($"{label}:");
        sb.AppendLine($"  boots equipped={equipment.GetEquipped(new DefinitionId(BootsSlotId)) != null}, charm equipped={equipment.GetEquipped(new DefinitionId(CharmSlotId)) != null}");
        sb.AppendLine($"  moveSpeed={attributes.GetValue(new DefinitionId(MoveSpeedId))} (base 5 +3)");
        sb.AppendLine($"  maxHealth={attributes.GetValue(new DefinitionId(MaxHealthId))} (base 50 +25); health.max={player.Get<ResourceSet>().GetResource(new DefinitionId(HealthId)).Maximum}");
        sb.AppendLine($"  tag Swift={tags.HasTag(services.TagTable.GetTag(SwiftTag))}, tag Lucky={tags.HasTag(services.TagTable.GetTag(LuckyTag))}");
        sb.AppendLine($"  ability SecondWind={player.Get<AbilitySet>().Has(new DefinitionId(SecondWindAbilityId))}");
        sb.Append($"  status LuckyRegen={player.Get<StatusEffectSet>().Has(new DefinitionId(LuckyRegenStatusId))}");
        return sb.ToString();
    }

    private static GameplayObject Spawn(RuntimeServices services, string id)
    {
        GameplayObjectDefinition definition = services.DataRegistry.Get<GameplayObjectDefinition>(new DefinitionId(id));
        GameplayObject obj = services.Factory.Create(definition);
        obj.Activate();
        return obj;
    }

    private static GameplayObject FindByDefinition(IReadOnlyList<GameplayObject> objects, string id)
    {
        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i].DefinitionId.Value == id)
            {
                return objects[i];
            }
        }

        throw new System.InvalidOperationException("No restored object with definition " + id);
    }

    private static RuntimeServices Boot()
    {
        return new RuntimeBootstrap().Run(
            new BootstrapConfiguration(new UnityGameLogger(), new[] { LoadAuthoredDefinitions() }));
    }

    private static DirectDefinitionSource LoadAuthoredDefinitions()
    {
        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { DefinitionsFolder });
        var definitions = new List<IDefinition>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset is IDefinition definition)
            {
                definitions.Add(definition);
            }
        }

        return new DirectDefinitionSource(definitions);
    }
}
