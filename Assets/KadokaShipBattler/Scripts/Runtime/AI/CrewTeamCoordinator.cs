using System.Collections.Generic;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 一つのプレイヤーチームの観測と予約をUnityのライフサイクルへ接続します。
        前提: 敵の現在状態を走査しません。共有するのは本人のセンサーと自船の既知状態です。
    } */
    [DisallowMultipleComponent]
    public sealed class CrewTeamCoordinator : MonoBehaviour
    {
        private readonly Dictionary<int, CrewTaskAssignment> assignments = new();
        public TaskReservationBoard Reservations { get; } = new();
        public SharedObservationBoard Observations { get; } = new(TeamSide.Player);
        public BattlePrototype Arena { get; private set; }

        /* { 処理: 方針と同じ戦闘GameObjectを接続先として取得します。 } */
        private void Awake() => Arena = GetComponent<BattlePrototype>();

        /* { 処理: 同じ戦闘のプレイヤー船員を登録します。戻り値: 所属違いや重複はfalseです。 } */
        public bool Register(CrewTaskAssignment assignment)
        {
            // 他チームや別のシーンから予約・観測を持ち込みません。
            if (assignment == null || Arena == null || assignment.Actor.Arena != Arena || assignment.Actor.TeamSide != TeamSide.Player) return false;
            var ownerId = assignment.OwnerId;
            if (assignments.ContainsKey(ownerId)) return false;
            assignments.Add(ownerId, assignment);
            Observations.RegisterReporter(ownerId, TeamSide.Player);
            return true;
        }

        /* { 処理: 登録済みの本人の現在の視界を共有します。前提: 外部の対象一覧を受け付けません。 } */
        public void Publish(CrewTaskAssignment assignment)
        {
            // センサー停止、死亡、無効化では以前の報告を残しません。
            if (assignment == null || !assignments.ContainsKey(assignment.OwnerId)) return;
            var actor = assignment.Actor;
            var sensor = assignment.Sensor;
            if (actor == null || sensor == null || !actor.IsAvailable || !sensor.isActiveAndEnabled || !assignment.isActiveAndEnabled)
            {
                Observations.ClearReporter(assignment.OwnerId);
                return;
            }
            Observations.Publish(assignment.OwnerId, sensor.Observations, Time.time);
        }

        /* { 処理: 現在の観測報告と、更新されない予約の回収を行います。 } */
        private void LateUpdate()
        {
            // 終了した戦闘では観測と担当を再作成しません。
            if (Arena == null || Arena.IsFinished)
            {
                Reservations.Clear();
                Observations.Clear();
                return;
            }
            foreach (var assignment in assignments.Values)
            {
                if (assignment == null) continue;
                if (!assignment.CanReserve) assignment.Release();
                Publish(assignment);
            }
            Reservations.Expire(Time.time);
        }

        /* { 処理: 取得済みや再観測で無効と分かった対象を共有と予約から除きます。 } */
        public void ForgetTarget(TaskTarget target)
        {
            // 未観測の現在状態を検索して無効化することはありません。
            Observations.ForgetTarget(target.TargetId);
            Reservations.ReleaseTarget(target);
        }

        /* { 処理: 船員コンポーネント破棄時に、予約、報告、登録参照を解除します。 } */
        public void Unregister(CrewTaskAssignment assignment)
        {
            // 破棄後のUnity参照を繰り返し追いません。
            var ownerId = assignment.OwnerId;
            Reservations.ReleaseOwner(ownerId);
            Observations.UnregisterReporter(ownerId);
            assignments.Remove(ownerId);
        }

        /* { 処理: 無効化・シーン破棄時にチームの一時状態を解除します。 } */
        private void OnDisable()
        {
            // 保存対象ではない戦闘内の情報を次の戦闘へ残しません。
            Reservations.Clear();
            Observations.Clear();
        }
    }
}
