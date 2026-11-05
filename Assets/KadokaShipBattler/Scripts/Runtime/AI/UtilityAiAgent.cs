using System.Collections.Generic;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using KadokaShipBattler.Navigation;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 現在の観測と自船情報から実行可能な行動を評価します。
        前提: チーム・個人方針は能力制限を解除しません。学習状態は方針から独立です。
    } */
    [RequireComponent(typeof(PlayerCrewController), typeof(VisionSensor), typeof(CrewPolicySelection))]
    [RequireComponent(typeof(UtilityDebugView))]
    public sealed class UtilityAiAgent : MonoBehaviour
    {
        [SerializeField] private AiPolicyDefinition teamPolicy;
        [SerializeField] private AiPolicyDefinition characterPolicy;
        [SerializeField] private bool showDebug;
        private PlayerCrewController actor;
        private CrewPolicySelection policySelection;
        private System.Func<AiActionType, float> evaluatePolicyWeight;
        private float ownHullFraction = 1;
        private bool hasObservedInvader;
        public UtilityLearningState Learning { get; } = new();
        public UtilityDecision Decision { get; private set; }
        public VisionObservation SelectedObservation { get; private set; }
        public AiActionType CurrentAction => Decision?.Selected.Action ?? AiActionType.Idle;
        public Vector2 ObservationOrigin { get; private set; }
        public Vector2 ObservationFacing { get; private set; }
        public bool IsDebugVisible => showDebug;
        public IReadOnlyList<UtilityScore> Scores => Decision?.Scores ?? System.Array.Empty<UtilityScore>();
        /* { 処理: 操作と方針のコンポーネントを一度だけ取得します。 } */
        private void Awake()
        {
            // 毎評価で同じコンポーネントを探索しません。
            actor = GetComponent<PlayerCrewController>();
            policySelection = GetComponent<CrewPolicySelection>();
            evaluatePolicyWeight = PolicyWeight;
        }
        /* { 処理: 死亡、戦闘未設定、終了時に一時的な判断を解除します。 } */
        private void Update()
        {
            // 実行できない状態で以前の対象を保持しません。
            if (!actor.IsAvailable || actor.Arena == null || actor.Arena.IsFinished) Clear();
        }

        /* { 処理: 行動と観測対象を解除します。前提: 学習と方針は保持します。 } */
        public void Clear()
        {
            // 再開時には新しい観測から判断を作り直します。
            Decision = null;
            SelectedObservation = null;
        }
        /* {
            処理: 観測した実行可能候補を、適性・学習・方針の補正で比較します。
            戻り値: 死亡、戦闘終了、候補なしの場合は待機です。
            前提: 緊急補正へ渡す侵入情報は本人の現在の視界に限定します。
        } */
        public AiActionType SelectAction()
        {
            // 今回の観測だけで候補を作り直します。
            Clear();
            if (!actor.IsAvailable || actor.Arena == null || actor.Arena.IsFinished) return CurrentAction;
            var sensor = GetComponent<VisionSensor>();
            sensor.Scan();
            ObservationOrigin = sensor.ObservationOrigin;
            ObservationFacing = sensor.FacingDirection;
            var observations = sensor.Observations;
            var context = CreateContext();
            ownHullFraction = context.HullFraction;
            hasObservedInvader = false;
            var candidates = CollectCandidates(sensor, context, observations);

            // 候補と現在の方針から行動を確定します。
            Decision = UtilityRules.Evaluate(context, candidates, Learning, evaluatePolicyWeight);
            foreach (var observation in observations)
                if (observation.TargetId == Decision.Selected.TargetId) SelectedObservation = observation;
            return CurrentAction;
        }

        /* { 処理: 船員と自船の既知情報を評価用の値に変換します。戻り値: 敵の未観測状態を含まない評価文脈です。 } */
        private UtilityContext CreateContext()
        {
            // 自船情報は味方の既知情報として扱います。
            var crew = GetComponent<CrewMember>();
            var stats = crew.Definition;
            var arena = actor.Arena;
            var ship = actor.TeamSide == TeamSide.Player ? arena.PlayerShip : arena.EnemyShip;
            var deck = actor.TeamSide == TeamSide.Player ? arena.PlayerAmmo : arena.EnemyAmmo;
            var cannon = actor.TeamSide == TeamSide.Player ? arena.PlayerCannon : null;
            return new UtilityContext(stats.Capabilities, stats.CombatSkill, stats.CarrySkill,
                crew.CurrentHp / stats.MaxHp, ship.CurrentHull / ship.MaxHull, deck != null ? deck.Deck.WaitingCount / 25f : 1,
                GetComponent<CrewAmmoInventory>().HasAmmo, cannon != null && cannon.IsLoaded,
                stats.AttackMode != NormalAttackMode.None && stats.AttackDamage > 0);
        }

        /* {
            処理: 観測と自船砲台から到達可能な行動候補を集めます。
            副作用: 今回の自船内の侵入観測を記録します。
            前提: 観測リストはこの同期処理中に再走査や変更を行いません。
        } */
        private List<UtilityCandidate> CollectCandidates(VisionSensor sensor, UtilityContext context,
            IReadOnlyList<VisionObservation> observations)
        {
            // 既知の自船砲台は、視界外でも移動候補になります。
            var inventory = GetComponent<CrewAmmoInventory>();
            var cannon = actor.TeamSide == TeamSide.Player ? actor.Arena.PlayerCannon : null;
            var candidates = new List<UtilityCandidate> { new UtilityCandidate(AiActionType.Idle) };
            if (cannon != null && (context.CannonLoaded || context.HasAmmo) && TryDistance(cannon.transform.position, out var cannonDistance))
            {
                var point = cannon.transform.position;
                candidates.Add(new UtilityCandidate(context.CannonLoaded ? AiActionType.OperateCannon : AiActionType.LoadCannon,
                    cannon.GetInstanceID(), point.x, point.y, cannonDistance, CalculateObservedDanger(point, observations)));
            }
            // 弾の取得と侵入者への防衛は、現在の観測だけから作ります。
            foreach (var seen in observations)
            {
                var point = new Vector2(seen.X, seen.Y);
                if (seen.Kind == ObservedTargetKind.Ammo)
                {
                    var pickup = sensor.GetVisibleAmmo(seen);
                    if (pickup == null || !inventory.CanPickup(pickup.Round) || !TryDistance(point, out var distance)) continue;
                    candidates.Add(new UtilityCandidate(AiActionType.CarryAmmo, seen.TargetId, seen.X, seen.Y,
                        distance, CalculateObservedDanger(point, observations), seen.AmmoDamage, seen.AmmoWeight));
                }
                else if (IsObservedInvader(seen))
                {
                    hasObservedInvader = true;
                    if (TryDistance(point, out var distance))
                        candidates.Add(new UtilityCandidate(AiActionType.DefendShip, seen.TargetId, seen.X, seen.Y,
                            distance, CalculateObservedDanger(point, observations), targetHp: seen.Hp));
                }
            }
            return candidates;
        }

        /* { 処理: 観測した生存中の敵が自船の部屋にいるか確認します。戻り値: 侵入者ならtrueです。 } */
        private bool IsObservedInvader(VisionObservation observation)
        {
            // LINQの条件式生成を避け、既知の部屋だけを確認します。
            if (observation.Kind != ObservedTargetKind.Crew || observation.TeamSide == actor.TeamSide || observation.Hp <= 0) return false;
            foreach (var room in actor.Arena.Navigation.Rooms)
                if (room.Side == actor.TeamSide && room.Bounds.Contains(new NavPoint(observation.X, observation.Y))) return true;
            return false;
        }

        /* { 処理: 観測した敵の近さから地点の危険度を求めます。戻り値: 0から1です。 } */
        private float CalculateObservedDanger(Vector2 point, IReadOnlyList<VisionObservation> observations)
        {
            // 視界外の敵位置やHPを読みません。
            var danger = 0f;
            foreach (var observation in observations)
                if (observation.Kind == ObservedTargetKind.Crew && observation.TeamSide != actor.TeamSide && observation.Hp > 0)
                    danger += Mathf.Max(0, 1 - Vector2.Distance(point, new Vector2(observation.X, observation.Y)) / 3) * 0.5f;
            return Mathf.Clamp01(danger);
        }
        /* { 処理: 船員の能力と船体条件に合う経路の距離を計算します。戻り値: 到達可能ならtrueです。 } */
        private bool TryDistance(Vector2 goal, out float distance)
        {
            // 直線距離ではなく、共通の経路探索が返す区間を使います。
            distance = 0;
            var arena = actor.Arena;
            var stats = GetComponent<CrewMember>().Definition;
            var flags = (stats.CanFly ? TraversalAbilities.Fly : 0) | (stats.CanPhase ? TraversalAbilities.Phase : 0);
            if (!arena.Navigation.TryFindPath(new NavPoint(transform.position.x, transform.position.y), new NavPoint(goal.x, goal.y),
                flags, actor.TeamSide, arena.PlayerShip.IsHullBreached, arena.EnemyShip.IsHullBreached, out var path)) return false;
            var previous = (Vector2)transform.position;
            foreach (var step in path)
            {
                var point = new Vector2(step.x, step.y);
                distance += Vector2.Distance(previous, point);
                previous = point;
            }
            return true;
        }
        /* {
            処理: 実行中の方針・緊急補正と既存Inspector設定を合成します。
            戻り値: 評価関数の許容範囲である0から16です。
            例外: Inspector設定の非有限値と範囲外の重みは拒否します。
        } */
        private float PolicyWeight(AiActionType action)
        {
            // 既存の定義アセットとの互換性を保ちます。
            var definition = GetComponent<CrewMember>().Definition;
            var team = GetDefinitionWeight(teamPolicy, action, definition);
            var individual = GetDefinitionWeight(characterPolicy, action, definition);
            if (!UtilityRules.Range(team, 0, 4) || !UtilityRules.Range(individual, 0, 4)) throw new System.ArgumentException("Policy weights must be finite and between 0 and 4.");
            var selectedWeight = policySelection.GetWeight(action, ownHullFraction, hasObservedInvader);
            return Mathf.Min(16, team * individual * selectedWeight);
        }
        /* { 処理: 既存アセットが船員の能力条件を満たす場合に重みを取得します。戻り値: 未設定や能力不足は1です。 } */
        private static float GetDefinitionWeight(AiPolicyDefinition policy, AiActionType action, CharacterDefinition definition) =>
            policy != null && policy.IsAvailableFor(definition) ? policy.GetWeight(action) : 1;
        /* { 処理: 実行の成否を学習状態へ渡します。前提: 方針を変更しません。 } */
        public void RecordOutcome(AiActionType action, bool success) => Learning.RecordOutcome(action, success ? 1 : -1);

        /* { 処理: コンポーネント無効化時に判断を解除します。 } */
        private void OnDisable() => Clear();
    }
}
