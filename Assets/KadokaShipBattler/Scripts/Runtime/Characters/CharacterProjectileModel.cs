using System;

namespace KadokaShipBattler.Characters
{
    /* { 責務: 発射されたキャラクターの飛行状態。 } */
    public enum ProjectileFlightStatus
    {
        Flying,
        Landed,
        Destroyed
    }

    /* { 責務: キャラクター固有の発射中挙動を差し込む拡張口。前提: 実装は飛行の速度だけを変え、状態は直接変えません。 } */
    public interface IProjectileBehaviour
    {
        /* { 処理: 飛行速度の倍率を返します。戻り値: 0以上の有限値です。 } */
        float GetSpeedMultiplier(float flightSeconds);
    }

    /* {
        責務: 大砲から敵船へ飛ぶキャラクターを、通常の弾と同じ飛翔体として扱う純粋なモデルです。
        前提: 座標は秒と同じ単位の連続値で、衝突と到達の判定は呼び出し側の幾何から渡します。
        計算量: 1回の更新は定数時間です。
    } */
    public sealed class CharacterProjectileModel
    {
        private readonly float targetX;
        private readonly float targetY;
        private readonly float speed;
        private readonly IProjectileBehaviour behaviour;

        public float X { get; private set; }
        public float Y { get; private set; }
        public float Hardness { get; }
        public float FlightSeconds { get; private set; }
        public ProjectileFlightStatus Status { get; private set; } = ProjectileFlightStatus.Flying;

        /* { 処理: 発射位置、着弾目標、速度、弾としての硬さで飛翔を開始します。例外: 非有限値、負の値、速度0を拒否します。 } */
        public CharacterProjectileModel(float startX, float startY, float targetX, float targetY, float speed,
            float hardness, IProjectileBehaviour behaviour = null)
        {
            // 不正値は飛行中に位置を壊すため、構築時に弾きます。
            if (!IsFinite(startX) || !IsFinite(startY) || !IsFinite(targetX) || !IsFinite(targetY))
                throw new ArgumentOutOfRangeException(nameof(startX));
            if (!IsFinite(speed) || speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
            if (!IsFinite(hardness) || hardness < 0) throw new ArgumentOutOfRangeException(nameof(hardness));
            X = startX;
            Y = startY;
            this.targetX = targetX;
            this.targetY = targetY;
            this.speed = speed;
            Hardness = hardness;
            this.behaviour = behaviour;
        }

        /* { 処理: 飛行を進め、目標に届けば着地にします。引数: deltaTimeは有限の正の秒数です。戻り値: 現在の状態です。 } */
        public ProjectileFlightStatus Advance(float deltaTime)
        {
            if (!IsFinite(deltaTime) || deltaTime <= 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (Status != ProjectileFlightStatus.Flying) return Status;
            FlightSeconds += deltaTime;
            var multiplier = behaviour?.GetSpeedMultiplier(FlightSeconds) ?? 1f;
            if (!IsFinite(multiplier) || multiplier < 0) multiplier = 1f;
            // 極端に大きい座標や速度でもfloatの範囲を超えないよう、中間計算はdoubleで行います。
            var step = (double)speed * multiplier * deltaTime;
            var dx = (double)targetX - X;
            var dy = (double)targetY - Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance <= step)
            {
                X = targetX;
                Y = targetY;
                Status = ProjectileFlightStatus.Landed;
                return Status;
            }
            X = (float)(X + dx / distance * step);
            Y = (float)(Y + dy / distance * step);
            return Status;
        }

        /* { 処理: 別の飛翔体との衝突を硬さで裁定します。硬い方が残り、同じ硬さなら両方落ちます。 } */
        public static void Clash(CharacterProjectileModel left, CharacterProjectileModel right)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (left.Status != ProjectileFlightStatus.Flying || right.Status != ProjectileFlightStatus.Flying) return;
            if (left.Hardness <= right.Hardness) left.Status = ProjectileFlightStatus.Destroyed;
            if (right.Hardness <= left.Hardness) right.Status = ProjectileFlightStatus.Destroyed;
        }

        /* { 処理: 通常の弾の硬さと衝突させます。通常弾の硬さ以下なら撃ち落とされ、より硬ければ弾だけが壊れます。戻り値: 自分が生き残ればtrueです。 } */
        public bool ClashWithShell(float shellHardness)
        {
            if (!IsFinite(shellHardness) || shellHardness < 0) throw new ArgumentOutOfRangeException(nameof(shellHardness));
            if (Status != ProjectileFlightStatus.Flying) return false;
            if (Hardness <= shellHardness) Status = ProjectileFlightStatus.Destroyed;
            return Status == ProjectileFlightStatus.Flying;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
