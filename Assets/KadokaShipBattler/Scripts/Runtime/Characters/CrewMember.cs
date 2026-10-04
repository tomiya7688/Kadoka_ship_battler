using KadokaShipBattler.Core;
using UnityEngine;

namespace KadokaShipBattler.Characters
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class CrewMember : MonoBehaviour
    {
        [SerializeField] private CharacterDefinition definition;
        [SerializeField] private TeamSide teamSide;
        private CrewHealthState health;

        public CharacterDefinition Definition => definition;
        public TeamSide TeamSide => teamSide;
        public float CurrentHp => Health.CurrentHp;
        public bool IsAlive => definition != null && Health.IsAlive;
        private CrewHealthState Health => health ??= new CrewHealthState(definition != null ? definition.MaxHp : 100f);
        private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;

        public void Initialize(CharacterDefinition character, TeamSide side)
        {
            definition = character;
            teamSide = side;
            health = new CrewHealthState(character != null ? character.MaxHp : 100f);
        }

        public bool Can(CharacterCapability capability)
        {
            return IsAlive && definition.HasCapability(capability);
        }

        public bool ApplyDamage(float damage)
        {
            if (!IsAlive || !Health.ApplyDamage(damage)) return false;
            if (!IsAlive)
            {
                var renderer = GetComponent<SpriteRenderer>();
                if (renderer != null) renderer.color *= 0.35f;
            }
            return true;
        }
        public bool TryAttack(CrewMember target)
        {
            return target != null && target.TeamSide != teamSide && Can(CharacterCapability.Combat) &&
                definition.AttackMode != NormalAttackMode.None && target.ApplyDamage(definition.AttackDamage);
        }
    }
}
