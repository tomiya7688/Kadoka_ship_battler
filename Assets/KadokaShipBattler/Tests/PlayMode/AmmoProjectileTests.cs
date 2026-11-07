using System.Collections.Generic;
using KadokaShipBattler.Ammo;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using KadokaShipBattler.Ships;
using NUnit.Framework;
using UnityEngine;

namespace KadokaShipBattler.Tests
{
    /* { 責務: 通常砲弾が即時着弾ではなく飛翔体として飛び、着弾でだけダメージを与え、敵対する弾と衝突することを確認します。 } */
    public sealed class AmmoProjectileTests
    {
        private readonly List<Object> objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in objects) if (item != null) Object.DestroyImmediate(item);
            objects.Clear();
            ProjectileClashField.Clear();
        }

        private ShipController Ship(TeamSide side, float x)
        {
            var item = new GameObject(side + " Ship"); objects.Add(item);
            item.transform.position = new Vector3(x, 0, 0);
            var ship = item.AddComponent<ShipController>(); ship.Initialize(side); return ship;
        }

        private CannonController Loaded(ShipController owner, ShipController target, float damage, float hardness,
            ProjectileTrajectoryKind kind = ProjectileTrajectoryKind.Direct)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>(); objects.Add(definition);
            definition.Initialize("test", "Test", CharacterCapability.CarryAmmo, 4, 5);
            var crew = new GameObject("Crew"); objects.Add(crew);
            crew.AddComponent<CrewMember>().Initialize(definition, owner.TeamSide);
            var inventory = crew.AddComponent<CrewAmmoInventory>();
            var ammo = ScriptableObject.CreateInstance<AmmoDefinition>(); objects.Add(ammo);
            ammo.Initialize("shell", "Shell", damage, 1f, hardness);
            Assert.That(inventory.TryPickup(ammo), Is.True);
            var item = new GameObject("Cannon"); objects.Add(item);
            item.transform.position = owner.transform.position;
            var cannon = item.AddComponent<CannonController>(); cannon.Initialize(owner, target);
            cannon.TrajectoryKind = kind;
            Assert.That(cannon.TryLoadFrom(inventory), Is.True);
            return cannon;
        }

        [Test]
        public void ShellDealsDamageOnlyWhenItLands()
        {
            var player = Ship(TeamSide.Player, 0); var enemy = Ship(TeamSide.Enemy, 8);
            var cannon = Loaded(player, enemy, 30, 1);
            Assert.That(cannon.FireAt(enemy), Is.True);
            Assert.That(cannon.IsLoaded, Is.False);
            Assert.That(AmmoProjectile.LiveCount, Is.EqualTo(1));
            Assert.That(enemy.CurrentHull, Is.EqualTo(100), "Launch alone must not damage the hull");
            AmmoProjectile.AdvanceAll(0.3f);
            Assert.That(AmmoProjectile.LiveCount, Is.EqualTo(1));
            Assert.That(enemy.CurrentHull, Is.EqualTo(100), "A shell still in flight deals no damage");
            ShellFlight.Land();
            Assert.That(enemy.CurrentHull, Is.EqualTo(70));
            Assert.That(AmmoProjectile.LiveCount, Is.Zero);
        }

        [Test]
        public void ArcShellStillLandsAndDamages()
        {
            var player = Ship(TeamSide.Player, 0); var enemy = Ship(TeamSide.Enemy, 8);
            var cannon = Loaded(player, enemy, 20, 1, ProjectileTrajectoryKind.Arc);
            Assert.That(cannon.FireAt(enemy), Is.True);
            AmmoProjectile.AdvanceAll(0.5f);
            Assert.That(Object.FindFirstObjectByType<AmmoProjectile>().transform.position.y, Is.GreaterThan(0.5f), "Arc rises above the straight line");
            ShellFlight.Land();
            Assert.That(enemy.CurrentHull, Is.EqualTo(80));
        }

        [Test]
        public void OpposingShellsClashAndNeitherLands()
        {
            var player = Ship(TeamSide.Player, 0); var enemy = Ship(TeamSide.Enemy, 8);
            var left = Loaded(player, enemy, 30, 1); var right = Loaded(enemy, player, 30, 1);
            Assert.That(left.FireAt(enemy), Is.True);
            Assert.That(right.FireAt(player), Is.True);
            ShellFlight.Land();
            Assert.That(enemy.CurrentHull, Is.EqualTo(100));
            Assert.That(player.CurrentHull, Is.EqualTo(100));
            Assert.That(AmmoProjectile.LiveCount, Is.Zero);
        }

        [Test]
        public void HarderShellSurvivesAClashAndLands()
        {
            var player = Ship(TeamSide.Player, 0); var enemy = Ship(TeamSide.Enemy, 8);
            var heavy = Loaded(player, enemy, 40, 3); var light = Loaded(enemy, player, 10, 1);
            Assert.That(heavy.FireAt(enemy), Is.True);
            Assert.That(light.FireAt(player), Is.True);
            ShellFlight.Land();
            Assert.That(enemy.CurrentHull, Is.EqualTo(60), "The harder shell keeps flying and lands");
            Assert.That(player.CurrentHull, Is.EqualTo(100), "The softer shell is destroyed before landing");
        }

        [Test]
        public void ShellIsCancelledWithoutDamageWhenDestroyedInFlight()
        {
            var player = Ship(TeamSide.Player, 0); var enemy = Ship(TeamSide.Enemy, 8);
            var cannon = Loaded(player, enemy, 30, 1);
            Assert.That(cannon.FireAt(enemy), Is.True);
            var shell = Object.FindFirstObjectByType<AmmoProjectile>();
            Object.DestroyImmediate(shell.gameObject);
            Assert.That(AmmoProjectile.LiveCount, Is.Zero);
            Assert.That(ProjectileClashField.Count, Is.Zero);
            Assert.That(enemy.CurrentHull, Is.EqualTo(100));
        }
    }
}
