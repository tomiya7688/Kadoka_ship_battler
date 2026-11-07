using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 船員が選ぶ候補と、チーム内の現在の担当を結び付けます。
        前提: 直接操作中の船員は予約しません。引継ぎ後は実行直前の確認で旧担当を停止します。
    } */
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCrewController))]
    public sealed class CrewTaskAssignment : MonoBehaviour
    {
        public const float ReservationDuration = 2;
        public PlayerCrewController Actor { get; private set; }
        public int OwnerId { get; private set; }
        public CrewTeamCoordinator Coordinator { get; private set; }
        public TaskReservation? CurrentReservation { get; private set; }
        private TaskTarget? blockedTarget;
        private float retryAt;
        private CrewAmmoAiController execution;
        private UtilityAiAgent utility;
        private CrewPolicySelection policySelection;
        public VisionSensor Sensor { get; private set; }

        public bool CanReserve => Actor != null && Actor.IsAvailable && !Actor.IsDirectlyControlled &&
            isActiveAndEnabled && Actor.Arena != null && !Actor.Arena.IsFinished && Actor.Arena.AllyActionsEnabled &&
            (Coordinator == null || Coordinator.isActiveAndEnabled) &&
            execution is { isActiveAndEnabled: true, IsRunning: true } &&
            utility is { isActiveAndEnabled: true };

        /* { 処理: 船員IDと状態の接続先を取得します。 } */
        private void Awake()
        {
            // 破棄時にはActorが先に無効になるため、登録IDを値として保持します。
            Actor = GetComponent<PlayerCrewController>();
            OwnerId = Actor.GetInstanceID();
        }

        /* { 処理: シーン生成後にチーム共有へ登録します。 } */
        private void Start() => EnsureConnected();

        /* { 処理: 本人のセンサーが今回取得した観測だけを共有します。 } */
        public void PublishPerception()
        {
            // 共有観測を本人の新しい観測として再報告しません。
            EnsureConnected();
            Coordinator?.Publish(this);
        }

        /* { 処理: 自分の優先度で候補の資源を予約できるか確認します。戻り値: 別の同等以上の担当がいればfalseです。 } */
        public bool CanConsider(UtilityCandidate candidate, float hullFraction, bool hasObservedInvader)
        {
            // 直接操作の評価表示には予約を作りません。
            if (!CanReserve || Coordinator == null || !TaskTarget.TryFromCandidate(candidate, out var target)) return true;
            if (blockedTarget.HasValue && blockedTarget.Value.Equals(target) && Time.time < retryAt) return false;
            return Coordinator.Reservations.CanClaim(target, OwnerId,
                GetPriority(candidate.Action, hullFraction, hasObservedInvader), Time.time);
        }

        /* { 処理: 選択した候補の担当を確保します。戻り値: 引継ぎなどで取得できなければfalseです。 } */
        public bool TryAssign(UtilityCandidate candidate, float hullFraction, bool hasObservedInvader)
        {
            // 待機、直接操作、停止では、以前の担当を解放します。
            if (!CanReserve || Coordinator == null || !TaskTarget.TryFromCandidate(candidate, out var target))
            {
                Release();
                return true;
            }
            if (!Coordinator.Reservations.TryClaim(target, OwnerId,
                GetPriority(candidate.Action, hullFraction, hasObservedInvader), Time.time, ReservationDuration, out var reservation)) return false;
            CurrentReservation = reservation;
            return true;
        }

        /* { 処理: 移動・取得・攻撃の直前に現在の担当を確認します。戻り値: 直接操作評価以外の予約なしはfalseです。 } */
        public bool IsCurrent()
        {
            // 上書き、期限切れ、停止中の予約で行動しません。
            return CanReserve && Coordinator != null && CurrentReservation.HasValue &&
                Coordinator.Reservations.IsCurrent(CurrentReservation.Value, Time.time);
        }

        /* { 処理: 現在の担当だけを解放します。前提: 観測と方針と学習は保持します。 } */
        public void Release()
        {
            // 上書きした新担当の予約を旧担当から消すことはありません。
            if (Coordinator != null) Coordinator.Reservations.ReleaseOwner(OwnerId);
            CurrentReservation = null;
        }

        /* { 処理: 経路失敗した対象を短時間避け、予約を解放します。 } */
        public void RejectBlockedTarget()
        {
            // 到達失敗を毎フレーム再取得するループから抜けます。
            if (CurrentReservation.HasValue)
            {
                blockedTarget = CurrentReservation.Value.Target;
                retryAt = Time.time + 0.5f;
            }
            Release();
        }

        /* { 処理: 取得成功や再観測失敗で分かった対象の無効化を通知します。 } */
        public void ForgetCurrentTarget()
        {
            // 予約のない対象を、推測で共有から削除しません。
            if (CurrentReservation.HasValue && Coordinator != null) Coordinator.ForgetTarget(CurrentReservation.Value.Target);
            Release();
        }

        /* {
            処理: 方針による専門担当と、防衛の緊急度から引継ぎ優先度を求めます。
            戻り値: 通常0、専門1、緊急防衛2、専門の緊急防衛3です。同点は既存担当を保持します。
        } */
        private int GetPriority(AiActionType action, float hullFraction, bool hasObservedInvader)
        {
            // 得点の小さな変動を予約の奪い合いへ変換しません。
            var director = policySelection.Director;
            var policy = director != null ? director.GetIndividualPolicy(Actor) : AiPolicyId.Automatic;
            var isSpecialist = action switch
            {
                AiActionType.CarryAmmo or AiActionType.LoadCannon => policy == AiPolicyId.Supply || policy == AiPolicyId.MobileSupply,
                AiActionType.OperateCannon => policy == AiPolicyId.Gunner,
                AiActionType.DefendShip => policy == AiPolicyId.Guard,
                _ => false
            };
            var priority = isSpecialist ? 1 : 0;
            if (action == AiActionType.DefendShip && (hasObservedInvader || hullFraction < 0.25f)) priority += 2;
            return priority;
        }

        /* { 処理: 同じ戦闘に一つの共有台帳を確保して船員を登録します。 } */
        private void EnsureConnected()
        {
            // RequireComponentの追加中には実行器が未生成の場合があります。報告開始時に接続します。
            if (execution == null) execution = GetComponent<CrewAmmoAiController>();
            if (utility == null) utility = GetComponent<UtilityAiAgent>();
            if (policySelection == null) policySelection = GetComponent<CrewPolicySelection>();
            if (Sensor == null) Sensor = GetComponent<VisionSensor>();
            // Arena設定前や敵陣営にプレイヤーの共有台帳を作りません。
            if (Coordinator != null || Actor.Arena == null || Actor.TeamSide != TeamSide.Player) return;
            Coordinator = Actor.Arena.GetComponent<CrewTeamCoordinator>();
            if (Coordinator == null) Coordinator = Actor.Arena.gameObject.AddComponent<CrewTeamCoordinator>();
            Coordinator.Register(this);
        }

        /* { 処理: コンポーネントや船員の無効化時に現在の担当と報告を解除します。 } */
        private void OnDisable()
        {
            // 再有効化しても古い予約のまま再開しません。
            Release();
            if (Coordinator != null) Coordinator.Observations.ClearReporter(OwnerId);
        }

        /* { 処理: 破棄される船員への登録参照をチームから除きます。 } */
        private void OnDestroy()
        {
            // 戦闘側が先に破棄された場合には参照しません。
            if (Coordinator != null) Coordinator.Unregister(this);
        }
    }
}
