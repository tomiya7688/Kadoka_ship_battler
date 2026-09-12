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
        [Range(0f, 10f)] [SerializeField] private float repairSkill = 5f;

        public string CharacterId => characterId;
        public string DisplayName => displayName;
        public CharacterCapability Capabilities => capabilities;
        public float MoveSpeed => moveSpeed;
        public float CombatSkill => combatSkill;
        public float CarrySkill => carrySkill;
        public float RepairSkill => repairSkill;

        public bool HasCapability(CharacterCapability capability)
        {
            return (capabilities & capability) == capability;
        }
    }
}
