namespace ToyChest.Framework.Events
{
    /// <summary>
    /// Canonical event category names used with <see cref="EventCategoryAttribute"/>.
    /// Categories are organizational only; they group events for tooling and diagnostics.
    /// Future systems may add categories here or define their own constants.
    /// </summary>
    public static class EventCategories
    {
        public const string Resource = "Resource";
        public const string Attribute = "Attribute";
        public const string Tag = "Tag";
        public const string Ability = "Ability";
        public const string GameplayEffect = "Gameplay Effect";
        public const string StatusEffect = "Status Effect";
        public const string Inventory = "Inventory";
        public const string Equipment = "Equipment";
        public const string Interaction = "Interaction";
        public const string World = "World";
        public const string Adventure = "Adventure";
        public const string Companion = "Companion";
        public const string Ui = "UI";

        /// <summary>Category for cross-cutting Gameplay Framework events (spawn, destroy, lifecycle).</summary>
        public const string Framework = "Framework";
    }
}
