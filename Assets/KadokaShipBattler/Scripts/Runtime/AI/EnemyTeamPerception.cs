using System.Collections.Generic;
using KadokaShipBattler.Core;
using KadokaShipBattler.Ships;
using UnityEngine;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 敵船員の本人視界を共有し、戦術判断に使う種類別の観測一覧を作ります。
        前提: 共有するのは実際のVisionSensorが得た値だけです。プレイヤー側の現在状態を直接検索しません。
              一覧は次のRefreshまで有効で、永続的な履歴として保持しないでください。
    } */
    public sealed class EnemyTeamPerception
    {
        private readonly SharedObservationBoard board = new(TeamSide.Enemy);
        private readonly List<VisionObservation> intruders = new();
        private readonly List<VisionObservation> ownAmmo = new();
        private readonly List<VisionObservation> playerAmmo = new();

        public IReadOnlyList<VisionObservation> Intruders => intruders;
        public IReadOnlyList<VisionObservation> OwnAmmo => ownAmmo;
        public IReadOnlyList<VisionObservation> PlayerAmmo => playerAmmo;
        public VisionObservation PlayerCore { get; private set; }

        /* { 処理: 敵船員を報告者として登録します。戻り値: 未指定IDや重複はfalseです。 } */
        public bool Register(int reporterId) => board.RegisterReporter(reporterId, TeamSide.Enemy);

        /* { 処理: 登録済みの報告者を共有から外します。 } */
        public void Unregister(int reporterId) => board.UnregisterReporter(reporterId);

        /* {
            処理: 船員本人の現在の視界を共有へ置き換えます。行動できない船員の報告は撤回します。
            引数: canReportは船員が生存し、センサーが有効な時だけtrueです。nowはTime.timeです。
        } */
        public void Publish(int reporterId, VisionSensor sensor, bool canReport, float now)
        {
            // 死亡やセンサー停止で、以前見た情報を残しません。
            if (!canReport || sensor == null)
            {
                board.ClearReporter(reporterId);
                return;
            }
            board.Publish(reporterId, sensor.Observations, now);
        }

        /* {
            処理: 有効な共有観測を、侵入者、自陣営の弾、相手の弾、相手の核へ分類します。
            引数: ownShipは侵入者の判定に使う自船です。nowはTime.timeです。
            計算量: 共有観測数に比例します。
        } */
        public void Refresh(float now, ShipController ownShip)
        {
            // 一覧を再利用し、毎回新しいコレクションを作りません。
            intruders.Clear();
            ownAmmo.Clear();
            playerAmmo.Clear();
            PlayerCore = null;
            var shared = board.GetCurrent(now);
            for (var index = 0; index < shared.Count; index++)
            {
                var observation = shared[index];
                if (observation.Kind == ObservedTargetKind.Crew && IsIntruder(observation, ownShip)) intruders.Add(observation);
                else if (observation.Kind == ObservedTargetKind.Ammo) AddAmmo(observation);
                else if (observation.Kind == ObservedTargetKind.Core && observation.TeamSide == TeamSide.Player) PlayerCore = observation;
            }
        }

        /* { 処理: 取得できなかった対象や再観測に失敗した対象を、全報告から除きます。 } */
        public void ForgetTarget(int targetId) => board.ForgetTarget(targetId);

        /* { 処理: 戦闘終了時に、全報告を解除します。 } */
        public void Clear()
        {
            board.Clear();
            intruders.Clear();
            ownAmmo.Clear();
            playerAmmo.Clear();
            PlayerCore = null;
        }

        /* { 処理: 観測した生存中のプレイヤー船員が、自船の甲板上にいるか判定します。 } */
        private static bool IsIntruder(VisionObservation observation, ShipController ownShip) =>
            observation.TeamSide == TeamSide.Player && observation.Hp > 0 &&
            ownShip.ContainsDeckPoint(new Vector2(observation.X, observation.Y));

        /* { 処理: 弾の観測を持ち主の陣営ごとの一覧へ入れます。陣営不明の弾は使用しません。 } */
        private void AddAmmo(VisionObservation observation)
        {
            if (observation.TeamSide == TeamSide.Enemy) ownAmmo.Add(observation);
            else if (observation.TeamSide == TeamSide.Player) playerAmmo.Add(observation);
        }
    }
}
