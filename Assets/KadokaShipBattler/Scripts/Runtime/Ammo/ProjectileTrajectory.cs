using System;

namespace KadokaShipBattler.Ammo
{
    /* { 責務: 弾道の種類。Directはほぼ直線、Arcは2D上で山なりの曲線を描きます。 } */
    public enum ProjectileTrajectoryKind
    {
        Direct,
        Arc
    }

    /* {
        責務: 発射位置から目標までの2D軌道を、進行度0〜1の関数として返す純粋なモデルです。
        前提: 軌道の違いは2D上の通り道の違いだけで表し、高度による衝突の有無は使いません。
        計算量: 位置の取得は定数時間です。
    } */
    public sealed class ProjectileTrajectory
    {
        // 山なりの頂点の高さを、発射位置と目標の距離に対する割合で決めます。
        public const double ArcHeightRatio = 0.35;
        private readonly double startX;
        private readonly double startY;
        private readonly double targetX;
        private readonly double targetY;
        private readonly double arcHeight;

        public ProjectileTrajectoryKind Kind { get; }
        public double Length { get; }

        /* { 処理: 軌道を作ります。引数: kindは弾道の種類、start/targetは有限の2D座標。例外: 非有限値と未定義の種類を拒否します。 } */
        public ProjectileTrajectory(ProjectileTrajectoryKind kind, float startX, float startY, float targetX, float targetY)
        {
            if (!Enum.IsDefined(typeof(ProjectileTrajectoryKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!IsFinite(startX)) throw new ArgumentOutOfRangeException(nameof(startX));
            if (!IsFinite(startY)) throw new ArgumentOutOfRangeException(nameof(startY));
            if (!IsFinite(targetX)) throw new ArgumentOutOfRangeException(nameof(targetX));
            if (!IsFinite(targetY)) throw new ArgumentOutOfRangeException(nameof(targetY));
            Kind = kind;
            this.startX = startX;
            this.startY = startY;
            this.targetX = targetX;
            this.targetY = targetY;
            // 極端に大きい座標でも溢れないよう、距離はdoubleで求めます。
            var dx = this.targetX - this.startX;
            var dy = this.targetY - this.startY;
            Length = Math.Sqrt(dx * dx + dy * dy);
            arcHeight = kind == ProjectileTrajectoryKind.Arc ? Length * ArcHeightRatio : 0;
            // 山なりの頂点がfloatの範囲を超えると位置が無限大になるため、構築時に拒否します。
            if (Math.Abs(this.startY) + Math.Abs(this.targetY) + 4 * arcHeight > float.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(kind), "Arc height exceeds the float range.");
        }

        /* { 処理: 進行度に対する2D位置を返します。引数: progressは0〜1に丸めます。戻り値: (X, Y)です。 } */
        public (double X, double Y) PositionAt(double progress)
        {
            var u = Math.Min(1.0, Math.Max(0.0, double.IsNaN(progress) ? 0.0 : progress));
            // 直線上の位置に、Arcだけ上向きの放物線の高さ 4h·u(1-u) を足します。
            return (startX + (targetX - startX) * u, startY + (targetY - startY) * u + 4 * arcHeight * u * (1 - u));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
