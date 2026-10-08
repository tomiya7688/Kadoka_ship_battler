using KadokaShipBattler.Ammo;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Navigation;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 選んだ弾運搬、砲台操作、防衛を、予約と本人の視界を確認して実行します。
        前提: 移動経路と観測は、それぞれの共通コンポーネントに任せます。
    } */
    [RequireComponent(typeof(PlayerCrewController), typeof(VisionSensor), typeof(UtilityAiAgent))]
    public sealed class CrewAmmoAiController : MonoBehaviour
    {
        private PlayerCrewController actor;
        private UtilityAiAgent utility;
        private CrewTaskAssignment assignment;
        private NavigationAgent navigation;
        private VisionSensor sensor;
        private CrewMember crew;
        private CrewAmmoInventory inventory;
        private float interactionTimer;
        private float searchTimer;
        private VisionObservation visibleObservation;
        public bool IsRunning { get; private set; }
        public Vector2 ObservationOrigin { get; private set; }
        public Vector2 ObservationFacing { get; private set; }
        public AmmoPickup VisibleAmmo { get; private set; }
        public AiActionType CurrentAction { get; private set; } = AiActionType.Idle;

        /* { 処理: 実行に使用するコンポーネントを一度だけ取得します。 } */
        private void Awake()
        {
            // 毎フレーム、同じ参照を探し直しません。
            actor = GetComponent<PlayerCrewController>();
            utility = GetComponent<UtilityAiAgent>();
            assignment = GetComponent<CrewTaskAssignment>();
            navigation = GetComponent<NavigationAgent>();
            sensor = GetComponent<VisionSensor>();
            crew = GetComponent<CrewMember>();
            inventory = GetComponent<CrewAmmoInventory>();
        }

        /* { 処理: AI実行と担当と経路を解除します。前提: 方針、学習、所持弾は保持します。 } */
        public void Suspend()
        {
            // 操作切替や死亡直後に予約を回収します。
            IsRunning = false;
            VisibleAmmo = null;
            visibleObservation = null;
            CurrentAction = AiActionType.Idle;
            navigation?.Cancel();
            utility?.Clear();
            assignment?.Release();
        }

        /* { 処理: 現在位置と向きからAI判断を再開します。 } */
        public void ResumeFromCurrentState()
        {
            // 古い対象とクールダウンで直接操作の終了を引き継ぎません。
            if (actor == null) Awake();
            IsRunning = true;
            interactionTimer = 0;
            searchTimer = 1;
            ObserveAndChoose();
        }

        /* { 処理: Unityのフレーム時間で実行を進めます。 } */
        private void Update() => Tick(Time.deltaTime);

        /* { 処理: 再評価した候補と本人の現在の観測を実行用状態へ写します。 } */
        private void ObserveAndChoose()
        {
            // 共有情報から選んでも、操作用の観測は本人が得たものだけです。
            utility.SelectAction();
            ObservationOrigin = utility.ObservationOrigin;
            ObservationFacing = utility.ObservationFacing;
            visibleObservation = utility.SelectedObservation;
            CurrentAction = utility.CurrentAction;
            VisibleAmmo = CurrentAction == AiActionType.CarryAmmo
                ? sensor.GetVisibleAmmo(visibleObservation) : null;
        }

        /* { 処理: 担当を再評価して、待機、移動、操作の一段階を進めます。引数: deltaTimeは有限の正の秒数です。 } */
        public void Tick(float deltaTime)
        {
            // 実行できない状態で予約を更新し続けません。
            if (!assignment.CanReserve)
            {
                assignment.Release();
                navigation.Cancel();
                if (!actor.IsAvailable || actor.Arena == null || actor.Arena.IsFinished) Suspend();
                return;
            }
            if (!UtilityRules.Range(deltaTime, float.Epsilon, float.MaxValue)) return;
            ObserveAndChoose();
            if (CurrentAction == AiActionType.Idle)
            {
                Search(deltaTime);
                return;
            }
            if (!assignment.IsCurrent()) return;

            // 防衛は移動前にも射程内の現在の観測へ作用します。
            interactionTimer -= deltaTime;
            if (CurrentAction == AiActionType.DefendShip && interactionTimer <= 0 && actor.TryAttackVisible(visibleObservation))
            {
                CompleteInteraction(true);
                return;
            }
            var selected = utility.Decision.Selected;
            var target = new Vector3(selected.X, selected.Y, 0);
            if (NeedsMovement(target))
            {
                MoveToward(target, deltaTime);
                return;
            }
            navigation.Cancel();
            if (interactionTimer <= 0) InteractAtDestination();
        }

        /* { 処理: 候補がない時に移動を止め、一定間隔で視野を回します。 } */
        private void Search(float deltaTime)
        {
            // 観測できない対象を全知の検索で補いません。
            navigation.Cancel();
            searchTimer -= deltaTime;
            if (searchTimer > 0) return;
            actor.Face(new Vector2(-actor.Facing.y, actor.Facing.x));
            searchTimer = 1;
        }

        /* { 処理: 操作範囲または通行区間の条件を満たすまで移動が必要か判定します。 } */
        private bool NeedsMovement(Vector3 target)
        {
            // 短い攻撃射程でも、射程外で停止して予約を保持し続けません。
            var definition = crew.Definition;
            var distance = CurrentAction == AiActionType.DefendShip ? Mathf.Min(0.45f, definition.AttackRange * 0.8f) : 0.45f;
            return ((Vector2)(target - transform.position)).sqrMagnitude > distance * distance ||
                !actor.Arena.CanCrewMove(actor.TeamSide, transform.position, target, definition);
        }

        /* { 処理: 共通経路で移動し、経路失敗時は担当を回収します。 } */
        private void MoveToward(Vector3 target, float deltaTime)
        {
            // 予約を失ってからも以前の目的地へ歩き続けません。
            if (!assignment.IsCurrent()) return;
            var status = navigation.MoveTo(target, deltaTime);
            if (status != NavigationStatus.Blocked && crew.Definition.MoveSpeed > 0) return;
            if (interactionTimer <= 0) utility.RecordOutcome(CurrentAction, false);
            interactionTimer = 0.35f;
            assignment.RejectBlockedTarget();
        }

        /* { 処理: 現在の担当と本人の視界を確認して、到着した対象へ作用します。 } */
        private void InteractAtDestination()
        {
            // 共有座標へ到着しても観測できなければ、操作せず古い報告を解除します。
            if (!assignment.IsCurrent()) return;
            if ((CurrentAction == AiActionType.CarryAmmo || CurrentAction == AiActionType.DefendShip) && visibleObservation == null)
            {
                assignment.ForgetCurrentTarget();
                return;
            }
            var success = CurrentAction switch
            {
                AiActionType.CarryAmmo => sensor.TryPickupVisible(visibleObservation, inventory),
                AiActionType.DefendShip => actor.TryAttackVisible(visibleObservation),
                AiActionType.LoadCannon or AiActionType.OperateCannon => actor.Arena.PlayerCannon.TryInteract(crew, inventory),
                _ => false
            };
            CompleteInteraction(success);
        }

        /* { 処理: 操作結果を学習し、完了した担当を解放します。前提: 防衛担当は攻撃間隔の間も保持します。 } */
        private void CompleteInteraction(bool success)
        {
            // 装填後は、砲手が次の判断で発射を担当できます。
            navigation.Cancel();
            utility.RecordOutcome(CurrentAction, success);
            interactionTimer = 0.35f;
            if (CurrentAction == AiActionType.CarryAmmo) assignment.ForgetCurrentTarget();
            else if (CurrentAction != AiActionType.DefendShip) assignment.Release();
            else if (!sensor.CanStillSee(visibleObservation)) assignment.ForgetCurrentTarget();
        }

        /* { 処理: 実行器の無効化時に担当と移動を回収します。 } */
        private void OnDisable() => Suspend();
    }
}
