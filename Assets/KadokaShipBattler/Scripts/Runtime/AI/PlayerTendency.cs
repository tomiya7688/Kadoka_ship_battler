using System;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 観測から推定したプレイヤーの行動傾向を、不変の値として戦術判断へ渡します。
        前提: 既定値はデータ不足を表し、全項目が0です。弾種の割合は標本数が足りるまで0のままです。
    } */
    public readonly struct PlayerTendency
    {
        public float ShellsPerMinute { get; }
        public float BoardingPressure { get; }
        public int SampledAmmoCount { get; }
        public float HeavyAmmoRatio { get; }

        /* {
            処理: 推定値を検証して保持します。
            引数: shellsPerMinuteは毎分の被弾数。boardingPressureとheavyAmmoRatioは0以上1以下。
            例外: 範囲外・非有限の値を拒否します。
        } */
        public PlayerTendency(float shellsPerMinute, float boardingPressure, int sampledAmmoCount, float heavyAmmoRatio)
        {
            // 推定器以外から不正な傾向値を持ち込ませません。
            if (!UtilityRules.Range(shellsPerMinute, 0, float.MaxValue) || !UtilityRules.Range(boardingPressure, 0, 1) ||
                sampledAmmoCount < 0 || !UtilityRules.Range(heavyAmmoRatio, 0, 1))
                throw new ArgumentException("Invalid player tendency.");
            ShellsPerMinute = shellsPerMinute;
            BoardingPressure = boardingPressure;
            SampledAmmoCount = sampledAmmoCount;
            HeavyAmmoRatio = heavyAmmoRatio;
        }
    }
}
