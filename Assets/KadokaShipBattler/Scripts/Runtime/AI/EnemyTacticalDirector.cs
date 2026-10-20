using System.Collections.Generic;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 敵5人の観測を共有し、戦況から共有戦術目標と各船員の役割を決めて、役割実行器を進めます。
        前提: 判断に使うのは敵船員の本人視界、自船の既知情報、公開される船体状態だけです。
              プレイヤーの入力、視界外の船員位置、プレイヤー船内の弾を読みません。
        計算量: 判断は0.25秒ごとに行い、船員5人と共有観測数に比例します。移動と操作は毎フレーム進めます。
    } */
    [DisallowMultipleComponent]
    public sealed class EnemyTacticalDirector : MonoBehaviour
    {
        public const float DecisionIntervalSeconds = 0.25f;
        public const float CannonIntervalSeconds = 10;
        private readonly List<Vector2> searchPoints = new();
        private readonly EnemyTeamPerception perception = new();
        private readonly List<EnemyRoleController> controllers = new();
        private readonly List<EnemyCrewProfile> aliveProfiles = new();
        private readonly Dictionary<int, EnemyTacticalRole> roles = new();
        private readonly Dictionary<int, int> ammoTargets = new();
        private readonly Dictionary<int, int> searchIndexes = new();
        private readonly HashSet<int> claimedAmmo = new();
        private BattlePrototype arena;
        private PlayerTendencyEstimator estimator;
        private EnemyTacticalPlanner planner;
        private float clock;
        private float decisionTimer;
        private float cannonTimer;
        private float lastHull;

        public EnemyTacticalGoal Goal => planner != null ? planner.Goal : EnemyTacticalGoal.Bombard;
        public PlayerTendency Tendency { get; private set; }
        public EnemyTeamPerception Perception => perception;
        public bool IsCannonReady => cannonTimer >= CannonIntervalSeconds;

        /* {
            処理: 敵船員の実行器を接続し、観測の登録と推定器を初期化します。
            引数: battleは同じ戦闘。crewは先頭がリーダーである敵船員で、編成順に役割割り当ての優先度になります。
            前提: 各船員にEnemyRoleControllerとVisionSensorが付いています。
        } */
        public void Initialize(BattlePrototype battle, IReadOnlyList<CrewMember> crew)
        {
            // 同じ戦闘に対する再初期化で、古い登録を残しません。
            arena = battle;
            controllers.Clear();
            foreach (var member in crew)
            {
                var controller = member.GetComponent<EnemyRoleController>();
                controller.Connect(this, battle, controllers.Count);
                perception.Register(controller.MemberId);
                controllers.Add(controller);
            }
            BuildSearchPoints();
            estimator = new PlayerTendencyEstimator(0);
            planner = new EnemyTacticalPlanner(0);
            clock = 0;
            decisionTimer = 0;
            cannonTimer = 0;
            lastHull = battle.EnemyShip.CurrentHull;
        }

        /* { 処理: 経過時間を進め、一定間隔で戦術を判断し、全船員の役割実行を一段階進めます。引数: deltaTimeは有限の正の秒数です。 } */
        public void Tick(float deltaTime)
        {
            if (arena == null || !UtilityRules.Range(deltaTime, float.Epsilon, float.MaxValue)) return;
            clock += deltaTime;
            decisionTimer -= deltaTime;
            if (decisionTimer <= 0)
            {
                Decide();
                decisionTimer = DecisionIntervalSeconds;
            }
            if (!arena.PlayerShip.IsHullBreached) cannonTimer += deltaTime;
            foreach (var controller in controllers) controller.Tick(GetRole(controller.MemberId), deltaTime);
        }

        /* { 処理: 割り当て済みの役割を返します。判断前や死亡後の船員はNoneです。 } */
        public EnemyTacticalRole GetRole(int memberId) => roles.TryGetValue(memberId, out var role) ? role : EnemyTacticalRole.None;

        /* { 処理: 砲撃の間隔を数え直します。前提: 実際に発射に成功した直後に呼びます。 } */
        public void NotifyCannonFired() => cannonTimer = 0;

        /* { 処理: 補給役へ割り当てた弾の観測を返します。戻り値: 割り当てがない、または観測が期限切れならfalseです。 } */
        public bool TryGetAmmoTarget(int memberId, out VisionObservation target)
        {
            target = null;
            if (!ammoTargets.TryGetValue(memberId, out var targetId)) return false;
            foreach (var observation in perception.OwnAmmo)
            {
                if (observation.TargetId != targetId) continue;
                target = observation;
                return true;
            }
            return false;
        }

        /* { 処理: 指定位置から最も近い、観測済みの侵入者を返します。戻り値: 観測中の侵入者がいなければfalseです。 } */
        public bool TryGetNearestIntruder(Vector2 from, out VisionObservation target)
        {
            target = null;
            var nearest = float.PositiveInfinity;
            foreach (var observation in perception.Intruders)
            {
                var distance = (new Vector2(observation.X, observation.Y) - from).sqrMagnitude;
                if (distance >= nearest) continue;
                target = observation;
                nearest = distance;
            }
            return target != null;
        }

        /* {
            処理: 乗り込み役の移動先を返します。観測済みの核があればその位置、なければ船員ごとの捜索地点です。
            前提: 捜索地点は相手船の部屋の中心です。核、船員、弾の位置は、敵船員が実際に見るまで知りません。
        } */
        public Vector2 GetBoardingDestination(int memberId, int slot)
        {
            if (perception.PlayerCore != null) return new Vector2(perception.PlayerCore.X, perception.PlayerCore.Y);
            if (searchPoints.Count == 0) return arena.PlayerShip.transform.position;
            searchIndexes.TryGetValue(memberId, out var index);
            return searchPoints[(index + slot) % searchPoints.Count];
        }

        /* { 処理: 到達済みまたは通行不可の捜索地点を飛ばして、次の部屋へ進めます。 } */
        public void AdvanceSearchPoint(int memberId)
        {
            if (searchPoints.Count == 0) return;
            searchIndexes.TryGetValue(memberId, out var index);
            searchIndexes[memberId] = (index + 1) % searchPoints.Count;
        }

        /* { 処理: 取得や再観測に失敗した共有対象を、全員の共有から除きます。 } */
        public void ForgetTarget(int targetId) => perception.ForgetTarget(targetId);

        /* { 処理: プレイヤー船の部屋の中心を、橋に近い順の捜索地点として作ります。前提: 船内の既知の間取りだけを使います。 } */
        private void BuildSearchPoints()
        {
            searchPoints.Clear();
            foreach (var room in arena.Navigation.Rooms)
            {
                if (room.Side != TeamSide.Player) continue;
                var bounds = room.Bounds;
                searchPoints.Add(new Vector2((bounds.minX + bounds.maxX) / 2, (bounds.minY + bounds.maxY) / 2));
            }
            searchPoints.Sort((left, right) => left.sqrMagnitude.CompareTo(right.sqrMagnitude));
        }

        /* { 処理: 共有観測を更新し、傾向推定、目標選択、役割割り当て、弾の担当割り当てを行います。 } */
        private void Decide()
        {
            var now = Time.time;
            PublishObservations(now);
            perception.Refresh(now, arena.EnemyShip);
            RecordTendency();
            CollectAliveProfiles();
            var situation = new EnemyTacticalSituation(arena.EnemyShip.CurrentHull / arena.EnemyShip.MaxHull,
                arena.EnemyShip.IsHullBreached, arena.PlayerShip.IsHullBreached, perception.Intruders.Count,
                perception.OwnAmmo.Count, arena.EnemyCannon.IsLoaded, aliveProfiles.Count);
            var goal = planner.Update(situation, Tendency, clock);
            EnemyRoleAssigner.Assign(aliveProfiles, goal, situation, Tendency, roles);
            AssignAmmoTargets();
        }

        /* { 処理: 行動できる敵船員の本人視界を共有し、行動できない船員の報告を撤回します。 } */
        private void PublishObservations(float now)
        {
            foreach (var controller in controllers)
                perception.Publish(controller.MemberId, controller.Sensor, controller.IsAvailable, now);
        }

        /* { 処理: 被弾、観測した相手の弾、観測した侵入者を推定器へ記録し、現在の傾向を更新します。 } */
        private void RecordTendency()
        {
            // 自船体の減少は砲撃を受けた公開情報として記録します。
            var hull = arena.EnemyShip.CurrentHull;
            if (hull < lastHull) estimator.RecordShellHit(clock);
            lastHull = hull;
            foreach (var ammo in perception.PlayerAmmo) estimator.RecordObservedAmmo(ammo.TargetId, ammo.AmmoWeight);
            estimator.RecordIntruderPresence(clock, perception.Intruders.Count);
            Tendency = estimator.GetTendency(clock);
        }

        /* { 処理: 生存して行動できる船員の能力を、編成順の値として集めます。 } */
        private void CollectAliveProfiles()
        {
            aliveProfiles.Clear();
            foreach (var controller in controllers)
                if (controller.IsAvailable) aliveProfiles.Add(controller.CreateProfile());
        }

        /* { 処理: 補給役が重ならないよう、各自の最寄りの未担当の弾を割り当てます。 } */
        private void AssignAmmoTargets()
        {
            ammoTargets.Clear();
            claimedAmmo.Clear();
            foreach (var controller in controllers)
            {
                if (GetRole(controller.MemberId) != EnemyTacticalRole.Supplier) continue;
                var target = FindNearestUnclaimedAmmo(controller.transform.position);
                if (target == null) continue;
                ammoTargets[controller.MemberId] = target.TargetId;
                claimedAmmo.Add(target.TargetId);
            }
        }

        /* { 処理: 指定位置に最も近く、他の補給役が担当していない自陣営の弾の観測を返します。なければnullです。 } */
        private VisionObservation FindNearestUnclaimedAmmo(Vector2 from)
        {
            VisionObservation nearest = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var observation in perception.OwnAmmo)
            {
                if (claimedAmmo.Contains(observation.TargetId)) continue;
                var distance = (new Vector2(observation.X, observation.Y) - from).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = observation;
                nearestDistance = distance;
            }
            return nearest;
        }

        /* { 処理: 無効化時に共有観測を破棄し、次の戦闘へ持ち越しません。 } */
        private void OnDisable() => perception.Clear();
    }
}
