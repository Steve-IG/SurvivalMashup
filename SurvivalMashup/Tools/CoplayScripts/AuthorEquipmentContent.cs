using System.Collections.Generic;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.Equipment;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Interactions;
using ToyChest.Systems.Items;
using ToyChest.Systems.Resources;
using ToyChest.Systems.StatusEffects;
using ToyChest.Systems.Tags;
using ToyChest.Gameplay.Objects;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// One-shot authoring of the Review Group 6 equipment content as real ScriptableObject assets
/// under Assets/Game/Content/Definitions. Idempotent: re-running reconfigures existing assets in
/// place. Addressables labeling is a separate step (LabelEquipmentContent).
/// </summary>
public static class AuthorEquipmentContent
{
    private const string Dir = "Assets/Game/Content/Definitions";

    public static string Execute()
    {
        var log = new List<string>();

        // ---- Equipment slots (player slot layout is authored data) ----
        EquipmentSlotDefinition slotBoots = Slot("Slot_Boots", "slot.boots", "Boots");
        EquipmentSlotDefinition slotCharm = Slot("Slot_Charm", "slot.charm", "Charm");

        // ---- Gameplay tags granted by equipment ----
        TagDefinition tagSwift = Tag("Tag_Equipment_Swift", "Equipment.Swift", "Granted while swift-footed equipment is worn.");
        TagDefinition tagLucky = Tag("Tag_Equipment_Lucky", "Equipment.Lucky", "Granted while a lucky charm is worn.");

        // ---- Heal effects (reused by an ability and a passive status) ----
        HealEffect fxSecondWind = Heal("Fx_Heal_SecondWind", "fx.heal.second_wind", "resource.health", 25f);
        HealEffect fxLuckyRegen = Heal("Fx_Heal_LuckyRegen", "fx.heal.lucky_regen", "resource.health", 1f);

        // ---- Passive regeneration status (infinite, periodic heal) ----
        StatusEffectDefinition luckyRegen = Status(
            "Status_LuckyRegen", "status.lucky_regen", StatusDurationType.Infinite, periodSeconds: 1f,
            periodic: new GameplayEffectDefinition[] { fxLuckyRegen });

        // ---- Granted ability (Self heal on a cooldown) ----
        AbilityDefinition secondWind = Ability(
            "Ability_SecondWind", "ability.second_wind", "Utility", AbilityTargetMode.Self,
            cooldownSeconds: 10f, effects: new GameplayEffectDefinition[] { fxSecondWind });

        // ---- Equippable components (pure composition of existing systems) ----
        EquippableDefinition equipBoots = Equippable(
            "Equippable_BootsOfSwiftness",
            allowedSlots: new[] { slotBoots },
            grantedTags: new[] { tagSwift },
            modifiers: new[] { ("attribute.movement_speed", 0, 3f) }, // Flat +3 move speed
            grantedAbilities: null,
            statuses: null);

        EquippableDefinition equipCharm = Equippable(
            "Equippable_LuckyCharm",
            allowedSlots: new[] { slotCharm },
            grantedTags: new[] { tagLucky },
            modifiers: new[] { ("attribute.max_health", 0, 25f) }, // Flat +25 maximum health
            grantedAbilities: new[] { secondWind },
            statuses: new[] { luckyRegen });

        // ---- Items ----
        ItemDefinition itemBoots = Item("Item_BootsOfSwiftness", "item.boots_of_swiftness", "Equipment", equipBoots);
        ItemDefinition itemCharm = Item("Item_LuckyCharm", "item.lucky_charm", "Equipment", equipCharm);

        // ---- Pickup path: an Equipment Cache that grants both items through the loot pattern ----
        AddItemEffect fxAddBoots = AddItem("Fx_AddItem_BootsOfSwiftness", "fx.add_item.boots_of_swiftness", itemBoots);
        AddItemEffect fxAddCharm = AddItem("Fx_AddItem_LuckyCharm", "fx.add_item.lucky_charm", itemCharm);
        AbilityDefinition lootCache = Ability(
            "Ability_LootEquipmentCache", "ability.loot_equipment_cache", "Interaction", AbilityTargetMode.Provided,
            cooldownSeconds: 0f, effects: new GameplayEffectDefinition[] { fxAddBoots, fxAddCharm },
            costResourceId: "resource.loot_charge", costAmount: 1f);

        TagDefinition tagPlayer = Load<TagDefinition>("Tag_Actor_Player");
        InteractionDefinition interactionLoot = Interaction(
            "Interaction_LootEquipment", "interaction.loot_equipment", "Take", lootCache, priority: 10,
            requiredInteractorTags: new[] { tagPlayer });

        ResourceDefinition resLoot = Load<ResourceDefinition>("Res_LootCharge");
        TagDefinition tagCrate = Load<TagDefinition>("Tag_Object_Container_Crate");
        GameplayObjectDefinition cache = Object3(
            "Obj_EquipmentCache", "object.equipment_cache", "Equipment Cache",
            resources: new[] { resLoot }, initialTags: new[] { tagCrate }, interactions: new[] { interactionLoot });

        // ---- Player gains the two equipment slots ----
        GameplayObjectDefinition player = Load<GameplayObjectDefinition>("Obj_Player");
        var playerSo = new SerializedObject(player);
        SetObjectArray(playerSo, "_equipmentSlots", new Object[] { slotBoots, slotCharm });
        playerSo.ApplyModifiedPropertiesWithoutUndo();
        log.Add("Obj_Player equipment slots -> [slot.boots, slot.charm]");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "Authored equipment content OK. " + string.Join("; ", log);
    }

