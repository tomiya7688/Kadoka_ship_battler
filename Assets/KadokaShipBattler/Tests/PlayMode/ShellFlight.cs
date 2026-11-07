using KadokaShipBattler.Ammo;

namespace KadokaShipBattler.Tests
{
    /* { 責務: PlayModeテストで、飛行中の通常砲弾をフレームを待たずに着弾まで進めます。 } */
    internal static class ShellFlight
    {
        /* { 処理: 飛行中の砲弾がなくなるまで、小刻みに進めます。補足: 上限を超えたら打ち切ります。 } */
        public static void Land()
        {
            for (var step = 0; step < 400 && AmmoProjectile.LiveCount > 0; step++) AmmoProjectile.AdvanceAll(0.05f);
        }
    }
}
