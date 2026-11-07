using System;
using System.Collections.Generic;
using KadokaShipBattler.Ammo;

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

    /* { 責務: 弾同士の衝突で、通常の耐久処理を上書きする特殊効果の拡張口。例: 盾割り、反射、すり抜け、変化。 } */
    public interface IProjectileClashEffect
    {
        /* { 処理: 衝突直前に呼ばれます。引数: selfは効果を持つ弾、otherは相手。戻り値: 通常処理を上書きして済ませたらtrueです。補足: trueは双方の耐久処理を効果側が完了済みとみなします。falseは通常処理へ進み、Hitは飛行中でない弾の耐久を減らしません。falseを返す場合に弾の状態を変えてもよいですが、その場合の裁定は変更後の状態に対する通常処理になります。 } */
        bool PreClash(CharacterProjectileModel self, CharacterProjectileModel other);
    }

    /* {
        責務: 大砲から敵船へ飛ぶキャラクターを、通常の弾と同じ飛翔体として扱う純粋なモデルです。
        前提: 座標は秒と同じ単位の連続値で、衝突と到達の判定は呼び出し側の幾何から渡します。硬さは大小比較ではなく衝突耐久です。
        計算量: 1回の更新は定数時間です。
    } */
    public sealed class CharacterProjectileModel
    {
        private readonly float targetX;
        private readonly float targetY;
        private readonly ProjectileTrajectory trajectory;
        // 軌道上の進行度（0〜1）。
        private double progress;
        private readonly float speed;
        private readonly IProjectileBehaviour behaviour;
        private readonly IProjectileClashEffect clashEffect;
        // 現在重なっている相手。同じ接触を複数フレームで数えないために使います。
        private readonly HashSet<CharacterProjectileModel> contacts = new();

        public float X { get; private set; }
        public float Y { get; private set; }
        public int MaxHardness { get; }
        public int RemainingHardness { get; private set; }
        public float FlightSeconds { get; private set; }
        public ProjectileFlightStatus Status { get; private set; } = ProjectileFlightStatus.Flying;

        /* { 処理: 発射位置、着弾目標、速度、衝突耐久で飛翔を開始します。例外: 非有限値、速度0以下、耐久1未満を拒否します。 } */
        public CharacterProjectileModel(float startX, float startY, float targetX, float targetY, float speed,
            int hardness, IProjectileBehaviour behaviour = null, IProjectileClashEffect clashEffect = null,
            ProjectileTrajectoryKind trajectoryKind = ProjectileTrajectoryKind.Direct)
        {
            // 不正値は飛行中に位置を壊すため、構築時に弾きます。
            if (!IsFinite(startX) || !IsFinite(startY) || !IsFinite(targetX) || !IsFinite(targetY))
                throw new ArgumentOutOfRangeException(nameof(startX));
            if (!IsFinite(speed) || speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
            if (hardness < 1) throw new ArgumentOutOfRangeException(nameof(hardness));
            X = startX;
            Y = startY;
            this.targetX = targetX;
            this.targetY = targetY;
            this.speed = speed;
            MaxHardness = hardness;
            RemainingHardness = hardness;
            this.behaviour = behaviour;
            this.clashEffect = clashEffect;
            trajectory = new ProjectileTrajectory(trajectoryKind, startX, startY, targetX, targetY);
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
            // 軌道の長さに対する進行度を進め、1に届けば着地にします。
            if (trajectory.Length <= step || progress + step / trajectory.Length >= 1.0)
            {
                X = targetX;
                Y = targetY;
                Status = ProjectileFlightStatus.Landed;
                contacts.Clear();
                return Status;
            }
            progress += step / trajectory.Length;
            var position = trajectory.PositionAt(progress);
            X = (float)position.X;
            Y = (float)position.Y;
            return Status;
        }

        /* { 処理: 衝突耐久を1減らし、0以下なら撃墜します。戻り値: この呼び出しで撃墜されたらtrueです。 } */
        public bool Hit()
        {
            if (Status != ProjectileFlightStatus.Flying) return false;
            RemainingHardness--;
            if (RemainingHardness > 0) return false;
            Destroy();
            return true;
        }

        /* { 処理: 耐久に関係なく撃墜します。特殊効果の上書き処理から使います。 } */
        public void Destroy()
        {
            if (Status != ProjectileFlightStatus.Flying) return;
            Status = ProjectileFlightStatus.Destroyed;
            contacts.Clear();
        }

        /* {
            処理: 2つの弾の当たり判定の重なりを渡し、重なり始めの1回だけ衝突を裁定します。重なりが外れると次の接触を数え直します。
            引数: left、rightは弾、overlappingは実際に2D上で当たり判定が重なっているか。戻り値: なし。例外: nullを拒否します。
        } */
        public static void UpdateContact(CharacterProjectileModel left, CharacterProjectileModel right, bool overlapping)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (ReferenceEquals(left, right)) return;
            if (!overlapping)
            {
                left.contacts.Remove(right);
                right.contacts.Remove(left);
                return;
            }
            if (left.Status != ProjectileFlightStatus.Flying || right.Status != ProjectileFlightStatus.Flying) return;
            // 同じ2つの接触を複数フレームで数えません。
            if (!left.contacts.Add(right)) return;
            right.contacts.Add(left);
            // 特殊効果は通常の耐久処理を上書きできます。
            if (left.clashEffect != null && left.clashEffect.PreClash(left, right)) return;
            if (right.clashEffect != null && right.clashEffect.PreClash(right, left)) return;
            left.Hit();
            right.Hit();
        }

        /* { 処理: 通常弾1発の命中を受けて耐久を1減らします。戻り値: 生き残ればtrueです。 } */
        public bool HitByShell()
        {
            if (Status != ProjectileFlightStatus.Flying) return false;
            Hit();
            return Status == ProjectileFlightStatus.Flying;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