    // ------------------------------------------------------------------ creators

    private static EquipmentSlotDefinition Slot(string name, string id, string display)
    {
        var a = GetOrCreate<EquipmentSlotDefinition>(name);
        var so = new SerializedObject(a);
        so.FindProperty("_definitionId").stringValue = id;
        so.FindProperty("_displayName").stringValue = display;
        so.ApplyModifiedPropertiesWithoutUndo();
        return a;
    }

    private static TagDefinition Tag(string name, string path, string description)
    {
        var a = GetOrCreate<TagDefinition>(name);
        var so = new SerializedObject(a);
        so.FindProperty("_tagPath").stringValue = path;
        so.FindProperty("_description").stringValue = description;
        so.ApplyModifiedPropertiesWithoutUndo();
        return a;
    }

    private static HealEffect Heal(string name, string id, string resourceId, float amount)
    {
        var a = GetOrCreate<HealEffect>(name);
        var so = new SerializedObject(a);
        so.FindProperty("_definitionId").stringValue = id;
        so.FindProperty("_resourceId").stringValue = resourceId;
        so.FindProperty("_amount").floatValue = amount;
        SetArraySize(so, "_conditions", 0);
        so.ApplyModifiedPropertiesWithoutUndo();
        return a;
    }

    private static AddItemEffect AddItem(string name, string id, ItemDefinition item)
    {
        var a = GetOrCreate<AddItemEffect>(name);
        var so = new SerializedObject(a);
        so.FindProperty("_definitionId").stringValue = id;
        so.FindProperty("_item").objectReferenceValue = item;
        so.FindProperty("_quantity").intValue = 1;
        SetArraySize(so, "_conditions", 0);
        so.ApplyModifiedPropertiesWithoutUndo();
        return a;
    }

    private static StatusEffectDefinition Status(
        string name, string id, StatusDurationType durationType, float periodSeconds, GameplayEffectDefinition[] periodic)
    {
        var a = GetOrCreate<StatusEffectDefinition>(name);
        var so = new SerializedObject(a);
        so.FindProperty("_definitionId").stringValue = id;
        so.FindProperty("_displayName").stringValue = name;
        so.FindProperty("_durationType").enumValueIndex = (int)durationType;
        so.FindProperty("_durationSeconds").floatValue = 0f;
        so.FindProperty("_stackingRule").enumValueIndex = 0;
        so.FindProperty("_maximumStacks").intValue = 1;
        SetObjectArray(so, "_grantedTags", null);
        SetArraySize(so, "_modifiers", 0);
        SetObjectArray(so, "_onApplyEffects", null);
        so.FindProperty("_periodSeconds").floatValue = periodSeconds;
        SetObjectArray(so, "_periodicEffects", periodic);
        SetObjectArray(so, "_onEndEffects", null);
        so.ApplyModifiedPropertiesWithoutUndo();
        return a;
    }

    private static AbilityDefinition Ability(
        string name, string id, string category, AbilityTargetMode targetMode, float cooldownSeconds,
        GameplayEffectDefinition[] effects, string costResourceId = null, float costAmount = 0f)
    {
        var a = GetOrCreate<AbilityDefinition>(name);
        var so = new SerializedObject(a);
        so.FindProperty("_definitionId").stringValue = id;
        so.FindProperty("_displayName").stringValue = name;
        so.FindProperty("_category").stringValue = category;
        SetObjectArray(so, "_tags", null);
        so.FindProperty("_targetMode").enumValueIndex = (int)targetMode;
        SetObjectArray(so, "_requiredOwnerTags", null);
        SetObjectArray(so, "_blockedByOwnerTags", null);

        SerializedProperty costs = so.FindProperty("_costs");
        if (string.IsNullOrEmpty(costResourceId))
        {
            costs.arraySize = 0;
        }
        else
        {
            costs.arraySize = 1;
            SerializedProperty c = costs.GetArrayElementAtIndex(0);
            c.FindPropertyRelative("_resourceId").stringValue = costResourceId;
            c.FindPropertyRelative("_amount").floatValue = costAmount;
        }

        so.FindProperty("_cooldownSeconds").floatValue = cooldownSeconds;
        SetObjectArray(so, "_effects", effects);
        so.ApplyModifiedPropertiesWithoutUndo();
        return a;
    }

