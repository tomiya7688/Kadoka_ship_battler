using UnityEngine;

namespace KadokaShipBattler.Characters
{
    [CreateAssetMenu(menuName = "Kadoka Ship Battler/Character Definition", fileName = "CharacterDefinition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private string characterId = "character";
        [SerializeField] private string displayName = "Character";
        [SerializeField] private CharacterCapability capabilities = CharacterCapability.None;

        [Header("Base stats")]
        [Min(0f)] [SerializeField] private float moveSpeed = 4f;
        [Range(0f, 10f)] [SerializeField] private float combatSkill = 5f;
        [Range(0f, 10f)] [SerializeField] private float carrySkill = 5f;
        [Min(0f)] [SerializeField] private float carryCapacity = 5f;
        [Min(0)] [SerializeField] private int maxCarryCount = 1;
        [Min(1f)] [SerializeField] private float maxHp = 100f;
        [SerializeField] private NormalAttackMode attackMode = NormalAttackMode.Contact;
        [Min(0f)] [SerializeField] private float attackDamage = 25f;
        [Min(0f)] [SerializeField] private float attackRange = 0.9f;
        [Min(0f)] [SerializeField] private float projectileHardness = 1f;
        [SerializeField] private bool canFly;
        [SerializeField] private bool canPhase;

        public string CharacterId => characterId;
        public string DisplayName => displayName;
        public CharacterCapability Capabilities => capabilities;
        public float MoveSpeed => moveSpeed;
        public float CombatSkill => combatSkill;
        public float CarrySkill => carrySkill;
        public float CarryCapacity => carryCapacity;
        public int MaxCarryCount => maxCarryCount;
        public float MaxHp => maxHp;
        public NormalAttackMode AttackMode => attackMode;
        public float AttackDamage => attackDamage;
        public float AttackRange => attackRange;
        public float ProjectileHardness => projectileHardness;
        public bool CanFly => canFly;
        public bool CanPhase => canPhase;

        public void Initialize(string id, string name, CharacterCapability allowedCapabilities, float speed, float combat,
            float capacity = 5f, int maxCount = 1)
        {
            if (capacity < 0 || float.IsNaN(capacity) || float.IsInfinity(capacity))
                throw new System.ArgumentOutOfRangeException(nameof(capacity));
            if (maxCount < 0) throw new System.ArgumentOutOfRangeException(nameof(maxCount));
            characterId = id;
            displayName = name;
            capabilities = allowedCapabilities & ~CharacterCapability.Repair;
            moveSpeed = Mathf.Max(0f, speed);
            combatSkill = Mathf.Clamp(combat, 0f, 10f);
            carryCapacity = capacity;
            maxCarryCount = maxCount;
            attackDamage = Mathf.Max(1f, combatSkill * 5f);
        }

        public void Initialize(CharacterStats stats)
        {
            if (stats == null) throw new System.ArgumentNullException(nameof(stats));
            stats.Validate();
            Initialize(stats.id, stats.displayName, stats.capabilities, stats.moveSpeed, stats.combatSkill,
                stats.carryCapacity, stats.maxCarryCount);
            carrySkill = stats.carrySkill;
            maxHp = stats.maxHp;
            attackMode = stats.attackMode;
            attackDamage = stats.attackDamage;
            attackRange = stats.attackRange;
            projectileHardness = stats.projectileHardness;
            canFly = stats.canFly;
            canPhase = stats.canPhase;
        }

        public bool HasCapability(CharacterCapability capability)
        {
            return (capability & CharacterCapability.Repair) == 0 && (capabilities & capability) == capability;
        }
    }
}
