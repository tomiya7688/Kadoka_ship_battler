using System.Collections;
using System.Linq;
using KadokaShipBattler.AI;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using KadokaShipBattler.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KadokaShipBattler.Tests
{
    public sealed class NavigationTests
    {
        private BattlePrototype arena;
        [UnitySetUp] public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("BattlePrototype");
            arena = Object.FindFirstObjectByType<BattlePrototype>();
            arena.AllyActionsEnabled = false; arena.EnemyActionsEnabled = false;
        }
        [UnityTearDown] public IEnumerator TearDown() { Object.Destroy(arena.gameObject); yield return null; }
        [Test] public void WalkerUsesDoorInsteadOfPassingThroughWall()
        {
            var actor = arena.PlayerCrew[0]; actor.transform.position = new Vector3(-5, -1.5f);
            actor.Move(Vector2.right, 0.5f);
            Assert.That(actor.transform.position.x, Is.EqualTo(-5));
            var agent = actor.GetComponent<NavigationAgent>();
            for (var i = 0; i < 150 && agent.Status != NavigationStatus.Arrived; i++) agent.MoveTo(new Vector2(-2, 0), 0.02f);
            Assert.That(agent.Status, Is.EqualTo(NavigationStatus.Arrived));
            Assert.That(Vector2.Distance(actor.transform.position, new Vector2(-2, 0)), Is.LessThan(0.001f));
        }
        [Test] public void ScoutUsesFlightAndPhaseShortcuts()
        {
            var actor = arena.PlayerCrew[3];
            actor.transform.position = new Vector3(-5, -1.5f); actor.Move(Vector2.right, 0.4f);
            Assert.That(actor.transform.position.x, Is.EqualTo(-3).Within(0.001f));
            actor.transform.position = new Vector3(-3, 1.7f); actor.Move(Vector2.left, 0.4f);
            Assert.That(actor.transform.position.x, Is.EqualTo(-5).Within(0.001f));
        }
        [Test] public void AgentTraversesTwoPortalsOnSameWallWithoutStalling()
        {
            var actor = arena.PlayerCrew[3]; actor.transform.position = new Vector3(-4.1f, 0);
            var agent = actor.GetComponent<NavigationAgent>();
            for (var i = 0; i < 150 && agent.Status != NavigationStatus.Arrived; i++) agent.MoveTo(new Vector2(-4.1f, 1.7f), 0.02f);
            Assert.That(agent.Status, Is.EqualTo(NavigationStatus.Arrived));
            Assert.That(Vector2.Distance(actor.transform.position, new Vector2(-4.1f, 1.7f)), Is.LessThan(0.001f));
        }
        [Test] public void DoorClosureBlocksAndReopeningReplans()
        {
            var actor = arena.PlayerCrew[0]; actor.transform.position = new Vector3(-5, -1);
            var agent = actor.GetComponent<NavigationAgent>(); agent.MoveTo(new Vector2(-2, 0), 0.02f);
            arena.Navigation.SetDoorOpen("Player/main-door", false);
            var position = actor.transform.position;
            Assert.That(agent.MoveTo(new Vector2(-2, 0), 0.02f), Is.EqualTo(NavigationStatus.Blocked));
            Assert.That(actor.transform.position, Is.EqualTo(position));
            arena.Navigation.SetDoorOpen("Player/main-door", true);
            for (var i = 0; i < 150 && agent.Status != NavigationStatus.Arrived; i++) agent.MoveTo(new Vector2(-2, 0), 0.02f);
            Assert.That(agent.Status, Is.EqualTo(NavigationStatus.Arrived));
        }
        [Test] public void AgentBoardsAndReturnsAcrossBridgeAfterBreach()
        {
            var actor = arena.PlayerCrew[0]; var agent = actor.GetComponent<NavigationAgent>();
            Assert.That(agent.MoveTo(new Vector2(6, 1.5f), 0.02f), Is.EqualTo(NavigationStatus.Blocked));
            arena.EnemyShip.ApplyDamage(100);
            var crossed = false;
            for (var i = 0; i < 400 && agent.Status != NavigationStatus.Arrived; i++)
            {
                agent.MoveTo(new Vector2(6, 1.5f), 0.02f);
                if (Mathf.Abs(actor.transform.position.x) < 1) crossed = true;
            }
            Assert.That(agent.Status, Is.EqualTo(NavigationStatus.Arrived)); Assert.That(crossed, Is.True);
            agent.Cancel();
            for (var i = 0; i < 400 && agent.Status != NavigationStatus.Arrived; i++) agent.MoveTo(new Vector2(-2, 0), 0.02f);
            Assert.That(agent.Status, Is.EqualTo(NavigationStatus.Arrived));
        }
        [Test] public void LoadedAmmoAiUsesRoomPathAndFires()
        {
            var actor = arena.PlayerCrew[0]; actor.transform.position = new Vector3(-5, -1.5f);
            actor.GetComponent<CrewAmmoInventory>().TryPickup(arena.PlayerAmmo.Pickups[0].Definition);
            arena.Controls.CycleNext(); arena.AllyActionsEnabled = true;
            var ai = actor.GetComponent<CrewAmmoAiController>();
            for (var i = 0; i < 220 && arena.EnemyShip.CurrentHull == 100; i++) ai.Tick(0.02f);
            Assert.That(arena.EnemyShip.CurrentHull, Is.EqualTo(75));
        }
        [Test] public void AgentCanNavigateToNamedRoomAndRejectUnknownRoom()
        {
            var actor = arena.PlayerCrew[0]; var agent = actor.GetComponent<NavigationAgent>();
            Assert.That(agent.MoveToRoom("missing", 0.02f), Is.EqualTo(NavigationStatus.Blocked));
            for (var i = 0; i < 150 && agent.Status != NavigationStatus.Arrived; i++) agent.MoveToRoom("Player/core", 0.02f);
            Assert.That(agent.Status, Is.EqualTo(NavigationStatus.Arrived));
            Assert.That(actor.transform.position.y, Is.EqualTo(1.8f).Within(0.001f));
        }
        [Test] public void ControlTakeoverAndDeathCancelMovement()
        {
            var actor = arena.PlayerCrew[1]; var agent = actor.GetComponent<NavigationAgent>();
            agent.MoveTo(new Vector2(-2, 0), 0.02f); arena.Controls.TrySwitch(actor);
            Assert.That(agent.Status, Is.EqualTo(NavigationStatus.Idle));
            actor.GetComponent<CrewMember>().ApplyDamage(1000);
            var position = actor.transform.position; agent.MoveTo(new Vector2(-2, 0), 1);
            Assert.That(actor.transform.position, Is.EqualTo(position)); Assert.That(agent.Status, Is.EqualTo(NavigationStatus.Idle));
        }
        [UnityTest] public IEnumerator RoomWallsAndClosedDoorsOccludeVision()
        {
            var observer = arena.PlayerCrew[0]; observer.transform.position = new Vector3(-4.4f, -1.5f); observer.Face(Vector2.right);
            var target = arena.PlayerCrew[1]; target.transform.position = new Vector3(-3.6f, -1.5f);
            var sensor = observer.GetComponent<VisionSensor>(); sensor.Configure(120, 6);
            Physics2D.SyncTransforms(); sensor.Scan();
            Assert.That(sensor.Observations.Any(o => o.TargetId == target.GetComponent<CrewMember>().GetInstanceID()), Is.False);
            arena.Navigation.SetDoorOpen("Player/phase-door", true);
            yield return null;
            Physics2D.SyncTransforms(); sensor.Scan();
            Assert.That(sensor.Observations.Any(o => o.TargetId == target.GetComponent<CrewMember>().GetInstanceID()), Is.True);
            arena.Navigation.SetDoorOpen("Player/phase-door", false);
            Physics2D.SyncTransforms(); sensor.Scan();
            Assert.That(sensor.Observations.Any(o => o.TargetId == target.GetComponent<CrewMember>().GetInstanceID()), Is.False, "Closure blocks sight immediately");
        }
        [Test] public void InteractionCannotReachThroughRoomWall()
        {
            var actor = arena.PlayerCrew[0]; var pickup = arena.PlayerAmmo.Pickups[0];
            actor.transform.position = new Vector3(-4.3f, -1.5f); pickup.transform.position = new Vector3(-3.7f, -1.5f);
            foreach (var other in arena.PlayerAmmo.Pickups) if (other != pickup) other.transform.position = new Vector3(-6, -2);
            Assert.That(actor.TryInteract(), Is.False); Assert.That(pickup.IsConsumed, Is.False);
        }
        [Test] public void DirectAttackCannotHitThroughRoomWall()
        {
            var actor = arena.PlayerCrew[0]; var target = arena.EnemyCrew[0];
            actor.transform.position = new Vector3(-4.3f, -1.5f); target.transform.position = new Vector3(-3.7f, -1.5f);
            var hp = target.CurrentHp;
            Assert.That(actor.TryAttack(), Is.False); Assert.That(target.CurrentHp, Is.EqualTo(hp));
        }
    }
}