    private static EquippableDefinition Equippable(
        string name, EquipmentSlotDefinition[] allowedSlots, TagDefinition[] grantedTags,
        (string attrId, int op, float value)[] modifiers, AbilityDefinition[] grantedAbilities,
        StatusEffectDefinition[] statuses)
    {
        var a = GetOrCreate<EquippableDefinition>(name);
        var so = new SerializedObject(a);
        SetObjectArray(so, "_allowedSlots", allowedSlots);
        SetObjectArray(so, "_requiredOwnerTags", null);
        SetObjectArray(so, "_grantedTags", grantedTags);

        SerializedProperty mods = so.FindProperty("_attributeModifiers");
        int count = modifiers?.Length ?? 0;
        mods.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            SerializedProperty e = mods.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("_attributeId").stringValue = modifiers[i].attrId;
            e.FindPropertyRelative("_operation").enumValueIndex = modifiers[i].op;
            e.FindPropertyRelative("_value").floatValue = modifiers[i].value;
        }

        SetObjectArray(so, "_grantedAbilities", grantedAbilities);
        SetObjectArray(so, "_appliedStatusEffects", statuses);
        so.ApplyModifiedPropertiesWithoutUndo();
        return a;
    }

    private static ItemDefinition Item(string name, string id, string category, ItemComponentDefinition component)
    {
        var a = GetOrCreate<ItemDefinition>(name);
        var so = new SerializedObject(a);
        so.FindProperty("_definitionId").stringValue = id;
        so.FindProperty("_displayName").stringValue = name;
        so.FindProperty("_category").stringValue = category;
        SetObjectArray(so, "_tags", null);
        so.FindProperty("_maxStackSize").intValue = 1;
        SetObjectArray(so, "_components", new Object[] { component });
        so.ApplyModifiedPropertiesWithoutUndo();
        return a;
    }

    private static InteractionDefinition Interaction(
        string name, string id, string display, AbilityDefinition ability, int priority, TagDefinition[] requiredInteractorTags)
    {
        var a = GetOrCreate<InteractionDefinition>(name);
        var so = new SerializedObject(a);
        so.FindProperty("_definitionId").stringValue = id;
        so.FindProperty("_displayName").stringValue = display;
        so.FindProperty("_ability").objectReferenceValue = ability;
        so.FindProperty("_priority").intValue = priority;
        SetObjectArray(so, "_requiredInteractorTags", requiredInteractorTags);
        SetObjectArray(so, "_blockedByInteractorTags", null);
        so.ApplyModifiedPropertiesWithoutUndo();
        return a;
    }

    private static GameplayObjectDefinition Object3(
        string name, string id, string display, ResourceDefinition[] resources, TagDefinition[] initialTags,
        InteractionDefinition[] interactions)
    {
        var a = GetOrCreate<GameplayObjectDefinition>(name);
        var so = new SerializedObject(a);
        so.FindProperty("_definitionId").stringValue = id;
        so.FindProperty("_displayName").stringValue = display;
        SetObjectArray(so, "_attributes", null);
        SetObjectArray(so, "_resources", resources);
        SetObjectArray(so, "_initialTags", initialTags);
        SetObjectArray(so, "_abilities", null);
        so.FindProperty("_inventory").objectReferenceValue = null;
        SetObjectArray(so, "_equipmentSlots", null);
        SetObjectArray(so, "_interactions", interactions);
        so.ApplyModifiedPropertiesWithoutUndo();
        return a;
    }

    // ------------------------------------------------------------------ helpers

    private static T GetOrCreate<T>(string name) where T : ScriptableObject
    {
        string path = Dir + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            return existing;
        }

        var created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    private static T Load<T>(string name) where T : Object
    {
        string path = Dir + "/" + name + ".asset";
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a == null)
        {
            throw new System.InvalidOperationException("Missing required existing asset: " + path);
        }

        return a;
    }

    private static void SetObjectArray(SerializedObject so, string prop, Object[] values)
    {
        SerializedProperty p = so.FindProperty(prop);
        int count = values?.Length ?? 0;
        p.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void SetArraySize(SerializedObject so, string prop, int size)
    {
        so.FindProperty(prop).arraySize = size;
    }
}
