using UnityEngine;
using KadokaShipBattler.Core;

namespace KadokaShipBattler.Ammo
{
    [CreateAssetMenu(menuName = "Kadoka Ship Battler/Ammo Definition", fileName = "AmmoDefinition")]
    public sealed class AmmoDefinition : ScriptableObject, IAmmo
    {
        [SerializeField] private string ammoId = "ammo";
        [SerializeField] private string displayName = "Ammo";
        [Min(0f)] [SerializeField] private float damage = 10f;
        [Min(0f)] [SerializeField] private float weight = 1f;
        [Min(0f)] [SerializeField] private float aiPriority = 1f;

        public string AmmoId => ammoId;
        public string DisplayName => displayName;
        public float Damage => damage;
        public float Weight => weight;
        public float AiPriority => aiPriority;

        public void Initialize(string id, string name, float baseDamage, float baseWeight = 1f)
        {
            if (baseWeight < 0 || float.IsNaN(baseWeight) || float.IsInfinity(baseWeight))
                throw new System.ArgumentOutOfRangeException(nameof(baseWeight));
            ammoId = id;
            displayName = name;
            damage = Mathf.Max(0f, baseDamage);
            weight = baseWeight;
        }
    }
}
