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
    public sealed class CrewControlTests
    {
        private BattlePrototype arena;
        private sealed class IdleInput : ICrewInputSource
        {
            public CrewInput Read() => new CrewInput(Vector2.zero);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("BattlePrototype");
            arena = Object.FindFirstObjectByType<BattlePrototype>();
            arena.Controls.InputSource = new IdleInput();
            arena.EnemyActionsEnabled = false;
            arena.AllyActionsEnabled = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (arena != null) Object.Destroy(arena.gameObject);
            yield return null;
        }

        [Test]
        public void SwitchPreservesPositionFacingAndAmmoAndResumesAiImmediately()
        {
            var previous = arena.PlayerCrew[0];
            var next = arena.PlayerCrew[1];
            previous.transform.position = new Vector3(-5, -0.5f, 0);
            previous.Face(Vector2.down);
            var inventory = previous.GetComponent<CrewAmmoInventory>();
            var ammo = Object.FindFirstObjectByType<AmmoPickup>().Definition;
            Assert.That(inventory.TryPickup(ammo), Is.True);
            var position = previous.transform.position;
            var facing = previous.Facing;
            Assert.That(arena.Controls.TrySwitch(next), Is.True);
            Assert.That(previous.IsDirectlyControlled, Is.False);
            Assert.That(next.IsDirectlyControlled, Is.True);
            Assert.That(previous.transform.position, Is.EqualTo(position));
            Assert.That(previous.Facing, Is.EqualTo(facing));
            Assert.That(inventory.CarriedAmmo, Is.SameAs(ammo));
            var ai = previous.GetComponent<CrewAmmoAiController>();
            Assert.That(ai.IsRunning, Is.True);
            Assert.That(ai.ObservationOrigin, Is.EqualTo((Vector2)position));
            Assert.That(ai.ObservationFacing, Is.EqualTo(facing));
            Assert.That(ai.CurrentAction, Is.EqualTo(AiActionType.LoadCannon));
            Assert.That(next.GetComponent<CrewAmmoAiController>().IsRunning, Is.False);
            previous.ApplyInput(new CrewInput(Vector2.left), 0.1f);
            Assert.That(previous.transform.position, Is.EqualTo(position), "Released crew must ignore direct input");
            arena.AllyActionsEnabled = true;
            var distance = Vector3.Distance(position, arena.PlayerCannon.transform.position);
            ai.Tick(0.1f);
            Assert.That(Vector3.Distance(previous.transform.position, arena.PlayerCannon.transform.position), Is.LessThan(distance));
        }

        [Test]
        public void SameInputUsesSelectedCharactersMovementSpeed()
        {
            var first = arena.PlayerCrew[0];
            var second = arena.PlayerCrew[1];
            var firstStart = first.transform.position;
            var secondStart = second.transform.position;
            arena.Controls.ApplyInput(new CrewInput(Vector2.right), 0.1f);
            arena.Controls.ApplyInput(new CrewInput(Vector2.right, switchNext: true), 0.1f);
            Assert.That(first.transform.position.x - firstStart.x, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(second.transform.position.x - secondStart.x, Is.EqualTo(0.28f).Within(0.001f));
            Assert.That(arena.PlayerController, Is.SameAs(second));
            for (var remaining = 0; remaining < arena.PlayerCrew.Count - 1; remaining++)
                Assert.That(arena.Controls.CycleNext(), Is.True);
            Assert.That(arena.PlayerController, Is.SameAs(first));
        }

        [Test]
        public void SwitchAndInteractInOneInputTargetsNewCrewOnly()
        {
            var first = arena.PlayerCrew[0];
            var second = arena.PlayerCrew[1];
            var pickup = Object.FindFirstObjectByType<AmmoPickup>();
            second.transform.position = pickup.transform.position;
            arena.Controls.ApplyInput(new CrewInput(Vector2.zero, interact: true, switchNext: true), 0.1f);
            Assert.That(second.GetComponent<CrewAmmoInventory>().HasAmmo, Is.True);
            Assert.That(first.GetComponent<CrewAmmoInventory>().HasAmmo, Is.False);
        }

        [Test]
        public void AiReobservesLiveFacingInsteadOfRememberingOldTargets()
        {
            var previous = arena.PlayerCrew[0];
            previous.transform.position = new Vector3(-5, -0.2f, 0);
            previous.Face(Vector2.up);
            arena.Controls.CycleNext();
            var ai = previous.GetComponent<CrewAmmoAiController>();
            Assert.That(ai.ObservationFacing, Is.EqualTo(Vector2.up));
            Assert.That(ai.VisibleAmmo, Is.Null, "Ammo behind the current facing must not be selected");
            Assert.That(ai.CurrentAction, Is.EqualTo(AiActionType.Idle));
            previous.Face(Vector2.down);
            ai.ResumeFromCurrentState();
            Assert.That(ai.VisibleAmmo, Is.Not.Null);
            Assert.That(ai.CurrentAction, Is.EqualTo(AiActionType.CarryAmmo));
        }

        [UnityTest]
        public IEnumerator ReleasedCrewActuallyPicksUpAmmoOnFollowingFrames()
        {
            var previous = arena.PlayerCrew[0];
            var pickup = Object.FindFirstObjectByType<AmmoPickup>();
            previous.transform.position = pickup.transform.position;
            previous.Face(Vector2.down);
            arena.Controls.CycleNext();
            arena.AllyActionsEnabled = true;
            var inventory = previous.GetComponent<CrewAmmoInventory>();
            for (var frame = 0; frame < 10 && !inventory.HasAmmo; frame++) yield return null;
            Assert.That(inventory.HasAmmo, Is.True, "AI Update must execute after control is released");
            Assert.That(previous.IsDirectlyControlled, Is.False);
            Assert.That(arena.PlayerController.GetComponent<CrewAmmoInventory>().HasAmmo, Is.False);
        }

        [Test]
        public void ReleasedAiCarriesLoadsAndFiresThenStopsWhenSelectedAgain()
        {
            var previous = arena.PlayerCrew[0];
            previous.transform.position = Object.FindFirstObjectByType<AmmoPickup>().transform.position;
            arena.Controls.CycleNext();
            arena.AllyActionsEnabled = true;
            var ai = previous.GetComponent<CrewAmmoAiController>();
            for (var step = 0; step < 200 && arena.EnemyShip.CurrentHull == 100; step++) ai.Tick(0.02f);
            Assert.That(arena.EnemyShip.CurrentHull, Is.EqualTo(75), "Resumed AI must execute pickup, movement, loading and firing");
            Assert.That(previous.GetComponent<CrewAmmoInventory>().HasAmmo, Is.False);
            Assert.That(arena.PlayerCannon.IsLoaded, Is.False);
            arena.Controls.TrySwitch(previous);
            var position = previous.transform.position;
            ai.Tick(1f);
            Assert.That(ai.IsRunning, Is.False);
            Assert.That(previous.transform.position, Is.EqualTo(position));
            Assert.That(arena.EnemyShip.CurrentHull, Is.EqualTo(75));
        }

        [Test]
        public void ReleasedBoardingCrewReturnsViaBridgeWithItsAmmo()
        {
            arena.EnemyShip.ApplyDamage(100);
            var previous = arena.PlayerCrew[0];
            var inventory = previous.GetComponent<CrewAmmoInventory>();
            inventory.TryPickup(Object.FindFirstObjectByType<AmmoPickup>().Definition);
            previous.transform.position = new Vector3(6, 1.5f, 0);
            arena.Controls.CycleNext();
            Assert.That(previous.transform.position, Is.EqualTo(new Vector3(6, 1.5f, 0)));
            arena.AllyActionsEnabled = true;
            var ai = previous.GetComponent<CrewAmmoAiController>();
            var usedBridge = false;
            for (var step = 0; step < 600 && inventory.HasAmmo; step++)
            {
                ai.Tick(0.02f);
                var position = (Vector2)previous.transform.position;
                Assert.That(arena.CanPlayerStand(position), Is.True, "AI must remain on decks or bridge");
                if (Mathf.Abs(position.x) < 1) usedBridge = true;
            }
            Assert.That(usedBridge, Is.True);
            Assert.That(inventory.HasAmmo, Is.False);
            Assert.That(arena.PlayerCannon.IsLoaded, Is.True);
        }

        [Test]
        public void SameAttackInputUsesSelectedCharactersCombatSkill()
        {
            arena.EnemyShip.Initialize(TeamSide.Enemy, 100, 100);
            arena.EnemyShip.ApplyDamage(100);
            arena.PlayerCrew[0].transform.position = arena.EnemyCore.transform.position;
            arena.PlayerCrew[1].transform.position = arena.EnemyCore.transform.position;
            arena.Controls.ApplyInput(new CrewInput(Vector2.zero, attack: true), 0.1f);
            Assert.That(arena.EnemyShip.CurrentCore, Is.EqualTo(75));
            arena.Controls.ApplyInput(new CrewInput(Vector2.zero, attack: true, switchNext: true), 0.1f);
            Assert.That(arena.EnemyShip.CurrentCore, Is.EqualTo(35));
        }

        [Test]
        public void DisabledCrewIsSkippedAndBattleEndBlocksSwitchInput()
        {
            var first = arena.PlayerCrew[0];
            var second = arena.PlayerCrew[1];
            first.gameObject.SetActive(false);
            arena.Controls.ApplyInput(new CrewInput(Vector2.zero), 0.1f);
            Assert.That(arena.PlayerController, Is.SameAs(second));
            Assert.That(first.IsDirectlyControlled, Is.False);
            Assert.That(first.GetComponent<CrewAmmoAiController>().IsRunning, Is.False);
            Assert.That(arena.Controls.TrySwitch(first), Is.False);
            first.gameObject.SetActive(true);
            arena.EnemyShip.ApplyDamage(100);
            arena.EnemyShip.TryDamageCore(30);
            arena.Controls.ApplyInput(new CrewInput(Vector2.left, switchNext: true), 0.1f);
            Assert.That(arena.PlayerController, Is.SameAs(second));
        }
    }
}
