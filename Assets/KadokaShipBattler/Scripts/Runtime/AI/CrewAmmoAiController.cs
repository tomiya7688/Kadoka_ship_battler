using KadokaShipBattler.Ammo;
using KadokaShipBattler.Characters;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    [RequireComponent(typeof(PlayerCrewController))]
    public sealed class CrewAmmoAiController : MonoBehaviour
    {
        private PlayerCrewController actor;
        private float interactionTimer;
        private float searchTimer;
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
            CurrentAction = AiActionType.Idle;
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

        // Perception is deliberately small: nearby ammo in a forward cone. Cannon location is known.
        private void Observe()
        {
            ObservationOrigin = transform.position;
            ObservationFacing = actor.Facing;
            VisibleAmmo = null;
            var bestDistance = float.PositiveInfinity;
            Physics2D.SyncTransforms();
            foreach (var collider in Physics2D.OverlapCircleAll(ObservationOrigin, 6f))
            {
                var pickup = collider.GetComponent<AmmoPickup>();
                if (pickup == null || pickup.Definition == null) continue;
                var offset = (Vector2)pickup.transform.position - ObservationOrigin;
                if (offset.sqrMagnitude > 0.01f && Vector2.Dot(offset.normalized, ObservationFacing) < 0.5f) continue;
                if (offset.sqrMagnitude >= bestDistance) continue;
                bestDistance = offset.sqrMagnitude;
                VisibleAmmo = pickup;
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
                searchTimer -= deltaTime;
                if (searchTimer <= 0)
                {
                    actor.Face(new Vector2(-actor.Facing.y, actor.Facing.x));
                    searchTimer = 1f;
                }
                return;
            }
            var target = CurrentAction == AiActionType.CarryAmmo ? VisibleAmmo.transform.position : actor.Arena.PlayerCannon.transform.position;
            // Fixture-specific bridge waypoints; room/door navigation remains a separate system.
            var routeThroughBridge = target.x < -1f && transform.position.x > -1f;
            if (routeThroughBridge)
                target = transform.position.x > 1f && Mathf.Abs(transform.position.y) > 0.05f
                    ? new Vector3(transform.position.x, 0, 0) : new Vector3(-1.2f, 0, 0);
            var offset = (Vector2)(target - transform.position);
            if (offset.magnitude > (routeThroughBridge ? 0.01f : 0.45f))
            {
                actor.Move(offset.normalized, Mathf.Min(deltaTime, offset.magnitude / Mathf.Max(0.01f, GetComponent<CrewMember>().Definition.MoveSpeed)));
                return;
            }
            if (routeThroughBridge) return;
            interactionTimer -= deltaTime;
            if (interactionTimer > 0) return;
            actor.TryInteract();
            interactionTimer = 0.35f;
        }
    }
}
