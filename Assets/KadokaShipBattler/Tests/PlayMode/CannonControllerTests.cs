using System.Collections;
using System.Collections.Generic;
using KadokaShipBattler.Ammo;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using KadokaShipBattler.Ships;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace KadokaShipBattler.Tests
{
    public sealed class CannonControllerTests
    {
        private readonly List<Object> objects = new();
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var item in objects) if (item != null) Object.Destroy(item);
            objects.Clear();
            var arena = Object.FindFirstObjectByType<BattlePrototype>();
            if (arena != null) Object.Destroy(arena.gameObject);
            yield return null;
        }
        private ShipController Ship(TeamSide side)
        {
            var item = new GameObject(side + " Ship"); objects.Add(item);
            var ship = item.AddComponent<ShipController>(); ship.Initialize(side); return ship;
        }
        private CrewAmmoInventory Carrier(TeamSide side, CharacterCapability capabilities)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>(); objects.Add(definition);
            definition.Initialize("test", "Test", capabilities, 4, 5);
            var item = new GameObject("Crew"); objects.Add(item);
            item.AddComponent<CrewMember>().Initialize(definition, side);
            return item.AddComponent<CrewAmmoInventory>();
        }
        private AmmoDefinition Ammo(float damage)
        {
            var ammo = ScriptableObject.CreateInstance<AmmoDefinition>(); objects.Add(ammo);
            ammo.Initialize("test", "Test", damage); return ammo;
        }
        private CannonController Cannon(ShipController owner, ShipController target)
        {
            var item = new GameObject("Cannon"); objects.Add(item);
            var cannon = item.AddComponent<CannonController>(); cannon.Initialize(owner, target); return cannon;
        }
        [Test]
        public void LoadAndFireDamagesEnemyAndConsumesAmmo()
        {
            var owner = Ship(TeamSide.Player); var enemy = Ship(TeamSide.Enemy);
            var carrier = Carrier(TeamSide.Player, CharacterCapability.CarryAmmo);
            var cannon = Cannon(owner, enemy);
            Assert.That(carrier.TryPickup(Ammo(30)), Is.True);
            Assert.That(cannon.TryLoadFrom(carrier), Is.True);
            Assert.That(carrier.HasAmmo, Is.False);
            Assert.That(cannon.FireAt(enemy), Is.True);
            Assert.That(enemy.CurrentHull, Is.EqualTo(70));
            Assert.That(cannon.IsLoaded, Is.False);
        }
        [Test]
        public void FriendlyFireAndEnemyLoadingAreRejectedWithoutLosingAmmo()
        {
            var owner = Ship(TeamSide.Player); var enemy = Ship(TeamSide.Enemy);
            var carrier = Carrier(TeamSide.Enemy, CharacterCapability.CarryAmmo);
            var cannon = Cannon(owner, enemy);
            Assert.That(carrier.TryPickup(Ammo(30)), Is.True);
            Assert.That(cannon.TryLoadFrom(carrier), Is.False);
            Assert.That(carrier.HasAmmo, Is.True);
            carrier.GetComponent<CrewMember>().Initialize(carrier.GetComponent<CrewMember>().Definition, TeamSide.Player);
            Assert.That(cannon.TryLoadFrom(carrier), Is.True);
            Assert.That(cannon.FireAt(owner), Is.False);
            Assert.That(cannon.IsLoaded, Is.True);
        }
        [Test]
        public void CapabilitiesProtectPickupAndCoreAttack()
        {
            var enemy = Ship(TeamSide.Enemy);
            var carrier = Carrier(TeamSide.Player, CharacterCapability.None);
            Assert.That(carrier.TryPickup(Ammo(30)), Is.False);
            var core = enemy.gameObject.AddComponent<ShipCore>(); core.Initialize(enemy);
            enemy.ApplyDamage(100);
            Assert.That(core.TryAttack(carrier.GetComponent<CrewMember>()), Is.False);
            Assert.That(enemy.CurrentCore, Is.EqualTo(30));
        }
        [UnityTest]
        public IEnumerator ActualScenePickupFireBoardAndDestroyCore()
        {
            yield return SceneManager.LoadSceneAsync("BattlePrototype");
            var arena = Object.FindFirstObjectByType<BattlePrototype>();
            Assert.That(arena, Is.Not.Null);
            arena.EnemyActionsEnabled = false;
            var player = arena.PlayerController;
            Assert.That(arena.CanPlayerStand(Vector2.zero), Is.False);
            Assert.That(arena.EnemyCore.TryAttack(player.GetComponent<CrewMember>()), Is.False);
            for (var shot = 0; shot < 4; shot++)
            {
                var pickup = Object.FindFirstObjectByType<AmmoPickup>();
                player.transform.position = pickup.transform.position;
                Assert.That(player.TryInteract(), Is.True, "Pickup round " + shot);
                player.transform.position = arena.PlayerCannon.transform.position;
                Assert.That(player.TryInteract(), Is.True, "Load round " + shot);
                Assert.That(player.TryInteract(), Is.True, "Fire round " + shot);
                yield return null;
            }
            Assert.That(arena.EnemyShip.CurrentHull, Is.EqualTo(0));
            Assert.That(arena.IsFinished, Is.False, "Hull zero must not finish the battle");
            Assert.That(arena.EnemyShip.CurrentCore, Is.EqualTo(30));
            player.transform.position = new Vector3(-2, 0, 0);
            for (var step = 0; step < 75; step++) player.Move(Vector2.right, 0.025f);
            Assert.That(player.transform.position.x, Is.GreaterThan(5));
            for (var step = 0; step < 15; step++) player.Move(Vector2.up, 0.025f);
            Assert.That(player.TryAttack(), Is.True);
            Assert.That(arena.IsFinished, Is.False);
            Assert.That(player.TryAttack(), Is.True);
            Assert.That(arena.PlayerWon, Is.True);
            Assert.That(player.TryAttack(), Is.False, "Actions stop at victory");
        }
        [UnityTest]
        public IEnumerator ActualSceneRendersBothDecks()
        {
            yield return SceneManager.LoadSceneAsync("BattlePrototype");
            var arena = Object.FindFirstObjectByType<BattlePrototype>();
            arena.EnemyActionsEnabled = false;
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previousTarget = arena.GameCamera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                arena.GameCamera.targetTexture = target;
                arena.GameCamera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                var left = image.GetPixel(340, 360);
                var right = image.GetPixel(940, 360);
                Assert.That(left.b, Is.GreaterThan(left.r), "Player deck must render blue");
                Assert.That(right.r, Is.GreaterThan(right.b), "Enemy deck must render red");
                System.IO.Directory.CreateDirectory("Logs");
                System.IO.File.WriteAllBytes("Logs/BattlePrototype.png", image.EncodeToPNG());
            }
            finally
            {
                arena.GameCamera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(image);
            }
        }

        [UnityTest]
        public IEnumerator EnemyCanBreachBoardAndDestroyPlayerCore()
        {
            yield return SceneManager.LoadSceneAsync("BattlePrototype");
            var arena = Object.FindFirstObjectByType<BattlePrototype>();
            arena.PlayerShip.ApplyDamage(100);
            Assert.That(arena.IsFinished, Is.False);
            // Accelerate the actual prototype enemy Update without waiting a full minute.
            var previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 20;
                var deadline = Time.realtimeSinceStartup + 8;
                while (!arena.IsFinished && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(arena.PlayerShip.IsDestroyed, Is.True);
                Assert.That(arena.PlayerWon, Is.False);
            }
            finally { Time.timeScale = previousScale; }
        }
    }
}
