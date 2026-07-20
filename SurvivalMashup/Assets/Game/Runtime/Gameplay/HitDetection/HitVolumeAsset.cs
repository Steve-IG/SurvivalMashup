using UnityEngine;

namespace ToyChest.Gameplay.HitDetection
{
    /// <summary>
    /// A shared, reusable hit-volume <em>preset</em> — the production authoring unit. One asset ("Sword
    /// Slash Wide", "Punch", "Explosion Large", "Boss Slam") captures a <see cref="HitVolume"/> shape once
    /// and is referenced by many attacks across many characters, so the shape is authored a single time and
    /// never duplicated. It carries only geometry: no faction filter (the attacker supplies that) and no
    /// anchor (the attack supplies that), which is exactly what makes it reusable between player, enemy,
    /// and boss.
    ///
    /// This is a plain presentation/authoring asset, not a gameplay <c>Definition</c>: it needs no runtime
    /// id or Data Registry entry because attacks reference it directly, the same way a hazard references a
    /// status asset. Create via <b>Assets ▸ Create ▸ ToyChest ▸ Hit Volume</b> and build a small shared
    /// library under <c>Assets/Game/Content/HitVolumes</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Hit Volume", fileName = "HitVolume")]
    public sealed class HitVolumeAsset : ScriptableObject
    {
        [SerializeField]
        [Tooltip("The reusable shape this preset represents.")]
        private HitVolume _volume = new HitVolume(HitShape.Cone, 2.5f, 70f, multiTarget: false, maxTargets: 1);

        /// <summary>The reusable hit-volume shape.</summary>
        public HitVolume Volume => _volume;
    }
}
