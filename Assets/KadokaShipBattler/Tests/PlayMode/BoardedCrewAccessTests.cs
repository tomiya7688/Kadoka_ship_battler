using System.Linq;
using KadokaShipBattler.Characters;
using KadokaShipBattler.Core;
using NUnit.Framework;
using UnityEngine;

namespace KadokaShipBattler.Tests
{
    /* { 責務: 砲台から着地した船員だけが、船体が無傷の相手船の部屋を移動できることを実際の戦闘で確認します。 } */
    public sealed class BoardedCrewAccessTests : CrewCoordinationFixture
    {
        /* { 処理: 無傷の敵船の部屋の中心と、そこから少しずれた点を返します。 } */
        private (Vector2 from, Vector2 to) EnemyRoomStep()
        {
            var room = Arena.Navigation.Rooms.First(candidate => candidate.Side == TeamSide.Enemy);
            var bounds = room.Bounds;
            var center = new Vector2((bounds.minX + bounds.maxX) / 2, (bounds.minY + bounds.maxY) / 2);
            return (center, center + new Vector2(0.05f, 0));
        }

        [Test]
        public void OnlyBoardedPlayerCrewMovesInsideIntactEnemyShip()
        {
            Assert.IsFalse(Arena.EnemyShip.IsHullBreached);
            var definition = Arena.PlayerCrew[0].GetComponent<CrewMember>().Definition;
            var (from, to) = EnemyRoomStep();
            Assert.IsFalse(Arena.CanCrewMove(TeamSide.Player, from, to, definition, false));
            Assert.IsTrue(Arena.CanCrewMove(TeamSide.Player, from, to, definition, true));
        }

        [Test]
        public void BoardingDoesNotOpenTheShipForTheOtherSide()
        {
            Assert.IsTrue(Arena.IsEnemyShipOpenTo(TeamSide.Player, true));
            Assert.IsFalse(Arena.IsEnemyShipOpenTo(TeamSide.Enemy, true));
            Assert.IsTrue(Arena.IsPlayerShipOpenTo(TeamSide.Enemy, true));
            Assert.IsFalse(Arena.IsPlayerShipOpenTo(TeamSide.Player, true));
        }
    }
}
