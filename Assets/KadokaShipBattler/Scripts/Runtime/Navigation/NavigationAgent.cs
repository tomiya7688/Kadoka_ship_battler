using System.Collections.Generic;
using System.Linq;
using KadokaShipBattler.Characters;
using UnityEngine;

namespace KadokaShipBattler.Navigation
{
    public enum NavigationStatus { Idle, Moving, Arrived, Blocked }
    [RequireComponent(typeof(PlayerCrewController))]
    public sealed class NavigationAgent : MonoBehaviour
    {
        private PlayerCrewController actor;
        private IReadOnlyList<NavPoint> path;
        private int waypoint, revision = -1;
        private Vector2 destination;
        private TraversalAbilities abilities;
        private bool playerBreached, enemyBreached;
        public NavigationStatus Status { get; private set; }
        public IReadOnlyList<NavPoint> Path => path ?? System.Array.Empty<NavPoint>();
        private void Awake() => actor = GetComponent<PlayerCrewController>();
        public void Cancel() { path = null; revision = -1; Status = NavigationStatus.Idle; }
        public NavigationStatus MoveToRoom(string roomId, float deltaTime)
        {
            var room = actor.Arena?.Navigation.Rooms.FirstOrDefault(candidate => candidate.Id == roomId);
            if (room == null) { Cancel(); return Status = NavigationStatus.Blocked; }
            var bounds = room.Bounds;
            return MoveTo(new Vector2((bounds.minX + bounds.maxX) / 2, (bounds.minY + bounds.maxY) / 2), deltaTime);
        }
        private void Update() { if (!actor.IsAvailable || actor.Arena == null || actor.Arena.IsFinished) Cancel(); }
        public NavigationStatus MoveTo(Vector2 goal, float deltaTime)
        {
            var arena = actor.Arena;
            if (!actor.IsAvailable || arena == null || arena.IsFinished || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            { Cancel(); return Status; }
            var stats = GetComponent<CrewMember>().Definition;
            var flags = (stats.CanFly ? TraversalAbilities.Fly : 0) | (stats.CanPhase ? TraversalAbilities.Phase : 0);
            if (path == null || destination != goal || revision != arena.Navigation.Revision || abilities != flags ||
                playerBreached != arena.PlayerShip.IsHullBreached || enemyBreached != arena.EnemyShip.IsHullBreached)
            {
                destination = goal; abilities = flags; revision = arena.Navigation.Revision;
                playerBreached = arena.PlayerShip.IsHullBreached; enemyBreached = arena.EnemyShip.IsHullBreached;
                var position = (Vector2)transform.position;
                if (!arena.Navigation.TryFindPath(new NavPoint(position.x, position.y), new NavPoint(goal.x, goal.y), flags,
                    actor.TeamSide, playerBreached, enemyBreached, out path))
                { path = null; return Status = NavigationStatus.Blocked; }
                waypoint = 0;
            }
            while (waypoint < path.Count)
            {
                var next = new Vector2(path[waypoint].x, path[waypoint].y);
                var offset = next - (Vector2)transform.position;
                if (offset.magnitude <= 0.0001f) { waypoint++; continue; }
                var before = transform.position;
                actor.Move(offset.normalized, Mathf.Min(deltaTime, offset.magnitude / stats.MoveSpeed));
                if (transform.position == before) { path = null; return Status = NavigationStatus.Blocked; }
                return Status = NavigationStatus.Moving;
            }
            return Status = NavigationStatus.Arrived;
        }
        private void OnDisable() => Cancel();
    }
}
