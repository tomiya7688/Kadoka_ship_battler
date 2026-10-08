namespace KadokaShipBattler.AI
{
    /* {
        責務: 担当者、優先度、有効期限、世代を持つ不変の予約値を表します。
        前提: 予約台帳だけが生成します。世代により、再取得前の古い担当を区別します。
    } */
    public readonly struct TaskReservation
    {
        public TaskTarget Target { get; }
        public int OwnerId { get; }
        public int Priority { get; }
        public float ExpiresAt { get; }
        public long Generation { get; }

        /* { 処理: 台帳が検証した予約内容を保持します。前提: 同じ担当の更新では世代を保持します。 } */
        internal TaskReservation(TaskTarget target, int ownerId, int priority, float expiresAt, long generation)
        {
            // 値だけを保持し、船員や対象の生存参照を公開しません。
            Target = target;
            OwnerId = ownerId;
            Priority = priority;
            ExpiresAt = expiresAt;
            Generation = generation;
        }
    }
}
