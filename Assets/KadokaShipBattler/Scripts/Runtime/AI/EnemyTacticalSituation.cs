using System;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 戦術判断に使う戦況を、観測済みの値と自船の既知情報だけで保持します。
        前提: 作成時に検証した不変の値です。相手船内の未観測な船員位置・弾・砲台状態を含みません。
              相手船の船体破壊は、橋が開くため両陣営から見える公開情報として扱います。
    } */
    public sealed class EnemyTacticalSituation
    {
        public float OwnHullRatio { get; }
        public bool OwnHullBreached { get; }
        public bool OpponentHullBreached { get; }
        public int ObservedIntruderCount { get; }
        public int KnownAmmoCount { get; }
        public bool IsCannonLoaded { get; }
        public int AliveCrewCount { get; }

        /* {
            処理: 戦況値を検証して保持します。
            引数: ownHullRatioは0以上1以下の自船体残存率。ownHullBreachedは残存率が0である場合だけtrue。
                  observedIntruderCountは自船内で現在観測した相手船員数。knownAmmoCountは共有観測にある自陣営の弾数。
            例外: 範囲外・非有限・矛盾した値を拒否します。
        } */
        public EnemyTacticalSituation(float ownHullRatio, bool ownHullBreached, bool opponentHullBreached,
            int observedIntruderCount, int knownAmmoCount, bool isCannonLoaded, int aliveCrewCount)
        {
            // 範囲外の値で採点を破綻させません。
            if (!UtilityRules.Range(ownHullRatio, 0, 1) || ownHullBreached != (ownHullRatio <= 0) ||
                observedIntruderCount < 0 || knownAmmoCount < 0 || aliveCrewCount < 0)
                throw new ArgumentException("Invalid tactical situation.");
            OwnHullRatio = ownHullRatio;
            OwnHullBreached = ownHullBreached;
            OpponentHullBreached = opponentHullBreached;
            ObservedIntruderCount = observedIntruderCount;
            KnownAmmoCount = knownAmmoCount;
            IsCannonLoaded = isCannonLoaded;
            AliveCrewCount = aliveCrewCount;
        }
    }
}
