using ToyChest.Framework.Data;

namespace ToyChest.Systems.Attributes
{
    /// <summary>
    /// Read-only access to an object's attributes for systems that consume attribute values
    /// (Resources binding their maximum, Equipment reading totals, AI evaluating targets).
    /// Consumers depend on this interface rather than the concrete <see cref="AttributeSet"/>,
    /// keeping dependencies to the stable contract only.
    /// </summary>
    public interface IAttributeProvider
    {
        /// <summary>Whether an attribute with this definition id is present.</summary>
        bool HasAttribute(DefinitionId attribute);

        /// <summary>
        /// The current computed value of the attribute.
        /// Throws when the attribute is not present; use <see cref="TryGetValue"/> to probe.
        /// </summary>
        float GetValue(DefinitionId attribute);

        /// <summary>Tolerant value lookup for optional attributes.</summary>
        bool TryGetValue(DefinitionId attribute, out float value);

        /// <summary>The runtime attribute, or null when not present, for change subscription.</summary>
        AttributeValue GetAttribute(DefinitionId attribute);
    }
}
