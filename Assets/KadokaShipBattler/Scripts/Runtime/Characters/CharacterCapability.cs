using System;

namespace KadokaShipBattler.Characters
{
    [Flags]
    public enum CharacterCapability
    {
        None = 0,
        Combat = 1 << 0,
        CarryAmmo = 1 << 1,
        OperateCannon = 1 << 2,
        Repair = 1 << 3,
        BoardEnemyShip = 1 << 4,
        Support = 1 << 5
    }
}
