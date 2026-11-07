using System;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;

namespace KadokaShipBattler.Ammo
{
    /* { 責務: 通常砲弾1発の結末。 } */
    public enum AmmoShotOutcome
    {
        Flying,
        Impacted,
        Intercepted,
        Cancelled
    }

    /* {
        責務: 砲台から発射された通常砲弾を、キャラクター弾と同じ衝突耐久・弾道の飛翔体として扱い、着弾時に既存のダメージ処理を呼ぶ純粋なモデルです。
        前提: 発射時にダメージは与えません。着弾で船体へダメージを与え、迎撃・取消でダメージなしに山札へ返します。結末は1回だけ確定します。
        計算量: 1回の更新は定数時間です。
    } */
    public sealed class AmmoShotModel
    {
        public const float FlightSpeed = 10f;
        private readonly ShipBattleState target;

        public IAmmo Ammo { get; }
        public TeamSide Side { get; }
        public CharacterProjectileModel Flight { get; }
        public AmmoShotOutcome Outcome { get; private set; } = AmmoShotOutcome.Flying;

        /* { 処理: 発射位置から目標へ飛ぶ砲弾を作ります。引数: ammoは弾、sideは発射側の陣営、targetは着弾する船、start/goalは2D座標、kindは弾道。例外: nullと不正値を拒否します。 } */
        public AmmoShotModel(IAmmo ammo, TeamSide side, ShipBattleState target, float startX, float startY, float goalX, float goalY,
            ProjectileTrajectoryKind kind)
        {
            Ammo = ammo ?? throw new ArgumentNullException(nameof(ammo));
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            Side = side;
            Flight = new CharacterProjectileModel(startX, startY, goalX, goalY, FlightSpeed, HardnessOf(ammo), null, null, kind);
        }

        /* { 処理: 弾の硬さを衝突耐久の整数へ換算します。戻り値: 硬さのない弾や1未満や非有限値は1、それ以外は四捨五入した整数です。 } */
        public static int HardnessOf(IAmmo ammo)
        {
            if (ammo is not IHardAmmo hard) return 1;
            var value = hard.Hardness;
            if (float.IsNaN(value) || value < 1f) return 1;
            return float.IsInfinity(value) || value > 1000f ? 1000 : (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        /* { 処理: 飛行だけを進めます。結末は確定しません。引数: deltaTimeは有限の正の秒数。 } */
        public void Step(float deltaTime)
        {
            if (Outcome == AmmoShotOutcome.Flying) Flight.Advance(deltaTime);
        }

        /* { 処理: 飛行の結果を確定します。着地なら船体へダメージを与えて山札へ返し、撃墜なら返すだけです。戻り値: 現在の結末。 } */
        public AmmoShotOutcome Settle()
        {
            if (Outcome != AmmoShotOutcome.Flying) return Outcome;
            if (Flight.Status == ProjectileFlightStatus.Landed)
            {
                target.ApplyHullDamage(Ammo.Damage);
                Finish(AmmoShotOutcome.Impacted);
            }
            else if (Flight.Status == ProjectileFlightStatus.Destroyed)
            {
                Finish(AmmoShotOutcome.Intercepted);
            }
            return Outcome;
        }

        /* { 処理: 飛行中の砲弾をダメージなしで取り消し、山札へ返します。場面の破棄時に使います。 } */
        public void Cancel()
        {
            if (Outcome != AmmoShotOutcome.Flying) return;
            Flight.Destroy();
            Finish(AmmoShotOutcome.Cancelled);
        }

        private void Finish(AmmoShotOutcome outcome)
        {
            Outcome = outcome;
            if (Ammo is ITrackedAmmo tracked) tracked.ReturnToDeck();
        }
    }
}
