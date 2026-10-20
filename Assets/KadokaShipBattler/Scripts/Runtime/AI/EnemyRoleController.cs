using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using KadokaShipBattler.Navigation;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 敵船員一人に割り当てられた役割を、経路探索と本人の視界を使って実行します。
        前提: 攻撃はCrewGuardAiControllerが担当します。この実行器は移動、拾得、装填、発射だけを行います。
              共有観測へ移動することは許可しますが、拾得は本人が現在見ている弾に限ります。
    } */
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCrewController), typeof(VisionSensor), typeof(NavigationAgent))]
    public sealed class EnemyRoleController : MonoBehaviour
    {
        private EnemyTacticalDirector director;
        private BattlePrototype arena;
        private PlayerCrewController actor;
        private CrewMember crew;
        private CrewAmmoInventory inventory;
        private NavigationAgent navigation;
        private int slot;

        public int MemberId { get; private set; }
        public VisionSensor Sensor { get; private set; }
        public bool IsAvailable => actor != null && actor.IsAvailable && Sensor != null && Sensor.isActiveAndEnabled;

        /* { 処理: 実行に使うコンポーネントと、戦闘内で一意な船員IDを取得します。 } */
        private void Awake()
        {
            // 毎フレーム同じ参照を探し直しません。
            actor = GetComponent<PlayerCrewController>();
            crew = GetComponent<CrewMember>();
            inventory = GetComponent<CrewAmmoInventory>();
            navigation = GetComponent<NavigationAgent>();
            Sensor = GetComponent<VisionSensor>();
            MemberId = actor.GetInstanceID();
        }

        /* { 処理: 戦術ディレクターと戦闘を接続します。引数: memberSlotは敵編成での順番で、0がリーダーです。 } */
        public void Connect(EnemyTacticalDirector tacticalDirector, BattlePrototype battle, int memberSlot)
        {
            director = tacticalDirector;
            arena = battle;
            slot = memberSlot;
        }

        /* { 処理: 役割割り当てに使う、現在の能力値を作ります。前提: 保持数が0の船員は運搬容量0として扱います。 } */
        public EnemyCrewProfile CreateProfile()
        {
            var definition = crew.Definition;
            var carryCapacity = definition.MaxCarryCount > 0 ? definition.CarryCapacity : 0;
            return new EnemyCrewProfile(MemberId, slot, definition.Capabilities & ~CharacterCapability.Repair,
                definition.AttackMode != NormalAttackMode.None, carryCapacity);
        }

        /* { 処理: 割り当て済みの役割に応じて、移動または操作を一段階進めます。引数: deltaTimeは有限の正の秒数です。 } */
        public void Tick(EnemyTacticalRole role, float deltaTime)
        {
            // 行動できない船員や終了した戦闘では、経路だけを解除します。
            if (!IsAvailable || arena == null || arena.IsFinished)
            {
                navigation.Cancel();
                return;
            }
            switch (role)
            {
                case EnemyTacticalRole.Gunner: TickGunner(deltaTime); break;
                case EnemyTacticalRole.Supplier: TickSupplier(deltaTime); break;
                case EnemyTacticalRole.Defender: TickDefender(deltaTime); break;
                case EnemyTacticalRole.Boarder: TickBoarder(deltaTime); break;
            }
        }

        /* { 処理: 砲手として、弾を持つか装填済みなら砲台へ行き、装填と発射間隔を満たした発射を行います。弾がなければ本人が見える弾を探します。 } */
        private void TickGunner(float deltaTime)
        {
            var cannon = arena.EnemyCannon;
            if (!cannon.IsLoaded && !inventory.HasAmmo)
            {
                FetchVisibleAmmo(deltaTime);
                return;
            }
            if (IsStillMoving(cannon.transform.position, deltaTime)) return;
            if (!cannon.IsLoaded) cannon.TryLoadFrom(inventory);
            if (director.IsCannonReady && cannon.FireAt(arena.PlayerShip)) director.NotifyCannonFired();
        }

        /* { 処理: 補給役として、持つ弾を砲台へ装填します。弾がなければ見える弾、次に共有された弾の位置へ向かいます。 } */
        private void TickSupplier(float deltaTime)
        {
            if (inventory.HasAmmo)
            {
                DeliverToCannon(deltaTime);
                return;
            }
            if (FetchVisibleAmmo(deltaTime)) return;
            FollowAssignedAmmo(deltaTime);
        }

        /* { 処理: 砲台まで移動し、砲台が空なら持っている弾を装填します。発射は砲手が行います。 } */
        private void DeliverToCannon(float deltaTime)
        {
            var cannon = arena.EnemyCannon;
            if (IsStillMoving(cannon.transform.position, deltaTime)) return;
            if (!cannon.IsLoaded) cannon.TryLoadFrom(inventory);
        }

        /* {
            処理: 本人の視界にある拾える弾のうち最も近いものへ向かい、近づいたら拾います。
            戻り値: 拾える弾を見ている間はtrueです。
        } */
        private bool FetchVisibleAmmo(float deltaTime)
        {
            // 見えない弾は、他の船員の報告があっても本人の視界で確認するまで拾いません。
            Sensor.Scan();
            var nearest = FindNearestVisibleAmmo();
            if (nearest == null) return false;
            if (!IsStillMoving(new Vector2(nearest.X, nearest.Y), deltaTime)) Sensor.TryPickupVisible(nearest, inventory);
            return true;
        }

        /* { 処理: 本人が今見ている自陣営の弾のうち、保持できる最も近いものを返します。なければnullです。 } */
        private VisionObservation FindNearestVisibleAmmo()
        {
            VisionObservation nearest = null;
            var nearestDistance = float.PositiveInfinity;
            foreach (var observation in Sensor.Observations)
            {
                if (observation.Kind != ObservedTargetKind.Ammo || observation.TeamSide != TeamSide.Enemy) continue;
                var pickup = Sensor.GetVisibleAmmo(observation);
                if (pickup == null || !inventory.CanPickup(pickup.Round)) continue;
                var distance = ((Vector2)transform.position - new Vector2(observation.X, observation.Y)).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = observation;
                nearestDistance = distance;
            }
            return nearest;
        }

        /* { 処理: 共有された弾の位置へ移動します。到着しても本人が見えない、または通行できない時は、その報告を共有から除きます。 } */
        private void FollowAssignedAmmo(float deltaTime)
        {
            if (!director.TryGetAmmoTarget(MemberId, out var target))
            {
                navigation.Cancel();
                return;
            }
            var status = navigation.MoveTo(new Vector2(target.X, target.Y), deltaTime);
            if (status == NavigationStatus.Arrived || status == NavigationStatus.Blocked) director.ForgetTarget(target.TargetId);
        }

        /* { 処理: 迎撃役として、観測済みの侵入者へ近づきます。観測中の侵入者がいなければ、その場で待機します。 } */
        private void TickDefender(float deltaTime)
        {
            if (!director.TryGetNearestIntruder(transform.position, out var intruder))
            {
                navigation.Cancel();
                return;
            }
            var stopDistance = Mathf.Clamp(crew.Definition.AttackRange * 0.8f, 0.3f, 0.7f);
            var offset = new Vector2(intruder.X, intruder.Y) - (Vector2)transform.position;
            if (offset.sqrMagnitude <= stopDistance * stopDistance)
            {
                navigation.Cancel();
                actor.Face(offset);
                return;
            }
            navigation.MoveTo(new Vector2(intruder.X, intruder.Y), deltaTime);
        }

        /* { 処理: 乗り込み役として、観測済みの核、なければ捜索地点へ移動します。到達または通行不可なら次の捜索地点へ進めます。 } */
        private void TickBoarder(float deltaTime)
        {
            var destination = director.GetBoardingDestination(MemberId, slot);
            var status = navigation.MoveTo(destination, deltaTime);
            if (status == NavigationStatus.Arrived || status == NavigationStatus.Blocked) director.AdvanceSearchPoint(MemberId);
        }

        /* { 処理: 目的地まで経路で移動します。戻り値: 到着していなければtrueで、通行不可の場合も次のフレームで再試行するためtrueです。 } */
        private bool IsStillMoving(Vector2 destination, float deltaTime) =>
            navigation.MoveTo(destination, deltaTime) != NavigationStatus.Arrived;

        /* { 処理: 無効化時に経路を解除します。 } */
        private void OnDisable() => navigation?.Cancel();
    }
}
