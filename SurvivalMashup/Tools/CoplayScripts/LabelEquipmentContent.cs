using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// Tags the Review Group 6 equipment definitions with the Addressables "definitions" label so the
/// runtime AddressablesDefinitionSource loads them, exactly like all prior authored content.
/// Idempotent.
/// </summary>
public static class LabelEquipmentContent
{
    private const string Label = "definitions";
    private const string Dir = "Assets/Game/Content/Definitions";

    private static readonly string[] Names =
    {
        "Slot_Boots", "Slot_Charm",
        "Tag_Equipment_Swift", "Tag_Equipment_Lucky",
        "Fx_Heal_SecondWind", "Fx_Heal_LuckyRegen",
        "Status_LuckyRegen",
        "Ability_SecondWind", "Ability_LootEquipmentCache",
        "Equippable_BootsOfSwiftness", "Equippable_LuckyCharm",
        "Item_BootsOfSwiftness", "Item_LuckyCharm",
        "Fx_AddItem_BootsOfSwiftness", "Fx_AddItem_LuckyCharm",
        "Interaction_LootEquipment", "Obj_EquipmentCache",
    };

    public static string Execute()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            return "ERROR: No AddressableAssetSettings found.";
        }

        if (!settings.GetLabels().Contains(Label))
        {
            settings.AddLabel(Label);
        }

        AddressableAssetGroup group = settings.DefaultGroup;
        var labeled = new List<string>();
        foreach (string name in Names)
        {
            string path = Dir + "/" + name + ".asset";
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                return "ERROR: missing asset " + path;
            }

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            entry.SetLabel(Label, true, true);
            labeled.Add(name);
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true, true);
        AssetDatabase.SaveAssets();
        return "Labeled " + labeled.Count + " assets with '" + Label + "'.";
    }
}
