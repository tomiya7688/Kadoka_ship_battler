using KadokaShipBattler.Ammo;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Navigation;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    [RequireComponent(typeof(PlayerCrewController), typeof(VisionSensor))]
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
            var sensor = GetComponent<VisionSensor>();
            sensor.Scan();
            ObservationOrigin = sensor.ObservationOrigin;
            ObservationFacing = sensor.FacingDirection;
            VisibleAmmo = null;
            visibleObservation = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var observation in sensor.Observations)
            {
                var pickup = sensor.GetVisibleAmmo(observation);
                if (pickup == null || pickup.IsConsumed || !GetComponent<CrewAmmoInventory>().CanPickup(pickup.Round)) continue;
                var offset = new Vector2(observation.X, observation.Y) - ObservationOrigin;
                if (offset.sqrMagnitude >= bestDistance) continue;
                bestDistance = offset.sqrMagnitude;
                VisibleAmmo = pickup;
                visibleObservation = observation;
            }
        }
        private void ChooseAction()
        {
            var crew = GetComponent<CrewMember>();
            var inventory = GetComponent<CrewAmmoInventory>();
            var cannon = actor.Arena != null ? actor.Arena.PlayerCannon : null;
            if (cannon != null && cannon.IsLoaded && crew.Can(CharacterCapability.OperateCannon)) CurrentAction = AiActionType.OperateCannon;
            else if (inventory.HasAmmo && cannon != null) CurrentAction = AiActionType.LoadCannon;
            else if (VisibleAmmo != null && crew.Can(CharacterCapability.CarryAmmo)) CurrentAction = AiActionType.CarryAmmo;
            else CurrentAction = AiActionType.Idle;
        }
        public void Tick(float deltaTime)
        {
            if (!IsRunning || actor == null || !actor.IsAvailable || actor.IsDirectlyControlled || !isActiveAndEnabled || actor.Arena == null || !actor.Arena.AllyActionsEnabled || actor.Arena.IsFinished || deltaTime <= 0) return;
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
            var target = CurrentAction == AiActionType.CarryAmmo ? new Vector3(visibleObservation.X, visibleObservation.Y, 0) : actor.Arena.PlayerCannon.transform.position;
            var offset = (Vector2)(target - transform.position);
            if (offset.magnitude > 0.45f || !actor.Arena.CanCrewMove(actor.TeamSide, transform.position, target, GetComponent<CrewMember>().Definition))
            {
                GetComponent<NavigationAgent>().MoveTo(target, deltaTime);
                return;
            }
            GetComponent<NavigationAgent>().Cancel();
            interactionTimer -= deltaTime;
            if (interactionTimer > 0) return;
            if (CurrentAction == AiActionType.CarryAmmo)
                GetComponent<VisionSensor>().TryPickupVisible(visibleObservation, GetComponent<CrewAmmoInventory>());
            else actor.Arena.PlayerCannon.TryInteract(GetComponent<CrewMember>(), GetComponent<CrewAmmoInventory>());
            interactionTimer = 0.35f;
        }
    }
}
