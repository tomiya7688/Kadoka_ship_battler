using System.Collections.Generic;
using System.Linq;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using KadokaShipBattler.Navigation;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    [RequireComponent(typeof(PlayerCrewController), typeof(VisionSensor))]
    public sealed class UtilityAiAgent : MonoBehaviour
    {
        [SerializeField] private AiPolicyDefinition teamPolicy;
        [SerializeField] private AiPolicyDefinition characterPolicy;
        [SerializeField] private bool showDebug;
        private PlayerCrewController actor;
        public UtilityLearningState Learning { get; } = new();
        public UtilityDecision Decision { get; private set; }
        public VisionObservation SelectedObservation { get; private set; }
        public AiActionType CurrentAction => Decision?.Selected.Action ?? AiActionType.Idle;
        public Vector2 ObservationOrigin { get; private set; }
        public Vector2 ObservationFacing { get; private set; }
        public IReadOnlyList<UtilityScore> Scores => Decision?.Scores ?? System.Array.Empty<UtilityScore>();
        private void Awake() => actor = GetComponent<PlayerCrewController>();
        private void Update() { if (!actor.IsAvailable || actor.Arena == null || actor.Arena.IsFinished) Clear(); }
        public void Clear() { Decision = null; SelectedObservation = null; }
        public AiActionType SelectAction()
        {
            Clear();
            if (!actor.IsAvailable || actor.Arena == null || actor.Arena.IsFinished) return CurrentAction;
            var sensor = GetComponent<VisionSensor>(); sensor.Scan();
            ObservationOrigin = sensor.ObservationOrigin; ObservationFacing = sensor.FacingDirection;
            var observations = sensor.Observations.ToArray();
            var crew = GetComponent<CrewMember>(); var stats = crew.Definition;
            var inventory = GetComponent<CrewAmmoInventory>(); var arena = actor.Arena;
            // Only own state and current sensor samples enter the evaluation context.
            var ship = actor.TeamSide == TeamSide.Player ? arena.PlayerShip : arena.EnemyShip;
            var deck = actor.TeamSide == TeamSide.Player ? arena.PlayerAmmo : arena.EnemyAmmo;
            var cannon = actor.TeamSide == TeamSide.Player ? arena.PlayerCannon : null;
            var context = new UtilityContext(stats.Capabilities, stats.CombatSkill, stats.CarrySkill,
                crew.CurrentHp / stats.MaxHp, ship.CurrentHull / ship.MaxHull, deck != null ? deck.Deck.WaitingCount / 25f : 1,
                inventory.HasAmmo, cannon != null && cannon.IsLoaded, stats.AttackMode != NormalAttackMode.None && stats.AttackDamage > 0);
            var candidates = new List<UtilityCandidate> { new UtilityCandidate(AiActionType.Idle) };
            float Danger(Vector2 point)
            {
                var value = 0f;
                foreach (var seen in observations)
                    if (seen.Kind == ObservedTargetKind.Crew && seen.TeamSide != actor.TeamSide && seen.Hp > 0)
                        value += Mathf.Max(0, 1 - Vector2.Distance(point, new Vector2(seen.X, seen.Y)) / 3) * 0.5f;
                return Mathf.Clamp01(value);
            }
            if (cannon != null && (context.CannonLoaded || context.HasAmmo) && TryDistance(cannon.transform.position, out var cannonDistance))
            {
                var point = cannon.transform.position;
                candidates.Add(new UtilityCandidate(context.CannonLoaded ? AiActionType.OperateCannon : AiActionType.LoadCannon,
                    cannon.GetInstanceID(), point.x, point.y, cannonDistance, Danger(point)));
            }
            foreach (var seen in observations)
            {
                var point = new Vector2(seen.X, seen.Y);
                if (seen.Kind == ObservedTargetKind.Ammo)
                {
                    var pickup = sensor.GetVisibleAmmo(seen);
                    if (pickup == null || !inventory.CanPickup(pickup.Round) || !TryDistance(point, out var distance)) continue;
                    candidates.Add(new UtilityCandidate(AiActionType.CarryAmmo, seen.TargetId, seen.X, seen.Y,
                        distance, Danger(point), seen.AmmoDamage, seen.AmmoWeight));
                }
                else if (seen.Kind == ObservedTargetKind.Crew && seen.TeamSide != actor.TeamSide && seen.Hp > 0 &&
                    arena.Navigation.Rooms.Any(room => room.Side == actor.TeamSide && room.Bounds.Contains(new NavPoint(seen.X, seen.Y))) &&
                    TryDistance(point, out var distance))
                    candidates.Add(new UtilityCandidate(AiActionType.DefendShip, seen.TargetId, seen.X, seen.Y, distance, Danger(point), targetHp: seen.Hp));
            }
            Decision = UtilityRules.Evaluate(context, candidates, Learning, PolicyWeight);
            SelectedObservation = observations.FirstOrDefault(seen => seen.TargetId == Decision.Selected.TargetId);
            return CurrentAction;
        }
        private bool TryDistance(Vector2 goal, out float distance)
        {
            distance = 0; var arena = actor.Arena; var stats = GetComponent<CrewMember>().Definition;
            var flags = (stats.CanFly ? TraversalAbilities.Fly : 0) | (stats.CanPhase ? TraversalAbilities.Phase : 0);
            if (!arena.Navigation.TryFindPath(new NavPoint(transform.position.x, transform.position.y), new NavPoint(goal.x, goal.y),
                flags, actor.TeamSide, arena.PlayerShip.IsHullBreached, arena.EnemyShip.IsHullBreached, out var path)) return false;
            var previous = (Vector2)transform.position;
            foreach (var step in path) { var point = new Vector2(step.x, step.y); distance += Vector2.Distance(previous, point); previous = point; }
            return true;
        }
        private float PolicyWeight(AiActionType action)
        {
            var definition = GetComponent<CrewMember>().Definition;
            float Weight(AiPolicyDefinition policy) => policy != null && policy.IsAvailableFor(definition) ? policy.GetWeight(action) : 1;
            var team = Weight(teamPolicy); var individual = Weight(characterPolicy);
            if (!UtilityRules.Range(team, 0, 4) || !UtilityRules.Range(individual, 0, 4)) throw new System.ArgumentException("Policy weights must be finite and between 0 and 4.");
            return team * individual;
        }
        public void RecordOutcome(AiActionType action, bool success) => Learning.RecordOutcome(action, success ? 1 : -1);
        private void OnDisable() => Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            if (!showDebug || Decision == null || actor.IsDirectlyControlled) return;
            var slot = actor.Arena.PlayerCrew.ToList().IndexOf(actor);
            var values = string.Join("  ", Scores.OrderByDescending(score => score.Score).Take(3)
                .Select(score => $"{score.Candidate.Action}#{score.Candidate.TargetId}: {score.BaseScore:0.0}x{score.LearnedWeight:0.00}x{score.PolicyWeight:0.00}={score.Score:0.0}"));
            GUI.Label(new Rect(10, 220 + Mathf.Max(0, slot) * 45, 1100, 45), $"{name}: {CurrentAction}\n{values}");
        }
        private void OnDrawGizmosSelected()
        {
            if (Decision == null || CurrentAction == AiActionType.Idle) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, new Vector3(Decision.Selected.X, Decision.Selected.Y));
        }
#endif
    }
}
