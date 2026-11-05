using System.Collections.Generic;
using KadokaShipBattler.Ammo;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using KadokaShipBattler.Ships;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    [RequireComponent(typeof(PlayerCrewController))]
    public sealed class VisionSensor : MonoBehaviour
    {
        [Range(1, 360)] [SerializeField] private float viewAngle = 120;
        [Min(0.01f)] [SerializeField] private float viewDistance = 6;
        private PlayerCrewController actor;
        private VisionCone cone;
        private readonly VisionMemory memory = new();
        private readonly Dictionary<int, Component> targets = new();
        public float ViewAngle => viewAngle;
        public float ViewDistance => viewDistance;
        public Vector2 ObservationOrigin { get; private set; }
        public Vector2 FacingDirection { get; private set; }
        public IReadOnlyList<VisionObservation> Observations => memory.Observations;
        private void Awake()
        {
            actor = GetComponent<PlayerCrewController>();
            cone = new VisionCone(viewAngle, viewDistance);
        }
        private void LateUpdate() => Scan();
        public void Configure(float angle, float distance)
        {
            var next = new VisionCone(angle, distance);
            viewAngle = angle;
            viewDistance = distance;
            cone = next;
            Clear();
        }
        public void Scan()
        {
            using (new TriggerQueryScope(true)) ScanCore();
        }
        private void ScanCore()
        {
            Clear();
            if (!isActiveAndEnabled || actor == null || !actor.IsAvailable) return;
            if (cone.ViewAngle != viewAngle || cone.ViewDistance != viewDistance) cone = new VisionCone(viewAngle, viewDistance);
            ObservationOrigin = transform.position;
            FacingDirection = actor.Facing;
            Physics2D.SyncTransforms();
            var samples = new List<VisionObservation>();
            var candidates = new List<Collider2D>();
            Physics2D.OverlapCircle(ObservationOrigin, viewDistance, SightFilter(), candidates);
            foreach (var collider in candidates)
            {
                Component target = collider.GetComponentInParent<AmmoPickup>();
                if (target == null) target = collider.GetComponentInParent<CrewMember>();
                if (target == null) target = collider.GetComponentInParent<ShipCore>();
                if (target == null || target.gameObject == gameObject || targets.ContainsKey(target.GetInstanceID())) continue;
                if (!HasLineOfSight(target.transform.position) || !CanObserve(target)) continue;
                var position = target.transform.position;
                VisionObservation sample;
                if (target is AmmoPickup pickup)
                {
                    var owner = pickup.GetComponentInParent<AmmoDeckSpawner>();
                    sample = new VisionObservation(target.GetInstanceID(), ObservedTargetKind.Ammo, owner != null ? owner.TeamSide : (TeamSide?)null,
                        position.x, position.y, 0, pickup.Round.Weight, pickup.Round.Damage, Time.time);
                }
                else if (target is CrewMember crew)
                    sample = new VisionObservation(target.GetInstanceID(), ObservedTargetKind.Crew, crew.TeamSide,
                        position.x, position.y, crew.CurrentHp, 0, 0, Time.time);
                else
                {
                    var core = (ShipCore)target;
                    sample = new VisionObservation(target.GetInstanceID(), ObservedTargetKind.Core, core.OwnerShip.TeamSide,
                        position.x, position.y, core.OwnerShip.CurrentCore, 0, 0, Time.time);
                }
                targets.Add(sample.TargetId, target);
                samples.Add(sample);
            }
            memory.Replace(samples);
        }
        private static bool CanObserve(Component target)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            if (target is Behaviour behaviour && !behaviour.isActiveAndEnabled) return false;
            var hasCollider = false;
            foreach (var collider in target.GetComponentsInChildren<Collider2D>())
                if (collider.enabled && collider.gameObject.activeInHierarchy) { hasCollider = true; break; }
            if (!hasCollider) return false;
            return target switch
            {
                AmmoPickup pickup => !pickup.IsConsumed && pickup.Round != null && (!(pickup.Round is ITrackedAmmo tracked) || tracked.CanCarry),
                CrewMember crew => crew.IsAlive,
                ShipCore core => core.OwnerShip != null && !core.OwnerShip.IsDestroyed,
                _ => false
            };
        }
        private bool HasLineOfSight(Vector2 point)
        {
            var origin = (Vector2)transform.position;
            var facing = actor.Facing;
            if (!cone.Contains(origin.x, origin.y, facing.x, facing.y, point.x, point.y)) return false;
            var offset = point - origin;
            // Explicit filters keep perception independent of global trigger / start-inside query settings.
            var overlaps = new List<Collider2D>();
            Physics2D.OverlapPoint(origin, SightFilter(), overlaps);
            foreach (var overlap in overlaps) if (Blocks(overlap)) return false;
            var hits = new List<RaycastHit2D>();
            Physics2D.Raycast(origin, offset.normalized, SightFilter(), hits, offset.magnitude);
            foreach (var hit in hits) if (Blocks(hit.collider)) return false;
            return true;
        }
        private static ContactFilter2D SightFilter() => new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = ~0 };
        private bool Blocks(Collider2D collider)
        {
            var blocker = collider.GetComponentInParent<VisionBlocker>();
            return blocker != null && blocker.isActiveAndEnabled && !collider.transform.IsChildOf(transform);
        }
        public bool CanStillSee(VisionObservation observation)
        {
            using (new TriggerQueryScope(true)) return CanStillSeeCore(observation);
        }
        private bool CanStillSeeCore(VisionObservation observation)
        {
            if (!isActiveAndEnabled || actor == null || !actor.IsAvailable || !memory.IsCurrent(observation)) return false;
            Physics2D.SyncTransforms();
            if (targets.TryGetValue(observation.TargetId, out var target) && target != null &&
                HasLineOfSight(target.transform.position) && CanObserve(target)) return true;
            targets.Remove(observation.TargetId);
            memory.Remove(observation.TargetId);
            return false;
        }
        // Unity physics queries run synchronously on the main thread. This also covers editor settings
        // where the global trigger switch overrides an explicit ContactFilter2D in overlap queries.
        // Restore on every return or exception; never change the project's persistent physics settings.
        private readonly struct TriggerQueryScope : System.IDisposable
        {
            private readonly bool original;
            public TriggerQueryScope(bool forceTriggers)
            {
                original = Physics2D.queriesHitTriggers;
                if (forceTriggers && !original) Physics2D.queriesHitTriggers = true;
            }
            public void Dispose()
            {
                if (Physics2D.queriesHitTriggers != original) Physics2D.queriesHitTriggers = original;
            }
        }
        public AmmoPickup GetVisibleAmmo(VisionObservation observation) =>
            observation != null && observation.Kind == ObservedTargetKind.Ammo && CanStillSee(observation) ? targets[observation.TargetId] as AmmoPickup : null;
        public bool TryPickupVisible(VisionObservation observation, CrewAmmoInventory inventory)
        {
            var pickup = GetVisibleAmmo(observation);
            if (pickup == null || inventory == null || inventory.gameObject != gameObject ||
                Vector2.Distance(transform.position, pickup.transform.position) > 0.8f) return false;
            if (!pickup.TryPickup(inventory)) return false;
            targets.Remove(observation.TargetId);
            memory.Remove(observation.TargetId);
            return true;
        }
        public void Clear()
        {
            targets.Clear();
            memory.Clear();
        }
        private void OnDisable() => Clear();
        private void OnDestroy() => Clear();
        private void OnDrawGizmosSelected()
        {
            var facing = actor != null ? actor.Facing : Vector2.down;
            var origin = transform.position;
            var angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - viewAngle * 0.5f;
            var previous = origin;
            Gizmos.color = Color.cyan;
            for (var step = 0; step <= 24; step++)
            {
                var radians = (angle + viewAngle * step / 24f) * Mathf.Deg2Rad;
                var next = origin + new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0) * viewDistance;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
            Gizmos.DrawLine(previous, origin);
            foreach (var observation in Observations)
            {
                Gizmos.color = observation.Kind == ObservedTargetKind.Ammo ? Color.yellow : Color.magenta;
                Gizmos.DrawLine(origin, new Vector3(observation.X, observation.Y, origin.z));
            }
        }
    }
}
