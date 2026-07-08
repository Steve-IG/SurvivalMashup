namespace ToyChest.Systems.Equipment
{
    /// <summary>
    /// Outcome of an equip attempt. Checks run in the declared order — equippable, slot
    /// known, slot allowed, slot free, owner requirements — and the first failing check is
    /// the reported reason, deterministically.
    /// </summary>
    public enum EquipResult
    {
        /// <summary>Every check passed; the item is equipped and its contributions are active.</summary>
        Equipped,

        /// <summary>The item carries no equippable component.</summary>
        NotEquippable,

        /// <summary>The owner has no slot with the requested id.</summary>
        UnknownSlot,

        /// <summary>The item does not fit the requested slot.</summary>
        SlotNotAllowed,

        /// <summary>The requested slot already holds an item. Unequip it first.</summary>
        SlotOccupied,

        /// <summary>The owner lacks a tag the item requires.</summary>
        MissingRequiredTag,
    }
}
