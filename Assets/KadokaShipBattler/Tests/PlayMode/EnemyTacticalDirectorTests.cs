using System.Collections;
using KadokaShipBattler.AI;
using KadokaShipBattler.Ammo;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KadokaShipBattler.Tests
{
    /* { 責務: 実際のシーンで、敵チームの共有戦術目標、役割、観測共有の境界を確認します。 } */
    public sealed class EnemyTacticalDirectorTests
    {
        private const float TickSeconds = 0.1f;
        private BattlePrototype arena;

        /* { 処理: 弾の出現、壁の視線遮蔽、弾と核の当たりを止めた実際のシーンを準備します。 } */
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 敵チームの判断だけを分離するため、プレイヤー側の自動行動を止めます。
            yield return SceneManager.LoadSceneAsync("BattlePrototype");
            arena = Object.FindFirstObjectByType<BattlePrototype>();
            arena.AllyActionsEnabled = false;
            arena.PlayerAmmo.SpawningEnabled = false;
            arena.EnemyAmmo.SpawningEnabled = false;
            foreach (var wall in Object.FindObjectsByType<VisionBlocker>(FindObjectsSortMode.None)) wall.GetComponent<Collider2D>().enabled = false;
            foreach (var pickup in Object.FindObjectsByType<AmmoPickup>(FindObjectsSortMode.None)) pickup.GetComponent<Collider2D>().enabled = false;
            arena.EnemyActionsEnabled = true;
        }

        /* { 処理: 戦闘の全オブジェクトを破棄します。 } */
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (arena != null) Object.Destroy(arena.gameObject);
            yield return null;
        }

        /* { 処理: 敵船員のスロットから、現在割り当てられている役割を返します。 } */
        private EnemyTacticalRole RoleOf(int slot) =>
            arena.EnemyTactics.GetRole(arena.EnemyCrew[slot].GetComponent<EnemyRoleController>().MemberId);

        /* { 処理: 全ての敵船員の本人視界を更新して、戦術を一段階進めます。 } */
        private void ScanAndTick()
        {
            foreach (var member in arena.EnemyCrew) member.GetComponent<VisionSensor>().Scan();
            arena.TickEnemy(TickSeconds);
        }

        /* { 処理: 指定スロットの敵船員を配置して向きを固定します。 } */
        private CrewMember Place(int slot, Vector2 point, Vector2 facing)
        {
            var member = arena.EnemyCrew[slot];
            member.transform.position = point;
            member.GetComponent<PlayerCrewController>().Face(facing);
            return member;
        }

        /* { 処理: 平常時は砲撃を共有目標にし、リーダーが砲手、運搬員が補給役になることを確認します。 } */
        [Test]
        public void CalmBattleSharesBombardGoalWithGunnerAndSupplier()
        {
            arena.TickEnemy(TickSeconds);
            Assert.That(arena.EnemyTactics.Goal, Is.EqualTo(EnemyTacticalGoal.Bombard));
            Assert.That(RoleOf(0), Is.EqualTo(EnemyTacticalRole.Gunner));
            Assert.That(RoleOf(2), Is.EqualTo(EnemyTacticalRole.Supplier));
        }

        /* { 処理: プレイヤー船の橋が開くと、砲撃から乗り込みへ切り替わり、乗り込み役が橋を渡ることを確認します。 } */
        [Test]
        public void OpenedBridgeSwitchesTeamToBoardingAndLeaderCrossesIt()
        {
            arena.PlayerShip.ApplyDamage(arena.PlayerShip.MaxHull);
            arena.TickEnemy(TickSeconds);
            Assert.That(arena.EnemyTactics.Goal, Is.EqualTo(EnemyTacticalGoal.BoardEnemy));
            Assert.That(RoleOf(0), Is.EqualTo(EnemyTacticalRole.Boarder));
            Assert.That(RoleOf(4), Is.EqualTo(EnemyTacticalRole.Boarder));
            Assert.That(RoleOf(2), Is.EqualTo(EnemyTacticalRole.None));
            var leader = arena.EnemyCrew[0];
            for (var step = 0; step < 150 && leader.transform.position.x > -1; step++) ScanAndTick();
            Assert.That(leader.transform.position.x, Is.LessThan(-1));
        }

        /* { 処理: 自船内で複数の侵入者を観測し続けると、保持時間を過ぎて迎撃へ切り替わることを確認します。 } */
        [Test]
        public void ObservedIntrudersSwitchTeamToDefenseAfterHoldTime()
        {
            Place(0, new Vector2(2, 0), Vector2.down);
            arena.PlayerCrew[0].transform.position = new Vector3(2, -1.2f);
            arena.PlayerCrew[1].transform.position = new Vector3(2.6f, -1.4f);
            ScanAndTick();
            Assert.That(arena.EnemyTactics.Goal, Is.EqualTo(EnemyTacticalGoal.Bombard));
            for (var step = 0; step < 40; step++) ScanAndTick();
            Assert.That(arena.EnemyTactics.Goal, Is.EqualTo(EnemyTacticalGoal.DefendShip));
            Assert.That(RoleOf(0), Is.EqualTo(EnemyTacticalRole.Defender));
            Assert.That(arena.EnemyTactics.Tendency.BoardingPressure, Is.GreaterThan(0));
        }

        /* { 処理: 全ての敵船員の視野の外に侵入者がいる間は、同じ甲板上でも目標を変えないことを確認します。 } */
        [Test]
        public void IntrudersOutsideEveryViewDoNotChangeTactics()
        {
            // 敵船員は全員が上を向き、侵入者は全員より下の甲板端にいます。
            foreach (var member in arena.EnemyCrew) member.GetComponent<PlayerCrewController>().Face(Vector2.up);
            arena.PlayerCrew[0].transform.position = new Vector3(2, -2.4f);
            arena.PlayerCrew[1].transform.position = new Vector3(2.6f, -2.4f);
            arena.PlayerCrew[2].transform.position = new Vector3(3, -2.4f);
            for (var step = 0; step < 40; step++) ScanAndTick();
            Assert.That(RoleOf(0), Is.EqualTo(EnemyTacticalRole.Gunner));
            Assert.That(arena.EnemyTactics.Goal, Is.EqualTo(EnemyTacticalGoal.Bombard));
            Assert.That(arena.EnemyTactics.Perception.Intruders.Count, Is.EqualTo(0));
        }

        /* { 処理: 自船体が砲撃を受けたことを、プレイヤーの砲撃傾向として記録することを確認します。 } */
        [Test]
        public void HullDamageIsRecordedAsPlayerShelling()
        {
            arena.EnemyShip.ApplyDamage(25);
            arena.TickEnemy(TickSeconds);
            Assert.That(arena.EnemyTactics.Tendency.ShellsPerMinute, Is.GreaterThan(0));
        }

        /* { 処理: 一人が見つけた弾を補給役が共有位置へ取りに行き、砲台へ装填し、砲手が発射するまでを確認します。 } */
        [Test]
        public void SharedAmmoSightingLetsSupplierLoadAndGunnerFire()
        {
            PlaceSupplyScenario(withObserver: true);
            for (var step = 0; step < 400 && arena.PlayerShip.CurrentHull == arena.PlayerShip.MaxHull; step++) ScanAndTick();
            Assert.That(arena.PlayerShip.CurrentHull, Is.LessThan(arena.PlayerShip.MaxHull));
        }

        /* { 処理: 誰も弾を見ていなければ、補給役が弾の位置を知らずに動かないことを確認します。 } */
        [Test]
        public void SupplierDoesNotMoveWithoutAnyAmmoSighting()
        {
            PlaceSupplyScenario(withObserver: false);
            var carrier = arena.EnemyCrew[2];
            var start = carrier.transform.position;
            for (var step = 0; step < 40; step++) ScanAndTick();
            Assert.That(carrier.transform.position, Is.EqualTo(start));
            Assert.That(carrier.GetComponent<CrewAmmoInventory>().HasAmmo, Is.False);
        }

        /* {
            処理: 同じ甲板で、リーダーと運搬員が弾に背を向け、偵察員だけが弾を見られる配置にします。
            引数: withObserverがfalseなら偵察員を無効にし、誰も弾を観測しない状態にします。
        } */
        private void PlaceSupplyScenario(bool withObserver)
        {
            // 遠い船員は砲手と補給役に使うため、他の2人を除外して役割を単純にします。
            arena.EnemyCrew[1].gameObject.SetActive(false);
            arena.EnemyCrew[4].gameObject.SetActive(false);
            Place(0, new Vector2(1.5f, -2), Vector2.down);
            Place(2, new Vector2(2, -1.6f), Vector2.down);
            var observer = Place(3, new Vector2(3.5f, 0.6f), Vector2.up);
            observer.gameObject.SetActive(withObserver);
            var pickup = arena.EnemyAmmo.Pickups[0];
            pickup.transform.position = new Vector3(3.5f, 1.6f);
            pickup.GetComponent<Collider2D>().enabled = true;
        }
    }
}
