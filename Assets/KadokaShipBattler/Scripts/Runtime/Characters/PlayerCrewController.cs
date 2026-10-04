using KadokaShipBattler.Ammo;
using KadokaShipBattler.AI;
using KadokaShipBattler.Core;
using KadokaShipBattler.Ships;
using UnityEngine;
namespace KadokaShipBattler.Characters
{
    [RequireComponent(typeof(CrewMember), typeof(CrewAmmoInventory))]
    public sealed class PlayerCrewController : MonoBehaviour, IControlledCrew
    {
        public BattlePrototype Arena { get; set; }
        public Vector2 Facing { get; private set; } = Vector2.down;
        public bool IsDirectlyControlled { get; private set; }
        public TeamSide TeamSide => Crew.TeamSide;
        public bool IsAvailable => this != null && isActiveAndEnabled && Crew.IsAlive;
        private CrewMember Crew => GetComponent<CrewMember>();
        private CrewAmmoInventory Inventory => GetComponent<CrewAmmoInventory>();
        private bool CanAct => IsAvailable && (Arena == null || !Arena.IsFinished);
        public void SetDirectControl(bool directControl)
        {
            if (this == null) return;
            IsDirectlyControlled = directControl;
            var ai = GetComponent<CrewAmmoAiController>();
            if (ai == null) return;
            if (directControl || !IsAvailable) ai.Suspend();
            else ai.ResumeFromCurrentState();
        }
        public void ApplyInput(CrewInput input, float deltaTime)
        {
            if (!IsDirectlyControlled || !CanAct) return;
            Move(input.Movement, deltaTime);
            if (input.Interact) TryInteract();
            if (input.Attack) TryAttack();
        }
        public void Move(Vector2 input, float deltaTime)
        {
            if (!CanAct || Crew.Definition == null || deltaTime <= 0f) return;
            Face(input);
            var next = (Vector2)transform.position + Vector2.ClampMagnitude(input, 1f) * (Crew.Definition.MoveSpeed * deltaTime);
            if (Arena == null || Arena.CanCrewStand(TeamSide, next)) transform.position = new Vector3(next.x, next.y, 0f);
        }
        public void Face(Vector2 direction)
        {
            if (direction.sqrMagnitude > 0f) Facing = direction.normalized;
        }
        public bool TryInteract()
        {
            if (!CanAct) return false;
            Physics2D.SyncTransforms();
            var colliders = Physics2D.OverlapCircleAll(transform.position, 0.8f);
            System.Array.Sort(colliders, (a, b) => (a.transform.position - transform.position).sqrMagnitude.CompareTo((b.transform.position - transform.position).sqrMagnitude));
            foreach (var collider in colliders)
            {
                var cannon = collider.GetComponent<CannonController>();
                if (cannon != null && cannon.TryInteract(Crew, Inventory)) return true;
                var pickup = collider.GetComponent<AmmoPickup>();
                if (pickup != null && pickup.TryPickup(Inventory)) return true;
            }
            return false;
        }
        public bool TryAttack() => TryAttackTarget(null);
        public bool TryAttackVisible(VisionObservation observation)
        {
            var sensor = GetComponent<VisionSensor>();
            return observation != null && sensor != null && sensor.CanStillSee(observation) && TryAttackTarget(observation.TargetId);
        }
        private bool TryAttackTarget(int? observedTargetId)
        {
            if (!CanAct || Crew.Definition.AttackMode == NormalAttackMode.None || !Crew.Can(CharacterCapability.Combat)) return false;
            Physics2D.SyncTransforms();
            var colliders = Physics2D.OverlapCircleAll(transform.position, Crew.Definition.AttackRange);
            System.Array.Sort(colliders, (a, b) => (a.transform.position - transform.position).sqrMagnitude.CompareTo((b.transform.position - transform.position).sqrMagnitude));
            foreach (var collider in colliders)
            {
                var offset = (Vector2)(collider.transform.position - transform.position);
                if (Crew.Definition.AttackMode == NormalAttackMode.Ranged && offset.sqrMagnitude > 0.01f &&
                    Vector2.Dot(offset.normalized, Facing) < 0.5f) continue;
                var core = collider.GetComponentInParent<ShipCore>();
                if (core != null && (!observedTargetId.HasValue || core.GetInstanceID() == observedTargetId.Value) && core.TryAttack(Crew)) return true;
                var target = collider.GetComponentInParent<CrewMember>();
                if (target != null && (!observedTargetId.HasValue || target.GetInstanceID() == observedTargetId.Value) && Crew.TryAttack(target)) return true;
            }
            return false;
        }
    }
}
