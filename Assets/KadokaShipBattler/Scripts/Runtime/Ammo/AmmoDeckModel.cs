using System;
using System.Collections.Generic;
using KadokaShipBattler.Core;

namespace KadokaShipBattler.Ammo
{
    [Serializable]
    public sealed class AmmoStats : IAmmo
    {
        public string id;
        public string displayName;
        public float damage;
        public float weight;
        public float hardness;
        public float Damage => damage;
        public float Weight => weight;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName) ||
                !FiniteNonNegative(damage) || !FiniteNonNegative(weight) || !FiniteNonNegative(hardness))
                throw new ArgumentException("Invalid ammo definition: " + id);
        }
        internal static bool FiniteNonNegative(float value) => value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class AmmoDeckData { public string[] slots; }
    [Serializable]
    public sealed class AmmoSpawnPoint { public float x; public float y; }
    [Serializable]
    public sealed class AmmoSpawnLayout
    {
        public string id;
        // Coordinates relative to the owning ship center, mirrored for the enemy.
        public AmmoSpawnPoint[] points;
    }
    [Serializable]
    public sealed class BattleAmmoDefinition
    {
        public AmmoStats[] ammo;
        public AmmoDeckData player;
        public AmmoDeckData enemy;
        public AmmoSpawnLayout[] layouts;
        public string playerLayout;
        public string enemyLayout;

        public void Validate()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (ammo == null || ammo.Length == 0) throw new ArgumentException("Ammo definitions are required.");
            foreach (var item in ammo)
            {
                if (item == null) throw new ArgumentException("Null ammo definition.");
                item.Validate();
                if (!ids.Add(item.id)) throw new ArgumentException("Duplicate ammo id: " + item.id);
            }
            ValidateDeck(player, ids);
            ValidateDeck(enemy, ids);
            var layoutIds = new HashSet<string>(StringComparer.Ordinal);
            if (layouts == null || layouts.Length == 0) throw new ArgumentException("Spawn layouts are required.");
            foreach (var layout in layouts)
            {
                if (layout == null || string.IsNullOrWhiteSpace(layout.id) || !layoutIds.Add(layout.id) ||
                    layout.points == null || layout.points.Length == 0) throw new ArgumentException("Invalid spawn layout.");
                var points = new HashSet<(float, float)>();
                foreach (var point in layout.points)
                    if (point == null || float.IsNaN(point.x) || float.IsInfinity(point.x) ||
                        float.IsNaN(point.y) || float.IsInfinity(point.y) || !points.Add((point.x, point.y)))
                        throw new ArgumentException("Invalid or duplicate spawn point.");
            }
            if (playerLayout == null || enemyLayout == null || !layoutIds.Contains(playerLayout) || !layoutIds.Contains(enemyLayout))
                throw new ArgumentException("Unknown ship spawn layout.");
        }
        private static void ValidateDeck(AmmoDeckData deck, HashSet<string> ids)
        {
            if (deck == null || deck.slots == null || deck.slots.Length != AmmoDeckState.DeckSize)
                throw new ArgumentException("Each ammo deck must have exactly 25 slots.");
            foreach (var id in deck.slots)
                if (id == null || !ids.Contains(id)) throw new ArgumentException("Unknown ammo in deck: " + id);
        }
        public AmmoSpawnLayout GetLayout(TeamSide side)
        {
            if (side != TeamSide.Player && side != TeamSide.Enemy) throw new ArgumentOutOfRangeException(nameof(side));
            Validate();
            var id = side == TeamSide.Player ? playerLayout : enemyLayout;
            return Array.Find(layouts, layout => layout.id == id);
        }
    }

    public enum AmmoRoundStage { Ground, Carried, Loaded, Returned }

    // A fresh handle for every appearance. Old handles cannot return or take a new appearance of the same slot.
    public sealed class AmmoDeckRound : ITrackedAmmo
    {
        private readonly AmmoDeckState owner;
        public int Slot { get; }
        public IAmmo Definition { get; }
        public float Damage { get; }
        public float Weight { get; }
        public AmmoRoundStage Stage { get; private set; } = AmmoRoundStage.Ground;
        public bool CanCarry => Stage == AmmoRoundStage.Ground && owner.IsCurrent(this);
        internal AmmoDeckRound(AmmoDeckState owner, int slot, IAmmo definition)
        {
            this.owner = owner;
            Slot = slot;
            Definition = definition;
            Damage = definition.Damage;
            Weight = definition.Weight;
        }
        public bool TryCarry() => Transition(AmmoRoundStage.Ground, AmmoRoundStage.Carried);
        public bool TryLoad() => Transition(AmmoRoundStage.Carried, AmmoRoundStage.Loaded);
        private bool Transition(AmmoRoundStage from, AmmoRoundStage to)
        {
            if (Stage != from || !owner.IsCurrent(this)) return false;
            Stage = to;
            return true;
        }
        public bool ReturnToDeck()
        {
            if (!owner.Return(this)) return false;
            Stage = AmmoRoundStage.Returned;
            return true;
        }
    }

    public sealed class AmmoDeckState
    {
        public const int DeckSize = 25;
        private readonly IAmmo[] definitions;
        private readonly AmmoDeckRound[] active = new AmmoDeckRound[DeckSize];
        private readonly Random random;
        private bool closed;
        public int ActiveCount { get; private set; }
        public int WaitingCount => DeckSize - ActiveCount;
        public AmmoDeckState(IReadOnlyList<IAmmo> entries, int seed)
        {
            if (entries == null || entries.Count != DeckSize) throw new ArgumentException("An ammo deck requires 25 entries.");
            definitions = new IAmmo[DeckSize];
            for (var index = 0; index < DeckSize; index++)
            {
                var entry = entries[index];
                if (entry == null || !AmmoStats.FiniteNonNegative(entry.Damage) || !AmmoStats.FiniteNonNegative(entry.Weight))
                    throw new ArgumentException("Invalid ammo deck entry.");
                definitions[index] = entry;
            }
            random = new Random(seed);
        }
        public AmmoDeckRound TrySpawn()
        {
            if (closed || WaitingCount == 0) return null;
            // Choose among actual waiting slots, never by rarity or ammo type.
            var choice = random.Next(WaitingCount);
            for (var slot = 0; slot < DeckSize; slot++)
            {
                if (active[slot] != null) continue;
                if (choice-- != 0) continue;
                var round = new AmmoDeckRound(this, slot, definitions[slot]);
                active[slot] = round;
                ActiveCount++;
                return round;
            }
            throw new InvalidOperationException("Ammo deck accounting mismatch.");
        }
        internal bool IsCurrent(AmmoDeckRound round) => ReferenceEquals(active[round.Slot], round);
        internal bool Return(AmmoDeckRound round)
        {
            if (!IsCurrent(round)) return false;
            active[round.Slot] = null;
            ActiveCount--;
            return true;
        }
        public void Close()
        {
            closed = true;
            foreach (var round in active) round?.ReturnToDeck();
        }
    }
}
