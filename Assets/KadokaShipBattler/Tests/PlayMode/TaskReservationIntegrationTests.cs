using System.Collections;
using System.Linq;
using KadokaShipBattler.AI;
using KadokaShipBattler.Characters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KadokaShipBattler.Tests
{
    /* { 責務: 複数船員の判断と本番の実行器が、予約と引継ぎを守ることを確認します。 } */
    public sealed class TaskReservationIntegrationTests : CrewCoordinationFixture
    {
        /* { 処理: 同じ弾を見た船員が別の実在弾を担当することを確認します。 } */
        [Test]
        public void TwoCrewChooseDifferentAmmoAndCompleteOneAmmoLoop()
        {
            // 第一担当が確保した弾を、第二担当の採点から除きます。
            var first = PlaceCrew(0, new Vector2(-5, -0.5f), Vector2.down);
            var second = PlaceCrew(1, new Vector2(-5.1f, -0.5f), Vector2.down);
            PlaceAmmo(0, new Vector2(-5, -1));
            PlaceAmmo(1, new Vector2(-5, -1.7f));
            var firstAi = RunCrew(first);
            RunCrew(second);
            var firstReservation = first.GetComponent<CrewTaskAssignment>().CurrentReservation.Value;
            var secondReservation = second.GetComponent<CrewTaskAssignment>().CurrentReservation.Value;
            Assert.That(firstReservation.Target.Kind, Is.EqualTo(TaskTargetKind.Ammo));
            Assert.That(secondReservation.Target.TargetId, Is.Not.EqualTo(firstReservation.Target.TargetId));
            Assert.That(second.GetComponent<UtilityAiAgent>().Scores.Any(score => score.Candidate.TargetId == firstReservation.Target.TargetId), Is.False);

            // 予約を使う状態でも、取得、装填、発射を完了します。
            for (var tick = 0; tick < 250 && Arena.EnemyShip.CurrentHull == 100; tick++) firstAi.Tick(0.02f);
            Assert.That(Arena.EnemyShip.CurrentHull, Is.EqualTo(75));
            Assert.That(Coordinator.Reservations.IsCurrent(firstReservation, Time.time), Is.False);
        }

        /* { 処理: 同じ砲台への装填担当を一人にし、完了後に別の砲手へ渡すことを確認します。 } */
        [Test]
        public void CannonLoadingAndFiringUseOneReservation()
        {
            // 同じ砲台を異なる行動として同時予約しません。
            var first = PlaceCrew(0, new Vector2(-5, -0.5f), Vector2.down);
            var second = PlaceCrew(1, new Vector2(-5.1f, -0.5f), Vector2.down);
            first.GetComponent<CrewAmmoInventory>().TryPickup(Arena.PlayerAmmo.Pickups[0].Definition);
            second.GetComponent<CrewAmmoInventory>().TryPickup(Arena.PlayerAmmo.Pickups[1].Definition);
            var firstAi = RunCrew(first);
            RunCrew(second);
            Assert.That(first.GetComponent<UtilityAiAgent>().CurrentAction, Is.EqualTo(AiActionType.LoadCannon));
            Assert.That(second.GetComponent<UtilityAiAgent>().Scores.Any(score => score.Candidate.TargetId == Arena.PlayerCannon.GetInstanceID()), Is.False);
            for (var tick = 0; tick < 250 && !Arena.PlayerCannon.IsLoaded; tick++) firstAi.Tick(0.02f);
            Assert.That(Arena.PlayerCannon.IsLoaded, Is.True);
            var secondUtility = second.GetComponent<UtilityAiAgent>();
            secondUtility.SelectAction();
            Assert.That(secondUtility.CurrentAction, Is.EqualTo(AiActionType.OperateCannon));
            Assert.That(second.GetComponent<CrewTaskAssignment>().CurrentReservation.Value.Target.Kind, Is.EqualTo(TaskTargetKind.Cannon));
        }

        /* { 処理: 砲台専門の船員へ引き継ぎ、旧担当が操作しないことを確認します。 } */
        [Test]
        public void SpecialistPreemptsAndOldOwnerCannotFire()
        {
            // 同じ射程内でも、現在の予約を持つ船員だけが操作します。
            var first = PlaceCrew(0, new Vector2(-2, 0.3f), Vector2.left);
            var specialist = PlaceCrew(1, new Vector2(-2.2f, 0.2f), Vector2.left);
            first.GetComponent<CrewAmmoInventory>().TryPickup(Arena.PlayerAmmo.Pickups[0].Definition);
            Arena.PlayerCannon.TryLoadFrom(first.GetComponent<CrewAmmoInventory>());
            var firstAi = RunCrew(first);
            var oldReservation = first.GetComponent<CrewTaskAssignment>().CurrentReservation.Value;
            Arena.GetComponent<CrewPolicyDirector>().TrySetIndividualPolicy(specialist, AiPolicyId.Gunner);
            var specialistAi = RunCrew(specialist);
            Assert.That(Coordinator.Reservations.IsCurrent(oldReservation, Time.time), Is.False);
            Assert.That(specialist.GetComponent<CrewTaskAssignment>().IsCurrent(), Is.True);
            firstAi.Tick(0.02f);
            Assert.That(Arena.EnemyShip.CurrentHull, Is.EqualTo(100));
            specialistAi.Tick(0.02f);
            Assert.That(Arena.EnemyShip.CurrentHull, Is.EqualTo(75));
        }

        /* { 処理: 操作切替と実行器停止で予約を回収し、次の船員が取得できることを確認します。 } */
        [Test]
        public void ControlSwitchAndDisableReleaseAssignments()
        {
            // 直接操作中にAIの担当を保持しません。
            var first = PlaceCrew(0, new Vector2(-5, -0.5f), Vector2.down);
            var second = PlaceCrew(1, new Vector2(-5.1f, -0.5f), Vector2.down);
            PlaceAmmo(0, new Vector2(-5, -1));
            RunCrew(first);
            var old = first.GetComponent<CrewTaskAssignment>().CurrentReservation.Value;
            Assert.That(Arena.Controls.TrySwitch(first), Is.True);
            Assert.That(Coordinator.Reservations.IsCurrent(old, Time.time), Is.False);
            var secondAi = RunCrew(second);
            Assert.That(second.GetComponent<CrewTaskAssignment>().CurrentReservation.Value.Target.Equals(old.Target), Is.True);
            secondAi.enabled = false;
            Assert.That(second.GetComponent<CrewTaskAssignment>().CurrentReservation, Is.Null);
            Assert.That(Coordinator.Reservations.Count, Is.EqualTo(0));
        }

        /* { 処理: 死亡と戦闘終了で予約が残らず、他の船員へ再割当できることを確認します。 } */
        [Test]
        public void DeathAndBattleEndDoNotLeaveDeadlock()
        {
            // 死亡後の最初の実行更新で担当を回収します。
            var first = PlaceCrew(0, new Vector2(-5, -0.5f), Vector2.down);
            var second = PlaceCrew(1, new Vector2(-5.1f, -0.5f), Vector2.down);
            PlaceAmmo(0, new Vector2(-5, -1));
            var firstAi = RunCrew(first);
            var old = first.GetComponent<CrewTaskAssignment>().CurrentReservation.Value;
            first.GetComponent<CrewMember>().ApplyDamage(1000);
            firstAi.Tick(0.02f);
            Assert.That(Coordinator.Reservations.IsCurrent(old, Time.time), Is.False);
            var secondAi = RunCrew(second);
            Assert.That(second.GetComponent<CrewTaskAssignment>().IsCurrent(), Is.True);
            Arena.EnemyShip.ApplyDamage(100);
            Arena.EnemyShip.TryDamageCore(30);
            secondAi.Tick(0.02f);
            Assert.That(Coordinator.Reservations.Count, Is.EqualTo(0));
        }

        /* { 処理: 移動能力がない船員が担当を保持し続けないことを確認します。 } */
        [Test]
        public void ImmobileCrewReleasesUnreachableWork()
        {
            // 能力フラグがあっても移動できなければ、別の担当へ渡します。
            var first = PlaceCrew(0, new Vector2(-5, -0.5f), Vector2.down);
            var stats = JsonUtility.FromJson<CharacterStats>(JsonUtility.ToJson(Arena.CrewSetup.characters[0]));
            stats.moveSpeed = 0;
            first.GetComponent<CrewMember>().Definition.Initialize(stats);
            PlaceAmmo(0, new Vector2(-5, -1.5f));
            var ai = RunCrew(first);
            Assert.That(first.GetComponent<CrewTaskAssignment>().IsCurrent(), Is.True);
            ai.Tick(0.02f);
            Assert.That(first.GetComponent<CrewTaskAssignment>().CurrentReservation, Is.Null);
            first.GetComponent<UtilityAiAgent>().SelectAction();
            Assert.That(first.GetComponent<UtilityAiAgent>().CurrentAction, Is.EqualTo(AiActionType.Idle));
        }

        /* { 処理: 船員破棄時に予約と共有報告を回収します。 } */
        [UnityTest]
        public IEnumerator DestroyedCrewDoesNotKeepReservation()
        {
            // 一部の船員だけが消えてもチーム台帳を残せるようにします。
            var first = PlaceCrew(0, new Vector2(-5, -0.5f), Vector2.down);
            PlaceAmmo(0, new Vector2(-5, -1));
            RunCrew(first);
            var old = first.GetComponent<CrewTaskAssignment>().CurrentReservation.Value;
            Object.Destroy(first.gameObject);
            yield return null;
            Assert.That(Coordinator.Reservations.IsCurrent(old, Time.time), Is.False);
        }
    }
}
