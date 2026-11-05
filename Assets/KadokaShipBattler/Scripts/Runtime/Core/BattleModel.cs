using System;

namespace KadokaShipBattler.Core
{
    public interface IAmmo
    {
        float Damage { get; }
    }

    public sealed class AmmoCarryState
    {
        public IAmmo CarriedAmmo { get; private set; }

        public bool TryPickup(IAmmo ammo, bool canCarry)
        {
            if (!canCarry || ammo == null || CarriedAmmo != null ||
                float.IsNaN(ammo.Damage) || float.IsInfinity(ammo.Damage) || ammo.Damage < 0f)
                return false;
            CarriedAmmo = ammo;
            return true;
        }

        public IAmmo TakeAmmo()
        {
            var ammo = CarriedAmmo;
            CarriedAmmo = null;
            return ammo;
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
            LoadedAmmo = inventory.TakeAmmo();
            return true;
        }

        public bool FireAt(ShipBattleState owner, ShipBattleState target)
        {
            if (LoadedAmmo == null || owner == null || target == null || owner.IsDestroyed ||
                target.IsDestroyed || ReferenceEquals(owner, target) || owner.TeamSide == target.TeamSide)
                return false;
            target.ApplyHullDamage(LoadedAmmo.Damage);
            LoadedAmmo = null;
            return true;
        }
    }
}
