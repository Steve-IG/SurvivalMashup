using System;

namespace ToyChest.Framework.Events
{
    /// <summary>
    /// Declares the logical category of an event type for tooling, trace filtering, and diagnostics.
    /// Categories are organizational only and must never affect dispatch behavior.
    /// Use the constants in <see cref="EventCategories"/>; systems may introduce new
    /// category strings without modifying the Framework.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class EventCategoryAttribute : Attribute
    {
        /// <summary>The logical category name, e.g. <see cref="EventCategories.Resource"/>.</summary>
        public string Category { get; }

        /// <summary>Declares the category of the annotated event type.</summary>
        /// <param name="category">The logical category name. Prefer <see cref="EventCategories"/> constants.</param>
        public EventCategoryAttribute(string category)
        {
            Category = category;
        }
    }
}
