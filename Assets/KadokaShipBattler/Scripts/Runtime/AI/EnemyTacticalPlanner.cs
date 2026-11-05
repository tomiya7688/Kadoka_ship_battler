using System;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 戦況とプレイヤーの傾向から、敵チームの共有戦術目標を選びます。
        前提: 切替には最小保持時間と点差が必要です。現在の目標が戦況の上で実行できなくなった場合だけ、直ちに切り替えます。
    } */
    public sealed class EnemyTacticalPlanner
    {
        public const float MinimumHoldSeconds = 2;
        public const float SwitchMargin = 0.15f;
        private float changedAt;

        public EnemyTacticalGoal Goal { get; private set; } = EnemyTacticalGoal.Bombard;

        /* { 処理: 目標の保持時間を数え始める時刻を設定します。例外: 非有限の時刻を拒否します。 } */
        public EnemyTacticalPlanner(float startedAt)
        {
            // 開始直後の目標切替を最小保持時間で抑えます。
            if (!UtilityRules.Range(startedAt, 0, float.MaxValue)) throw new ArgumentOutOfRangeException(nameof(startedAt));
            changedAt = startedAt;
        }

        /* {
            処理: 一つの目標を0以上1以下で採点します。実行できない目標は0です。
            戻り値: 点数。大きいほどチーム全体で優先します。
        } */
        public static float Score(EnemyTacticalGoal goal, EnemyTacticalSituation situation, PlayerTendency tendency)
        {
            if (situation == null) throw new ArgumentNullException(nameof(situation));
            return goal switch
            {
                EnemyTacticalGoal.Bombard => ScoreBombard(situation, tendency),
                EnemyTacticalGoal.DefendShip => ScoreDefend(situation, tendency),
                EnemyTacticalGoal.BoardEnemy => ScoreBoard(situation),
                _ => throw new ArgumentOutOfRangeException(nameof(goal))
            };
        }

        /* {
            処理: 全目標を採点し、保持時間と点差の条件を満たす場合だけ目標を切り替えます。
            戻り値: 今回の判断後の目標です。生存船員がいない時は現在の目標を保持します。
        } */
        public EnemyTacticalGoal Update(EnemyTacticalSituation situation, PlayerTendency tendency, float now)
        {
            if (situation == null) throw new ArgumentNullException(nameof(situation));
            if (!UtilityRules.Range(now, changedAt, float.MaxValue)) throw new ArgumentOutOfRangeException(nameof(now));
            if (situation.AliveCrewCount == 0) return Goal;
            var best = SelectBestGoal(situation, tendency);
            if (best == Goal) return Goal;

            // 実行できない目標は待たずに捨て、実行できる目標同士は小さな点差で揺らがせません。
            var isHeldLongEnough = now - changedAt >= MinimumHoldSeconds;
            var isClearlyBetter = Score(best, situation, tendency) - Score(Goal, situation, tendency) >= SwitchMargin;
            if (IsFeasible(Goal, situation) && !(isHeldLongEnough && isClearlyBetter)) return Goal;
            Goal = best;
            changedAt = now;
            return Goal;
        }

        /* { 処理: 目標が戦況の上で実行可能か判定します。砲撃は相手の橋が閉じている間、乗り込みは開いている間だけ可能です。 } */
        private static bool IsFeasible(EnemyTacticalGoal goal, EnemyTacticalSituation situation) => goal switch
        {
            EnemyTacticalGoal.Bombard => !situation.OpponentHullBreached,
            EnemyTacticalGoal.BoardEnemy => situation.OpponentHullBreached,
            _ => true
        };

        /* { 処理: 最高点の目標を返します。同点では列挙値の小さい目標を選び、判断を決定的にします。 } */
        private static EnemyTacticalGoal SelectBestGoal(EnemyTacticalSituation situation, PlayerTendency tendency)
        {
            var best = EnemyTacticalGoal.Bombard;
            var bestScore = float.NegativeInfinity;
            foreach (EnemyTacticalGoal goal in Enum.GetValues(typeof(EnemyTacticalGoal)))
            {
                var score = Score(goal, situation, tendency);
                if (score <= bestScore) continue;
                best = goal;
                bestScore = score;
            }
            return best;
        }

        /* { 処理: 砲撃を採点します。自船体が危うい、相手の砲撃が速い、弾の供給がある場合に上げます。相手船体が破壊済みなら0です。 } */
        private static float ScoreBombard(EnemyTacticalSituation situation, PlayerTendency tendency)
        {
            // 重い弾を多用する相手には、同じ残存率でも船体の危険を大きく見積もります。
            if (situation.OpponentHullBreached) return 0;
            var supplyBonus = situation.IsCannonLoaded || situation.KnownAmmoCount > 0 ? 0.1f : 0;
            var hullDanger = Math.Min(1f, (1f - situation.OwnHullRatio) * (1f + 0.5f * tendency.HeavyAmmoRatio));
            var shellPace = Math.Min(1f, tendency.ShellsPerMinute / 6f);
            return 0.5f + supplyBonus + 0.3f * hullDanger + 0.1f * shellPace;
        }

        /* { 処理: 迎撃を採点します。観測した侵入者数、自船体の破壊、乗り込み傾向から上げます。 } */
        private static float ScoreDefend(EnemyTacticalSituation situation, PlayerTendency tendency)
        {
            var breachedBonus = situation.OwnHullBreached ? 0.3f : 0;
            return Math.Min(1f, 0.4f * situation.ObservedIntruderCount + breachedBonus + 0.3f * tendency.BoardingPressure);
        }

        /* { 処理: 乗り込みを採点します。相手の橋が開いている間だけ成立し、自船内の侵入者が増えるほど下げます。 } */
        private static float ScoreBoard(EnemyTacticalSituation situation)
        {
            if (!situation.OpponentHullBreached) return 0;
            return Math.Max(0f, 0.9f - 0.3f * situation.ObservedIntruderCount);
        }
    }
}
