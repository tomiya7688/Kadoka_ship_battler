using System;
using System.Collections.Generic;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 自船が受けた砲撃、観測した相手の弾、観測した侵入者から、プレイヤーの傾向を推定します。
        前提: 入力は敵チームが実際に得た情報だけです。時刻は単調に増える秒数で、同じ時刻の複数記録を許可します。
        計算量: 砲撃記録は最大32件、弾の標本は最大256件で、推定はこの上限に比例します。
    } */
    public sealed class PlayerTendencyEstimator
    {
        public const float ShellWindowSeconds = 60;
        public const float MinimumShellWindowSeconds = 10;
        public const float BoardingTimeConstantSeconds = 10;
        public const float HeavyAmmoWeight = 3;
        public const int MinimumAmmoSamples = 3;
        private const int MaximumShellRecords = 32;
        private const int MaximumAmmoSamples = 256;
        private readonly Queue<float> shellTimes = new();
        private readonly Dictionary<int, bool> isHeavyByAmmoId = new();
        private readonly float startedAt;
        private float latestTime;
        private float boardingPressure;
        private int heavyAmmoCount;

        /* { 処理: 推定の開始時刻を設定します。例外: 非有限の時刻を拒否します。 } */
        public PlayerTendencyEstimator(float startedAt)
        {
            // 戦闘の開始前の時刻を標本に混ぜません。
            if (!UtilityRules.Range(startedAt, 0, float.MaxValue)) throw new ArgumentOutOfRangeException(nameof(startedAt));
            this.startedAt = latestTime = startedAt;
        }

        /* { 処理: 自船体が砲撃を受けた時刻を記録します。前提: 古い記録は上限を超えた時に捨てます。 } */
        public void RecordShellHit(float now)
        {
            RequireTime(now);
            shellTimes.Enqueue(now);
            if (shellTimes.Count > MaximumShellRecords) shellTimes.Dequeue();
        }

        /* {
            処理: 観測した相手陣営の弾を一度だけ標本へ加えます。
            引数: ammoIdは弾の個体ID。weightは観測した重量で、3以上を重い弾とします。
            戻り値: 新しい標本を加えた時だけtrueです。既知の弾と上限超過はfalseです。
        } */
        public bool RecordObservedAmmo(int ammoId, float weight)
        {
            // 同じ弾を何度も見ても、傾向を偏らせません。
            if (ammoId == 0 || !UtilityRules.Range(weight, 0, float.MaxValue)) throw new ArgumentException("Invalid observed ammo.");
            if (isHeavyByAmmoId.ContainsKey(ammoId) || isHeavyByAmmoId.Count >= MaximumAmmoSamples) return false;
            var isHeavy = weight >= HeavyAmmoWeight;
            isHeavyByAmmoId.Add(ammoId, isHeavy);
            if (isHeavy) heavyAmmoCount++;
            return true;
        }

        /* {
            処理: 自船内で観測している侵入者数から、乗り込みの圧力を時間減衰付きで更新します。
            引数: intruderCountは現在観測している侵入者数で、2人以上を最大の圧力とします。
        } */
        public void RecordIntruderPresence(float now, int intruderCount)
        {
            // 圧力は観測が続く間だけ上がり、観測がなければ約10秒の時定数で下がります。
            if (intruderCount < 0) throw new ArgumentOutOfRangeException(nameof(intruderCount));
            var elapsed = now - latestTime;
            RequireTime(now);
            var target = Math.Min(1f, intruderCount / 2f);
            var blend = 1f - (float)Math.Exp(-elapsed / BoardingTimeConstantSeconds);
            boardingPressure += (target - boardingPressure) * blend;
        }

        /* { 処理: 現在の推定値を作ります。例外: 開始時刻より前の時刻を拒否します。 } */
        public PlayerTendency GetTendency(float now)
        {
            // 取得は状態を変更せず、同じ入力から同じ値を返します。
            if (!UtilityRules.Range(now, startedAt, float.MaxValue)) throw new ArgumentOutOfRangeException(nameof(now));
            var window = Math.Min(ShellWindowSeconds, Math.Max(MinimumShellWindowSeconds, now - startedAt));
            var recentShells = 0;
            foreach (var shellTime in shellTimes) if (shellTime > now - window) recentShells++;
            var sampled = isHeavyByAmmoId.Count;
            var heavyRatio = sampled >= MinimumAmmoSamples ? (float)heavyAmmoCount / sampled : 0;
            return new PlayerTendency(recentShells * 60f / window, Math.Min(1f, Math.Max(0f, boardingPressure)), sampled, heavyRatio);
        }

        /* { 処理: 記録時刻が有限で、過去へ戻らないことを確認して最新時刻を進めます。 } */
        private void RequireTime(float now)
        {
            // 時刻の逆行で減衰と窓の計算を壊しません。
            if (!UtilityRules.Range(now, latestTime, float.MaxValue)) throw new ArgumentOutOfRangeException(nameof(now));
            latestTime = now;
        }
    }
}
