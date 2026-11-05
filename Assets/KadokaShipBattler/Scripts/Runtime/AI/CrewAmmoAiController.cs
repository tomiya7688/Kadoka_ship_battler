using KadokaShipBattler.Ammo;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Navigation;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    [RequireComponent(typeof(PlayerCrewController), typeof(VisionSensor), typeof(UtilityAiAgent))]
    public sealed class CrewAmmoAiController : MonoBehaviour
    {
        private PlayerCrewController actor;
        private float interactionTimer;
        private float searchTimer;
        private VisionObservation visibleObservation;
        public bool IsRunning { get; private set; }
        public Vector2 ObservationOrigin { get; private set; }
        public Vector2 ObservationFacing { get; private set; }
        public AmmoPickup VisibleAmmo { get; private set; }
        public AiActionType CurrentAction { get; private set; } = AiActionType.Idle;

        private void Awake() => actor = GetComponent<PlayerCrewController>();
        public void Suspend()
        {
            IsRunning = false;
            VisibleAmmo = null;
            visibleObservation = null;
            CurrentAction = AiActionType.Idle;
            GetComponent<NavigationAgent>()?.Cancel();
            GetComponent<UtilityAiAgent>().Clear();
        }
        public void ResumeFromCurrentState()
        {
            if (actor == null) actor = GetComponent<PlayerCrewController>();
            IsRunning = true;
            interactionTimer = 0;
            searchTimer = 1f;
            Observe();
            ChooseAction();
        }
        private void Update() => Tick(Time.deltaTime);

        // Perception comes from the sensor. Own cannon position is known map information.
        private void Observe()
        {
            var utility = GetComponent<UtilityAiAgent>(); utility.SelectAction();
            ObservationOrigin = utility.ObservationOrigin; ObservationFacing = utility.ObservationFacing;
            visibleObservation = utility.SelectedObservation;
            VisibleAmmo = utility.CurrentAction == AiActionType.CarryAmmo ? GetComponent<VisionSensor>().GetVisibleAmmo(visibleObservation) : null;
        }
        private void ChooseAction()
        {
            CurrentAction = GetComponent<UtilityAiAgent>().CurrentAction;
        }
        public void Tick(float deltaTime)
        {
            if (actor != null && (!actor.IsAvailable || actor.Arena != null && actor.Arena.IsFinished)) { Suspend(); return; }
            if (!IsRunning || actor == null || !actor.IsAvailable || actor.IsDirectlyControlled || !isActiveAndEnabled || actor.Arena == null || !actor.Arena.AllyActionsEnabled || actor.Arena.IsFinished || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            Observe();
            ChooseAction();
            if (CurrentAction == AiActionType.Idle)
            {
                GetComponent<NavigationAgent>().Cancel();
                searchTimer -= deltaTime;
                if (searchTimer <= 0)
                {
                    actor.Face(new Vector2(-actor.Facing.y, actor.Facing.x));
                    searchTimer = 1f;
                }
                return;
            }
            var utility = GetComponent<UtilityAiAgent>();
            var selected = utility.Decision.Selected;
            var target = new Vector3(selected.X, selected.Y, 0);
            if (CurrentAction == AiActionType.DefendShip && interactionTimer <= 0 && actor.TryAttackVisible(visibleObservation))
            {
                GetComponent<NavigationAgent>().Cancel(); utility.RecordOutcome(CurrentAction, true);
                interactionTimer = 0.35f; return;
            }
            var offset = (Vector2)(target - transform.position);
            if (offset.magnitude > 0.45f || !actor.Arena.CanCrewMove(actor.TeamSide, transform.position, target, GetComponent<CrewMember>().Definition))
            {
                var status = GetComponent<NavigationAgent>().MoveTo(target, deltaTime);
                interactionTimer -= deltaTime;
                if (status == NavigationStatus.Blocked && interactionTimer <= 0) { utility.RecordOutcome(CurrentAction, false); interactionTimer = 0.35f; }
                return;
            }
            GetComponent<NavigationAgent>().Cancel();
            interactionTimer -= deltaTime;
            if (interactionTimer > 0) return;
            var success = CurrentAction == AiActionType.CarryAmmo
                ? GetComponent<VisionSensor>().TryPickupVisible(visibleObservation, GetComponent<CrewAmmoInventory>())
                : CurrentAction == AiActionType.DefendShip ? actor.TryAttackVisible(visibleObservation)
                : actor.Arena.PlayerCannon.TryInteract(GetComponent<CrewMember>(), GetComponent<CrewAmmoInventory>());
            utility.RecordOutcome(CurrentAction, success);
            interactionTimer = 0.35f;
        }
    }
}
