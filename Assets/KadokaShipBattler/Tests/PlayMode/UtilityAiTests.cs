using System.Collections;
using System.Linq;
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
    public sealed class UtilityAiTests
    {
        private BattlePrototype arena;
        private PlayerCrewController Actor => arena.PlayerCrew[0];
        private UtilityAiAgent Utility => Actor.GetComponent<UtilityAiAgent>();
        [UnitySetUp] public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("BattlePrototype");
            arena = Object.FindFirstObjectByType<BattlePrototype>();
            arena.AllyActionsEnabled = false; arena.EnemyActionsEnabled = false;
            arena.PlayerAmmo.SpawningEnabled = false; arena.EnemyAmmo.SpawningEnabled = false;
            foreach (var pickup in Object.FindObjectsByType<AmmoPickup>(FindObjectsSortMode.None)) pickup.GetComponent<Collider2D>().enabled = false;
            Actor.transform.position = new Vector3(-5, -0.5f); Actor.Face(Vector2.down);
        }
        [UnityTearDown] public IEnumerator TearDown() { Object.Destroy(arena.gameObject); yield return null; }
        private AmmoPickup Ammo(Vector2 position)
        {
            var pickup = arena.PlayerAmmo.Pickups[0]; pickup.transform.position = position;
            pickup.GetComponent<Collider2D>().enabled = true; return pickup;
        }
        private CrewMember Invader(Vector2 position)
        {
            var enemy = arena.EnemyCrew[0]; enemy.transform.position = position; return enemy;
        }
        [Test] public void HiddenAndUnreachableAmmoNeverBecomeCandidates()
        {
            var pickup = Ammo(new Vector2(-5, 0.5f)); Utility.SelectAction();
            Assert.That(Utility.Scores.Any(s => s.Candidate.TargetId == pickup.GetInstanceID()), Is.False);
            Actor.Face(Vector2.up); Utility.SelectAction();
            Assert.That(Utility.CurrentAction, Is.EqualTo(AiActionType.CarryAmmo));
            arena.Navigation.SetDoorOpen("Player/main-door", false);
            pickup.transform.position = new Vector3(-3, 1.7f); Actor.transform.position = new Vector3(-4.3f, 1.7f); Actor.Face(Vector2.right);
            Utility.SelectAction(); Assert.That(Utility.CurrentAction, Is.EqualTo(AiActionType.Idle));
        }
        [Test] public void LearnedWeightsChangeSameObservedDecision()
        {
            Ammo(new Vector2(-5, -1.2f)); Invader(new Vector2(-5, -1));
            Utility.SelectAction(); Assert.That(Utility.CurrentAction, Is.EqualTo(AiActionType.DefendShip));
            Utility.Learning.SetWeight(AiActionType.CarryAmmo, 4); Utility.Learning.SetWeight(AiActionType.DefendShip, 0.25f);
            Utility.SelectAction(); Assert.That(Utility.CurrentAction, Is.EqualTo(AiActionType.CarryAmmo));
            Assert.That(Utility.Scores.Select(s => s.Candidate.Action), Does.Contain(AiActionType.DefendShip));
        }
        [Test] public void SwitchImmediatelyEvaluatesCurrentFacingAndRetainsLearning()
        {
            Invader(new Vector2(-5, -1)); Utility.Learning.SetWeight(AiActionType.DefendShip, 2);
            arena.Controls.CycleNext(); var ai = Actor.GetComponent<CrewAmmoAiController>();
            Assert.That(ai.CurrentAction, Is.EqualTo(AiActionType.DefendShip));
            Assert.That(Utility.ObservationOrigin, Is.EqualTo((Vector2)Actor.transform.position));
            Assert.That(Utility.ObservationFacing, Is.EqualTo(Vector2.down));
            arena.Controls.TrySwitch(Actor); Assert.That(Utility.Decision, Is.Null);
            Assert.That(Utility.Learning.GetWeight(AiActionType.DefendShip), Is.EqualTo(2));
        }
        [Test] public void ActualAiDefendsAndRewardsSuccessfulAttackWithCooldown()
        {
            var enemy = Invader(new Vector2(-5, -1)); arena.Controls.CycleNext(); arena.AllyActionsEnabled = true;
            var ai = Actor.GetComponent<CrewAmmoAiController>(); ai.Tick(0.02f);
            Assert.That(enemy.CurrentHp, Is.EqualTo(75)); Assert.That(Utility.Learning.GetWeight(AiActionType.DefendShip), Is.GreaterThan(1));
            ai.Tick(0.02f); Assert.That(enemy.CurrentHp, Is.EqualTo(75), "Attack cooldown prevents per-frame damage");
            for (var i = 0; i < 22; i++) ai.Tick(0.02f);
            Assert.That(enemy.CurrentHp, Is.LessThan(75));
        }
        [Test] public void ActualAmmoLoopLearnsPickupLoadAndFire()
        {
            Ammo(new Vector2(-5, -1)); arena.Controls.CycleNext(); arena.AllyActionsEnabled = true;
            var ai = Actor.GetComponent<CrewAmmoAiController>();
            for (var i = 0; i < 250 && arena.EnemyShip.CurrentHull == 100; i++) ai.Tick(0.02f);
            Assert.That(arena.EnemyShip.CurrentHull, Is.EqualTo(75));
            foreach (var action in new[] { AiActionType.CarryAmmo, AiActionType.LoadCannon, AiActionType.OperateCannon })
                Assert.That(Utility.Learning.GetWeight(action), Is.GreaterThan(1), action.ToString());
        }
        [Test] public void ScoreSnapshotsDoNotReadHiddenEnemyHpOrPosition()
        {
            var enemy = Invader(new Vector2(-5, -1)); Utility.SelectAction();
            var snapshot = Utility.Decision; var oldScore = snapshot.Scores.Single(s => s.Candidate.Action == AiActionType.DefendShip);
            enemy.transform.position = new Vector3(2, 2); enemy.ApplyDamage(50);
            Assert.That(oldScore.Candidate.TargetHp, Is.EqualTo(100)); Assert.That(oldScore.Candidate.Y, Is.EqualTo(-1));
            Utility.SelectAction(); Assert.That(Utility.Scores.Any(s => s.Candidate.Action == AiActionType.DefendShip), Is.False);
            Assert.That(snapshot.Selected.Action, Is.EqualTo(AiActionType.DefendShip));
        }
        [Test] public void NonCombatCarrierRejectsDefenseAndDefenderRejectsCarrying()
        {
            Ammo(new Vector2(-5, -1)); Invader(new Vector2(-5, -1.2f));
            foreach (var index in new[] { 2, 4 })
            {
                var actor = arena.PlayerCrew[index]; actor.transform.position = Actor.transform.position; actor.Face(Vector2.down);
                var utility = actor.GetComponent<UtilityAiAgent>(); utility.SelectAction();
                Assert.That(utility.CurrentAction, Is.EqualTo(index == 2 ? AiActionType.CarryAmmo : AiActionType.DefendShip));
            }
        }
        [Test] public void OwnKnownCannonUsesActualRoomRouteDistance()
        {
            Actor.GetComponent<CrewAmmoInventory>().TryPickup(arena.PlayerAmmo.Pickups[0].Definition);
            Utility.SelectAction(); var load = Utility.Scores.Single(s => s.Candidate.Action == AiActionType.LoadCannon);
            Assert.That(load.Candidate.Distance, Is.GreaterThan(Vector2.Distance(Actor.transform.position, arena.PlayerCannon.transform.position)));
        }
        [Test] public void NewAgentCanImportPriorBattleLearning()
        {
            arena.EnemyShip.ApplyDamage(100); arena.EnemyShip.TryDamageCore(30);
            Assert.That(Utility.SelectAction(), Is.EqualTo(AiActionType.Idle));
            Utility.RecordOutcome(AiActionType.CarryAmmo, true);
            var json = JsonUtility.ToJson(Utility.Learning.Export());
            var next = arena.PlayerCrew[1].GetComponent<UtilityAiAgent>(); next.Learning.Import(JsonUtility.FromJson<UtilityLearningData>(json));
            Assert.That(next.Learning.GetWeight(AiActionType.CarryAmmo), Is.EqualTo(Utility.Learning.GetWeight(AiActionType.CarryAmmo)));
        }
        [Test] public void DeadActorCancelsDecisionWithoutLosingLearning()
        {
            Invader(new Vector2(-5, -1)); arena.Controls.CycleNext();
            var ai = Actor.GetComponent<CrewAmmoAiController>(); Utility.Learning.SetWeight(AiActionType.DefendShip, 2);
            Actor.GetComponent<CrewMember>().ApplyDamage(1000); ai.Tick(0.02f);
            Assert.That(ai.IsRunning, Is.False); Assert.That(Utility.Decision, Is.Null);
            Assert.That(Utility.Learning.GetWeight(AiActionType.DefendShip), Is.EqualTo(2));
        }
        [Test] public void RealPathFailureLowersActionWeightWithoutMovingThroughWall()
        {
            Actor.GetComponent<CrewAmmoInventory>().TryPickup(arena.PlayerAmmo.Pickups[0].Definition);
            arena.Controls.CycleNext(); arena.AllyActionsEnabled = true;
            var ai = Actor.GetComponent<CrewAmmoAiController>(); ai.Tick(0.02f);
            // Relocation invalidates the next cached waypoint; the observed goal remains reachable.
            Actor.transform.position = new Vector3(-4.4f, 1.8f);
            for (var i = 0; i < 30 && Utility.Learning.GetWeight(AiActionType.LoadCannon) == 1; i++) ai.Tick(0.02f);
            Assert.That(Utility.Learning.GetWeight(AiActionType.LoadCannon), Is.LessThan(1));
            Assert.That(Actor.transform.position.y, Is.GreaterThanOrEqualTo(1.3f), "Stale path must not cross the solid core-room wall");
        }
    }
}
