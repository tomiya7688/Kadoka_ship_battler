using System;

namespace KadokaShipBattler.AI
{
    /* { 責務: 予約する資源の種類を区別します。前提: 予約キーに従属する小さな列挙型です。 } */
    public enum TaskTargetKind { Ammo, Cannon, Invader }

    /* {
        責務: 行動名が異なっても同じ資源を一つの予約対象として識別します。
        前提: 砲台の装填と発射は同じキーを使います。Unity参照を保持しません。
    } */
    public readonly struct TaskTarget : IEquatable<TaskTarget>
    {
        public TaskTargetKind Kind { get; }
        public int TargetId { get; }

        /* { 処理: 種類と戦闘内の対象IDを検証して保持します。例外: 不明な種類と未指定IDを拒否します。 } */
        public TaskTarget(TaskTargetKind kind, int targetId)
        {
            // UnityのインスタンスIDは負数も使用します。0だけを未指定として拒否します。
            if ((uint)kind > (uint)TaskTargetKind.Invader || targetId == 0) throw new ArgumentException("Invalid reservation target.");
            Kind = kind;
            TargetId = targetId;
        }

        /* { 処理: 実行中の行動候補を予約資源へ変換します。戻り値: 待機や未実装の行動はfalseです。 } */
        public static bool TryFromCandidate(UtilityCandidate candidate, out TaskTarget target)
        {
            // 装填と発射の担当が同じ砲台へ同時に集まることを防ぎます。
            target = default;
            if (candidate == null || candidate.TargetId == 0) return false;
            var kind = candidate.Action switch
            {
                AiActionType.CarryAmmo => TaskTargetKind.Ammo,
                AiActionType.LoadCannon or AiActionType.OperateCannon => TaskTargetKind.Cannon,
                AiActionType.DefendShip => TaskTargetKind.Invader,
                _ => (TaskTargetKind)(-1)
            };
            if ((int)kind < 0) return false;
            target = new TaskTarget(kind, candidate.TargetId);
            return true;
        }

        /* { 処理: 同じ資源種別とIDか比較します。 } */
        public bool Equals(TaskTarget other) => Kind == other.Kind && TargetId == other.TargetId;
        /* { 処理: 型が一致する場合に予約キーを比較します。 } */
        public override bool Equals(object other) => other is TaskTarget target && Equals(target);
        /* { 処理: 辞書の資源検索に使うハッシュ値を返します。 } */
        public override int GetHashCode() => unchecked((int)Kind * 397 ^ TargetId);
    }
}
