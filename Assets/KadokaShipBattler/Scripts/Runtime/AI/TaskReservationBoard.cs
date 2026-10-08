using System;
using System.Collections.Generic;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 一つのチームの作業担当を、資源ごとに一人・船員ごとに一件だけ保持します。
        前提: 同じ戦闘の単調な秒時計を使用します。Unityの主スレッドから同期的に呼びます。
        計算量: 個別の確認・取得・解放は平均O(1)。期限回収は予約数に比例します。
    } */
    public sealed class TaskReservationBoard
    {
        private readonly Dictionary<TaskTarget, TaskReservation> reservations = new();
        private readonly Dictionary<int, TaskTarget> ownerTargets = new();
        private readonly List<int> expiredOwners = new();
        private long nextGeneration;
        public int Count => reservations.Count;

        /* { 処理: 予約を取得可能か確認します。戻り値: 空き、期限切れ、自分の担当、高優先度ならtrueです。 } */
        public bool CanClaim(TaskTarget target, int ownerId, int priority, float now)
        {
            // 同点では先に確保した担当を保持し、毎フレームの奪い合いを防ぎます。
            ValidateRequest(target, ownerId, priority, now);
            return !reservations.TryGetValue(target, out var current) || current.ExpiresAt <= now ||
                current.OwnerId == ownerId || priority > current.Priority;
        }

        /* {
            処理: 取得可能な対象へ担当を変更し、有効期限を更新します。
            戻り値: 取得不可の場合はfalseで、以前の担当を保持します。
            前提: 期限は0より大きく60秒以下です。高優先度の上書きは旧担当も同時に解除します。
        } */
        public bool TryClaim(TaskTarget target, int ownerId, int priority, float now, float duration,
            out TaskReservation reservation)
        {
            // 入力拒否や取得失敗で、以前の作業を部分的に失いません。
            reservation = default;
            if (!UtilityRules.Range(duration, float.Epsilon, 60) || !UtilityRules.Finite(now + duration) || now + duration <= now)
                throw new ArgumentOutOfRangeException(nameof(duration));
            if (!CanClaim(target, ownerId, priority, now)) return false;
            reservations.TryGetValue(target, out var previous);
            var isRenewal = previous.OwnerId == ownerId && previous.ExpiresAt > now;
            var generation = isRenewal ? previous.Generation : checked(nextGeneration + 1);
            if (isRenewal)
            {
                // 同じ担当の毎フレームの更新では、逆引きの削除と追加を繰り返しません。
                reservation = new TaskReservation(target, ownerId, priority, now + duration, generation);
                reservations[target] = reservation;
                return true;
            }

            // 対象と担当者の逆引きを同時に変更し、古い担当を残しません。
            ReleaseOwner(ownerId);
            if (previous.OwnerId != 0) ReleaseOwner(previous.OwnerId);
            nextGeneration = generation;
            reservation = new TaskReservation(target, ownerId, priority, now + duration, generation);
            reservations[target] = reservation;
            ownerTargets[ownerId] = target;
            return true;
        }

        /* { 処理: 実行直前に同じ担当と世代がまだ有効か確認します。戻り値: 上書き、期限切れ、解放後はfalseです。 } */
        public bool IsCurrent(TaskReservation reservation, float now)
        {
            // 同じ船員が同じ対象を取り直しても、古い予約による実行を認めません。
            ValidateTime(now);
            return reservations.TryGetValue(reservation.Target, out var current) && current.ExpiresAt > now &&
                current.OwnerId == reservation.OwnerId && current.Generation == reservation.Generation;
        }

        /* { 処理: 船員の担当を解放します。戻り値: 担当が存在した場合にtrueです。 } */
        public bool ReleaseOwner(int ownerId)
        {
            // 既に別の船員へ引き継いだ対象を、旧担当の後始末で消しません。
            if (!ownerTargets.TryGetValue(ownerId, out var target)) return false;
            ownerTargets.Remove(ownerId);
            if (reservations.TryGetValue(target, out var current) && current.OwnerId == ownerId) reservations.Remove(target);
            return true;
        }

        /* { 処理: 対象の消滅や取得を検知した時に担当を解放します。 } */
        public void ReleaseTarget(TaskTarget target)
        {
            // 別の船員による取得も、次の評価で新しい対象を選ぶ契機にします。
            if (reservations.TryGetValue(target, out var current)) ReleaseOwner(current.OwnerId);
        }

        /* { 処理: 更新されなくなった予約をまとめて回収します。 } */
        public void Expire(float now)
        {
            // 辞書の走査中に変更せず、作業リストを再利用します。
            ValidateTime(now);
            expiredOwners.Clear();
            foreach (var entry in reservations)
                if (entry.Value.ExpiresAt <= now) expiredOwners.Add(entry.Value.OwnerId);
            foreach (var owner in expiredOwners) ReleaseOwner(owner);
        }

        /* { 処理: 戦闘終了時に全ての担当を解放します。 } */
        public void Clear()
        {
            // 世代番号は再使用せず、終了前の予約値を無効にします。
            reservations.Clear();
            ownerTargets.Clear();
        }

        /* { 処理: 時刻の有限性と非負条件を確認します。例外: 不正な時刻を拒否します。 } */
        private static void ValidateTime(float now)
        {
            // NaNを期限比較へ持ち込みません。
            if (!UtilityRules.Range(now, 0, float.MaxValue)) throw new ArgumentOutOfRangeException(nameof(now));
        }

        /* { 処理: 対象、担当ID、優先度、時刻を確認します。例外: 未指定キーと範囲外の優先度を拒否します。 } */
        private static void ValidateRequest(TaskTarget target, int ownerId, int priority, float now)
        {
            // defaultの構造体も、正規の予約キーとして扱いません。
            ValidateTime(now);
            if (target.TargetId == 0 || (uint)target.Kind > (uint)TaskTargetKind.Invader || ownerId == 0 || priority < 0 || priority > 3)
                throw new ArgumentException("Invalid reservation request.");
        }
    }
}
