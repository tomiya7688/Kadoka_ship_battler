using System;
using System.Collections.Generic;
using KadokaShipBattler.Core;

namespace KadokaShipBattler.Characters
{
    public enum NormalAttackMode { None = 0, Contact = 1, Ranged = 2 }

    [Serializable]
    public sealed class CharacterStats
    {
        public string id;
        public string displayName;
        public float maxHp;
        public float moveSpeed;
        public float carryCapacity;
        public int maxCarryCount;
        public NormalAttackMode attackMode;
        public float attackDamage;
        public float attackRange;
        public float projectileHardness;
        public bool canFly;
        public bool canPhase;
        public CharacterCapability capabilities;
        public float combatSkill;
        public float carrySkill;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Character id and display name are required.");
            if (!Positive(maxHp) || !NonNegative(moveSpeed) || !NonNegative(carryCapacity) || maxCarryCount < 0 ||
                !NonNegative(attackDamage) || !NonNegative(projectileHardness) || !NonNegative(attackRange) ||
                !NonNegative(combatSkill) || combatSkill > 10 || !NonNegative(carrySkill) || carrySkill > 10)
                throw new ArgumentException("Character stats must be finite and within their allowed ranges: " + id);
            if (!Enum.IsDefined(typeof(NormalAttackMode), attackMode) || attackMode != NormalAttackMode.None && attackRange <= 0)
                throw new ArgumentException("Invalid normal attack configuration: " + id);
            const CharacterCapability allowed = CharacterCapability.Combat | CharacterCapability.CarryAmmo |
                CharacterCapability.OperateCannon | CharacterCapability.BoardEnemyShip | CharacterCapability.Support;
            if ((capabilities & ~allowed) != 0)
                throw new ArgumentException("Unsupported character capability (repair is not supported): " + id);
        }
        private static bool NonNegative(float value) => value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Positive(float value) => value > 0 && NonNegative(value);
    }

    [Serializable]
    public sealed class CrewFormation
    {
        // Slot 0 is the leader; slots 1-4 are allies. Repeated character ids are allowed.
        public string[] slots;
    }

    [Serializable]
    public sealed class BattleCrewDefinition
    {
        public const int TeamSize = 5;
        public CharacterStats[] characters;
        public CrewFormation player;
        public CrewFormation enemy;

        public void Validate()
        {
            if (characters == null || characters.Length == 0) throw new ArgumentException("Character definitions are required.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var character in characters)
            {
                if (character == null) throw new ArgumentException("Null character definition.");
                character.Validate();
                if (!ids.Add(character.id)) throw new ArgumentException("Duplicate character id: " + character.id);
            }
            ValidateFormation(player, ids);
            ValidateFormation(enemy, ids);
        }

        public CharacterStats[] GetTeam(TeamSide side)
        {
            Validate();
            if (side != TeamSide.Player && side != TeamSide.Enemy) throw new ArgumentOutOfRangeException(nameof(side));
            var formation = side == TeamSide.Player ? player : enemy;
            var lookup = new Dictionary<string, CharacterStats>(StringComparer.Ordinal);
            foreach (var character in characters) lookup.Add(character.id, character);
            var result = new CharacterStats[TeamSize];
            for (var slot = 0; slot < TeamSize; slot++) result[slot] = lookup[formation.slots[slot]];
            return result;
        }

        private static void ValidateFormation(CrewFormation formation, HashSet<string> ids)
        {
            if (formation == null || formation.slots == null || formation.slots.Length != TeamSize)
                throw new ArgumentException("Each crew formation must have exactly five slots.");
            foreach (var id in formation.slots)
                if (id == null || !ids.Contains(id)) throw new ArgumentException("Unknown character in crew formation: " + id);
        }
    }

    public sealed class CrewHealthState
    {
        public float MaxHp { get; }
        public float CurrentHp { get; private set; }
        public bool IsAlive => CurrentHp > 0;
        public CrewHealthState(float maxHp)
        {
            if (maxHp <= 0 || float.IsNaN(maxHp) || float.IsInfinity(maxHp)) throw new ArgumentOutOfRangeException(nameof(maxHp));
            MaxHp = CurrentHp = maxHp;
        }
        public bool ApplyDamage(float damage)
        {
            if (!IsAlive || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return false;
            CurrentHp = Math.Max(0, CurrentHp - damage);
            return true;
        }
    }
}
