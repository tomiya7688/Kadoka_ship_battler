using System.Collections;
using System.Linq;
using KadokaShipBattler.AI;
using KadokaShipBattler.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KadokaShipBattler.Tests
{
    /* { 責務: 共有観測による移動と本人の再確認、共有の解除を実際のシーンで検証します。 } */
    public sealed class SharedPerceptionIntegrationTests : CrewCoordinationFixture
    {
        /* { 処理: 本人の視界外にある共有弾へ移動し、再観測して取得・装填・発射することを確認します。 } */
        [Test]
        public void SharedAmmoGuidesMovementAndActualAmmoCycle()
        {
            // 観測する船員を動かさず、共有を受けた船員だけを進めます。
            var observer = PlaceCrew(3, new Vector2(-5, -0.5f), Vector2.down);
            var recipient = PlaceCrew(0, new Vector2(-5, 0.6f), Vector2.up);
            var pickup = PlaceAmmo(0, new Vector2(-5, -1.2f));
            Report(observer);
            var ai = RunCrew(recipient);
            var utility = recipient.GetComponent<UtilityAiAgent>();
            Assert.That(utility.CurrentAction, Is.EqualTo(AiActionType.CarryAmmo));
            Assert.That(utility.Decision.Selected.TargetId, Is.EqualTo(pickup.GetInstanceID()));
            Assert.That(utility.SelectedObservation, Is.Null, "Shared report is not a local action reference");
            Assert.That(recipient.GetComponent<CrewAmmoInventory>().Count, Is.EqualTo(0));
            for (var tick = 0; tick < 300 && Arena.EnemyShip.CurrentHull == 100; tick++) ai.Tick(0.02f);
            Assert.That(Arena.EnemyShip.CurrentHull, Is.EqualTo(75));
            Assert.That(Coordinator.Observations.GetCurrent(Time.time).Any(sample => sample.TargetId == pickup.GetInstanceID()), Is.False);
        }

        /* { 処理: 自分の視界にない共有弾を、近距離でも盲目的に取得しないことを確認します。 } */
        [Test]
        public void SharedSampleCannotAuthorizeBlindPickup()
        {
            // 共有情報だけで取得せず、本人の向きと視界を維持します。
            var observer = PlaceCrew(3, new Vector2(-5, -0.5f), Vector2.down);
            var recipient = PlaceCrew(0, new Vector2(-5, -1.3f), Vector2.up);
            var pickup = PlaceAmmo(0, new Vector2(-5, -1.6f));
            Report(observer);
            var ai = RunCrew(recipient);
            Assert.That(recipient.GetComponent<UtilityAiAgent>().SelectedObservation, Is.Null);
            ai.Tick(0.02f);
            Assert.That(recipient.GetComponent<CrewAmmoInventory>().Count, Is.EqualTo(0));
            Assert.That(pickup.IsConsumed, Is.False);
            Assert.That(recipient.GetComponent<CrewTaskAssignment>().CurrentReservation, Is.Null);
        }

        /* { 処理: 共有された侵入者へ移動しても、本人の視界に入るまで攻撃しないことを確認します。 } */
        [Test]
        public void SharedInvaderGuidesDefenseWithoutBlindDamage()
        {
            // 操作APIへ味方の観測オブジェクトを渡しません。
            var observer = PlaceCrew(3, new Vector2(-5, -0.5f), Vector2.down);
            var recipient = PlaceCrew(0, new Vector2(-5, 0.6f), Vector2.up);
            var enemy = Arena.EnemyCrew[0];
            enemy.transform.position = new Vector3(-5, -1.6f);
            Report(observer);
            var ai = RunCrew(recipient);
            Assert.That(recipient.GetComponent<UtilityAiAgent>().CurrentAction, Is.EqualTo(AiActionType.DefendShip));
            Assert.That(recipient.GetComponent<UtilityAiAgent>().SelectedObservation, Is.Null);
            ai.Tick(0.02f);
            Assert.That(enemy.CurrentHp, Is.EqualTo(100));
            for (var tick = 0; tick < 100 && enemy.CurrentHp == 100; tick++) ai.Tick(0.02f);
            Assert.That(enemy.CurrentHp, Is.LessThan(100));
        }

        /* { 処理: 未報告の移動やHP変更で、共有済みの値が勝手に更新されないことを確認します。 } */
        [Test]
        public void SharedSnapshotsDoNotReadHiddenLiveState()
        {
            // 新しい本人の報告がない限り、前の位置とHPを保持します。
            var observer = PlaceCrew(3, new Vector2(-5, -0.5f), Vector2.down);
            var enemy = Arena.EnemyCrew[0];
            enemy.transform.position = new Vector3(-5, -1.6f);
            Report(observer);
            var sample = Coordinator.Observations.GetCurrent(Time.time).Single(value => value.TargetId == enemy.GetInstanceID());
            enemy.transform.position = new Vector3(5, 1);
            enemy.ApplyDamage(50);
            Assert.That(sample.X, Is.EqualTo(-5));
            Assert.That(sample.Hp, Is.EqualTo(100));
            Report(observer);
            Assert.That(Coordinator.Observations.GetCurrent(Time.time).Any(value => value.TargetId == enemy.GetInstanceID()), Is.False);
        }

        /* { 処理: 別陣営の報告登録を拒否し、操作中の味方の視界を共有することを確認します。 } */
        [Test]
        public void ControlledCrewCanReportAndEnemyCannotRegister()
        {
            // 直接操作中は予約を作らず、本人の視界だけを共有します。
            var observer = PlaceCrew(0, new Vector2(-5, -0.5f), Vector2.down);
            Arena.Controls.TrySwitch(observer);
            var pickup = PlaceAmmo(0, new Vector2(-5, -1.6f));
            Report(observer);
            Assert.That(Coordinator.Observations.GetCurrent(Time.time).Any(value => value.TargetId == pickup.GetInstanceID()), Is.True);
            Assert.That(observer.GetComponent<CrewTaskAssignment>().CurrentReservation, Is.Null);
            Assert.That(Coordinator.Observations.RegisterReporter(999, Core.TeamSide.Enemy), Is.False);
        }

        /* { 処理: センサーと船員の停止で共有情報を回収します。 } */
        [UnityTest]
        public IEnumerator DisabledSensorWithdrawsSharedReport()
        {
            // 新しい本人の観測を作れない状態では、古い報告を出し続けません。
            var observer = PlaceCrew(3, new Vector2(-5, -0.5f), Vector2.down);
            foreach (var actor in Arena.PlayerCrew)
                if (actor != observer) actor.GetComponent<VisionSensor>().enabled = false;
            Coordinator.Observations.Clear();
            var pickup = PlaceAmmo(0, new Vector2(-5, -1.6f));
            Report(observer);
            observer.GetComponent<VisionSensor>().enabled = false;
            yield return null;
            yield return null;
            Assert.That(Coordinator.Observations.GetCurrent(Time.time).Any(value => value.TargetId == pickup.GetInstanceID()), Is.False);
        }

        /* { 処理: 戦闘再開始が共有情報と旧世代の担当を残さないことを確認します。 } */
        [UnityTest]
        public IEnumerator RestartCreatesFreshCoordinationState()
        {
            // 実際のシーン再読込で一時状態の寿命を確認します。
            var first = PlaceCrew(0, new Vector2(-5, -0.5f), Vector2.down);
            PlaceAmmo(0, new Vector2(-5, -1.6f));
            RunCrew(first);
            var previousCoordinator = Coordinator;
            yield return SceneManager.LoadSceneAsync("BattlePrototype");
            Arena = Object.FindFirstObjectByType<Core.BattlePrototype>();
            Arena.AllyActionsEnabled = false;
            Arena.EnemyActionsEnabled = false;
            yield return null;
            Assert.That(Coordinator, Is.Not.SameAs(previousCoordinator));
            Assert.That(Coordinator.Reservations.Count, Is.EqualTo(0));
        }
    }
}
