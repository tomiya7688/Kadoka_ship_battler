using UnityEngine;

namespace KadokaShipBattler.Ammo
{
    [CreateAssetMenu(menuName = "Kadoka Ship Battler/Ammo Definition", fileName = "AmmoDefinition")]
    public sealed class AmmoDefinition : ScriptableObject
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
    }
}
