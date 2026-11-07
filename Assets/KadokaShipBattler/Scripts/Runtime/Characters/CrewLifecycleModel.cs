using System;

namespace KadokaShipBattler.Characters
{
    /* { 責務: 船員の戦闘状態。Activeは行動可能、Downedは倒れた直後、Respawningは復帰待ちです。 } */
    public enum CrewLifecycleState
    {
        Active,
        Downed,
        Respawning
    }

    /* {
        責務: 船員の戦闘不能と復帰を、通常攻撃・侵入先での敗北・キャラクター弾の撃墜で共通に扱う純粋な状態機械です。
        前提: 復帰時間は呼び出し側がデータから渡します。コア破壊後は復帰しません。復帰待ち中の船員はAI・操作切替・タスク予約の対象外です。
        フィールド: State=現在の状態、RemainingSeconds=復帰までの残り秒数、RespawnSeconds=復帰時間、RespawnDisabled=コア破壊後で復帰しない。
        計算量: 1回の更新は定数時間です。
    } */
    public sealed class CrewLifecycleModel
    {
        private readonly Action onDowned;
        private readonly Action onRespawned;

        public CrewLifecycleState State { get; private set; } = CrewLifecycleState.Active;
        public float RespawnSeconds { get; }
        public float RemainingSeconds { get; private set; }
        public bool RespawnDisabled { get; private set; }

        // AI・操作切替・タスク予約・攻撃の対象にできるのは、行動可能な間だけです。
        public bool IsTargetable => State == CrewLifecycleState.Active;

        /* { 処理: 状態機械を作ります。引数: respawnSecondsは0以上の有限値、onDownedは倒れた時に1回だけ呼ぶ後始末（例外を投げない契約。投げた場合も状態は倒れたままで、二重実行はされません）（制御停止・予約解放・所持弾処理・飛行終了・操作対象の切替）、onRespawnedは復帰時に1回呼ぶ処理（自船のスポーン位置への配置とHP全回復）。例外: 不正な復帰時間を拒否します。 } */
        public CrewLifecycleModel(float respawnSeconds, Action onDowned = null, Action onRespawned = null)
        {
            if (float.IsNaN(respawnSeconds) || float.IsInfinity(respawnSeconds) || respawnSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(respawnSeconds));
            RespawnSeconds = respawnSeconds;
            this.onDowned = onDowned;
            this.onRespawned = onRespawned;
        }

        /* { 処理: 戦闘不能にします。行動可能な時だけ受け付け、後始末を1回だけ実行します。戻り値: 新たに倒れたらtrueです。 } */
        public bool Down()
        {
            if (State != CrewLifecycleState.Active) return false;
            State = CrewLifecycleState.Downed;
            RemainingSeconds = RespawnSeconds;
            onDowned?.Invoke();
            return true;
        }

        /* { 処理: コアが破壊されたことを通知します。以後は復帰せず、倒れたままになります。 } */
        public void DisableRespawn() => RespawnDisabled = true;

        /* { 処理: 時間を進めます。倒れた直後は復帰待ちへ移り、残り時間が尽きたら復帰します。引数: deltaTimeは有限の正の秒数。例外: 不正な値を拒否します。 } */
        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (State == CrewLifecycleState.Active || RespawnDisabled) return;
            if (State == CrewLifecycleState.Downed) State = CrewLifecycleState.Respawning;
            RemainingSeconds = Math.Max(0, RemainingSeconds - deltaTime);
            if (RemainingSeconds > 0) return;
            // 復帰処理が成功してから行動可能にします。失敗（例外）した場合は復帰待ちのまま残り、次の更新で再試行されます。
            onRespawned?.Invoke();
            State = CrewLifecycleState.Active;
        }
    }
}
