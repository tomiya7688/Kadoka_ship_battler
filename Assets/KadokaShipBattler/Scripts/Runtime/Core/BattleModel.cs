using System;
using System.Collections.Generic;

namespace KadokaShipBattler.Core
{
    public interface IAmmo
    {
        float Damage { get; }
        float Weight { get; }
    }

    public interface ITrackedAmmo : IAmmo
    {
        bool CanCarry { get; }
        bool TryCarry();
        bool TryLoad();
        bool ReturnToDeck();
    }

    public sealed class AmmoCarryState
    {
        private readonly List<IAmmo> items = new();
        private readonly List<float> acceptedWeights = new();
        private double currentWeight;
        public IReadOnlyList<IAmmo> Items { get; }
        public IAmmo CarriedAmmo => items.Count > 0 ? items[0] : null;
        public int Count => items.Count;
        public float CurrentWeight => (float)currentWeight;
        public float CarryCapacity { get; private set; }
        public int MaxCarryCount { get; private set; }

        public AmmoCarryState(float carryCapacity = float.MaxValue, int maxCarryCount = 1)
        {
            Items = items.AsReadOnly();
            ConfigureLimits(carryCapacity, maxCarryCount);
        }

        // Existing items survive reduced limits; only new pickups are blocked until they fit again.
        public void ConfigureLimits(float carryCapacity, int maxCarryCount)
        {
            if (!IsNonNegativeFinite(carryCapacity)) throw new ArgumentOutOfRangeException(nameof(carryCapacity));
            if (maxCarryCount < 0) throw new ArgumentOutOfRangeException(nameof(maxCarryCount));
            CarryCapacity = carryCapacity;
            MaxCarryCount = maxCarryCount;
        }

        public bool CanPickup(IAmmo ammo, bool canCarry)
        {
            return canCarry && ammo != null && Count < MaxCarryCount &&
                (!(ammo is ITrackedAmmo tracked) || tracked.CanCarry) &&
                IsNonNegativeFinite(ammo.Damage) && IsNonNegativeFinite(ammo.Weight) &&
                currentWeight + ammo.Weight <= CarryCapacity;
        }

        public bool TryPickup(IAmmo ammo, bool canCarry)
        {
            if (!CanPickup(ammo, canCarry)) return false;
            if (ammo is ITrackedAmmo tracked && !tracked.TryCarry()) return false;
            items.Add(ammo);
            acceptedWeights.Add(ammo.Weight);
            currentWeight += ammo.Weight;
            return true;
        }

        public IAmmo TakeAmmo()
        {
            var ammo = CarriedAmmo;
            if (ammo == null) return null;
            currentWeight = Math.Max(0, currentWeight - acceptedWeights[0]);
            items.RemoveAt(0);
            acceptedWeights.RemoveAt(0);
            if (items.Count == 0) currentWeight = 0;
            return ammo;
        }

        private static bool IsNonNegativeFinite(float value) =>
            value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);

        public void Clear()
        {
            foreach (var item in items) if (item is ITrackedAmmo tracked) tracked.ReturnToDeck();
            items.Clear();
            acceptedWeights.Clear();
            currentWeight = 0;
        }
    }

    public sealed class ShipBattleState
    {
        public TeamSide TeamSide { get; }
        public float MaxHull { get; }
        public float CurrentHull { get; private set; }
        public float MaxCore { get; }
        public float CurrentCore { get; private set; }
        public bool IsHullBreached => CurrentHull <= 0f;
        public bool IsDestroyed => CurrentCore <= 0f;

        public ShipBattleState(TeamSide side, float maxHull, float maxCore)
        {
            if (!IsPositiveFinite(maxHull) || !IsPositiveFinite(maxCore))
                throw new ArgumentOutOfRangeException(nameof(maxHull), "Hull and core health must be positive and finite.");
            TeamSide = side;
            MaxHull = CurrentHull = maxHull;
            MaxCore = CurrentCore = maxCore;
        }

        public bool ApplyHullDamage(float damage)
        {
            if (!IsPositiveFinite(damage) || IsDestroyed || IsHullBreached)
                return false;
            CurrentHull = Math.Max(0f, CurrentHull - damage);
            return true;
        }

        public bool TryDamageCore(float damage)
        {
            if (!IsPositiveFinite(damage) || !IsHullBreached || IsDestroyed)
                return false;
            CurrentCore = Math.Max(0f, CurrentCore - damage);
            return true;
        }

        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class CannonState
    {
        public IAmmo LoadedAmmo { get; private set; }

        public bool TryLoadFrom(AmmoCarryState inventory, ShipBattleState owner, TeamSide carrierSide)
        {
            if (inventory == null || owner == null || owner.IsDestroyed ||
                owner.TeamSide != carrierSide || LoadedAmmo != null || inventory.CarriedAmmo == null)
                return false;
            if (inventory.CarriedAmmo is ITrackedAmmo tracked && !tracked.TryLoad()) return false;
            LoadedAmmo = inventory.TakeAmmo();
            return true;
        }

        public bool FireAt(ShipBattleState owner, ShipBattleState target)
        {
            if (LoadedAmmo == null || owner == null || target == null || owner.IsDestroyed ||
                target.IsDestroyed || ReferenceEquals(owner, target) || owner.TeamSide == target.TeamSide)
                return false;
            target.ApplyHullDamage(LoadedAmmo.Damage);
            if (LoadedAmmo is ITrackedAmmo tracked) tracked.ReturnToDeck();
            LoadedAmmo = null;
            return true;
        }

        public void Clear()
        {
            if (LoadedAmmo is ITrackedAmmo tracked) tracked.ReturnToDeck();
            LoadedAmmo = null;
        }
    }
}
