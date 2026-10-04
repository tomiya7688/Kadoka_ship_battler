using System;
using System.Collections.Generic;
using System.Linq;
using KadokaShipBattler.Core;

namespace KadokaShipBattler.Navigation
{
    [Flags] public enum TraversalAbilities { None = 0, Fly = 1, Phase = 2 }
    [Serializable] public struct NavPoint
    {
        public float x, y;
        public NavPoint(float x, float y) { this.x = x; this.y = y; }
        public bool IsFinite => Finite(x) && Finite(y);
        public static float Distance(NavPoint a, NavPoint b) => (float)Math.Sqrt(((double)a.x - b.x) * ((double)a.x - b.x) + ((double)a.y - b.y) * ((double)a.y - b.y));
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
    [Serializable] public sealed class RoomData
    {
        public string id;
        public float minX, minY, maxX, maxY;
        public bool Contains(NavPoint point) => point.x >= minX - 0.0001f && point.x <= maxX + 0.0001f && point.y >= minY - 0.0001f && point.y <= maxY + 0.0001f;
        public static bool SharedEdge(RoomData a, RoomData b, out NavPoint start, out NavPoint end)
        {
            start = end = default;
            if (Math.Abs(a.maxX - b.minX) < 0.0001f || Math.Abs(b.maxX - a.minX) < 0.0001f)
            {
                var x = Math.Abs(a.maxX - b.minX) < 0.0001f ? a.maxX : a.minX;
                start = new NavPoint(x, Math.Max(a.minY, b.minY)); end = new NavPoint(x, Math.Min(a.maxY, b.maxY));
                return end.y - start.y > 0.0001f;
            }
            if (Math.Abs(a.maxY - b.minY) < 0.0001f || Math.Abs(b.maxY - a.minY) < 0.0001f)
            {
                var y = Math.Abs(a.maxY - b.minY) < 0.0001f ? a.maxY : a.minY;
                start = new NavPoint(Math.Max(a.minX, b.minX), y); end = new NavPoint(Math.Min(a.maxX, b.maxX), y);
                return end.x - start.x > 0.0001f;
            }
            return false;
        }
    }
    [Serializable] public sealed class DoorData
    {
        public string id, from, to;
        public NavPoint point;
        public float width;
        public TraversalAbilities requiredAbilities;
        public bool isOpen, allowPhaseWhenClosed;
    }
    [Serializable] public sealed class ShipMapData
    {
        public string id;
        public RoomData[] rooms;
        public DoorData[] doors;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) || rooms == null || rooms.Length == 0 || doors == null) throw new ArgumentException("Ship map needs an id, rooms and doors.");
            var lookup = new Dictionary<string, RoomData>(StringComparer.Ordinal);
            foreach (var room in rooms)
            {
                if (room == null || string.IsNullOrWhiteSpace(room.id) || lookup.ContainsKey(room.id) ||
                    !NavPoint.Finite(room.minX) || !NavPoint.Finite(room.minY) || !NavPoint.Finite(room.maxX) || !NavPoint.Finite(room.maxY) ||
                    room.minX >= room.maxX || room.minY >= room.maxY) throw new ArgumentException("Invalid room.");
                foreach (var other in lookup.Values)
                    if (Math.Min(other.maxX, room.maxX) - Math.Max(other.minX, room.minX) > 0.0001f &&
                        Math.Min(other.maxY, room.maxY) - Math.Max(other.minY, room.minY) > 0.0001f) throw new ArgumentException("Room interiors cannot overlap.");
                lookup.Add(room.id, room);
            }
            var ids = new HashSet<string>();
            foreach (var door in doors)
            {
                if (door == null || string.IsNullOrWhiteSpace(door.id) || !ids.Add(door.id) || door.from == null || door.to == null ||
                    !lookup.TryGetValue(door.from, out var from) || !lookup.TryGetValue(door.to, out var to) || from == to ||
                    !door.point.IsFinite || !NavPoint.Finite(door.width) || door.width <= 0 || ((int)door.requiredAbilities & ~3) != 0 ||
                    !RoomData.SharedEdge(from, to, out var start, out var end)) throw new ArgumentException("Invalid room connection.");
                var length = NavPoint.Distance(start, end);
                if (Math.Abs(NavPoint.Distance(start, door.point) + NavPoint.Distance(door.point, end) - length) > 0.0001f ||
                    NavPoint.Distance(start, door.point) + 0.0001f < door.width / 2 || NavPoint.Distance(end, door.point) + 0.0001f < door.width / 2)
                    throw new ArgumentException("Door must fit on a shared room edge.");
            }
        }
    }
    [Serializable] public sealed class BattleMapData
    {
        public ShipMapData[] types;
        public void Validate()
        {
            if (types == null || types.Length == 0) throw new ArgumentException("Ship map types are required.");
            var ids = new HashSet<string>();
            foreach (var type in types)
            {
                if (type == null) throw new ArgumentException("Null ship map.");
                type.Validate();
                if (!ids.Add(type.id)) throw new ArgumentException("Duplicate ship map type.");
            }
        }
        public ShipMapData GetType(string id)
        {
            Validate();
            return Array.Find(types, type => type.id == id) ?? throw new ArgumentException("Unknown ship map type: " + id);
        }
    }
    public sealed class NavigationRoom
    {
        internal RoomData Geometry { get; }
        public string Id => Geometry.id;
        public RoomData Bounds => Copy(Geometry);
        public TeamSide? Side { get; }
        public NavigationRoom(RoomData bounds, TeamSide? side) { Geometry = Copy(bounds); Side = side; }
        private static RoomData Copy(RoomData data) => new RoomData { id = data.id, minX = data.minX, minY = data.minY, maxX = data.maxX, maxY = data.maxY };
    }
    public sealed class NavigationDoor
    {
        public string Id { get; }
        public string From { get; }
        public string To { get; }
        public NavPoint Point { get; }
        public float Width { get; }
        public TraversalAbilities RequiredAbilities { get; }
        public bool AllowPhaseWhenClosed { get; }
        public bool IsOpen { get; internal set; }
        public NavigationDoor(DoorData door)
        { Id = door.id; From = door.from; To = door.to; Point = door.point; Width = door.width; RequiredAbilities = door.requiredAbilities; AllowPhaseWhenClosed = door.allowPhaseWhenClosed; IsOpen = door.isOpen; }
        public bool CanUse(TraversalAbilities abilities) => (abilities & RequiredAbilities) == RequiredAbilities &&
            (IsOpen || AllowPhaseWhenClosed && (abilities & TraversalAbilities.Phase) != 0);
    }
    public sealed class BattleNavigation
    {
        public IReadOnlyList<NavigationRoom> Rooms { get; }
        public IReadOnlyList<NavigationDoor> Doors { get; }
        public int Revision { get; private set; }
        public event Action Changed;
        public BattleNavigation(BattleMapData data, string playerType, string enemyType)
        {
            var rooms = new List<NavigationRoom>(); var doors = new List<NavigationDoor>();
            AddShip(data.GetType(playerType), TeamSide.Player, rooms, doors);
            AddShip(data.GetType(enemyType), TeamSide.Enemy, rooms, doors);
            rooms.Add(new NavigationRoom(new RoomData { id = "bridge", minX = -1, maxX = 1, minY = -0.5f, maxY = 0.5f }, null));
            foreach (var side in new[] { TeamSide.Player, TeamSide.Enemy })
            {
                var point = new NavPoint(side == TeamSide.Player ? -1 : 1, 0);
                var room = rooms.SingleOrDefault(candidate => candidate.Side == side && candidate.Geometry.Contains(point)) ?? throw new ArgumentException("Ship map needs a common entrance at local (3,0).");
                doors.Add(new NavigationDoor(new DoorData { id = side + "/entrance", from = room.Id, to = "bridge", point = point, width = 1, isOpen = true }));
            }
            Rooms = rooms.AsReadOnly(); Doors = doors.AsReadOnly();
        }
        private static void AddShip(ShipMapData type, TeamSide side, List<NavigationRoom> rooms, List<NavigationDoor> doors)
        {
            NavPoint World(NavPoint point) => new NavPoint(side == TeamSide.Player ? -4 + point.x : 4 - point.x, point.y);
            foreach (var room in type.rooms)
            {
                var first = World(new NavPoint(room.minX, room.minY)); var last = World(new NavPoint(room.maxX, room.maxY));
                rooms.Add(new NavigationRoom(new RoomData { id = side + "/" + room.id, minX = Math.Min(first.x, last.x), maxX = Math.Max(first.x, last.x), minY = first.y, maxY = last.y }, side));
            }
            foreach (var door in type.doors) doors.Add(new NavigationDoor(new DoorData { id = side + "/" + door.id, from = side + "/" + door.from, to = side + "/" + door.to,
                point = World(door.point), width = door.width, requiredAbilities = door.requiredAbilities, isOpen = door.isOpen, allowPhaseWhenClosed = door.allowPhaseWhenClosed }));
        }
        public bool SetDoorOpen(string id, bool open)
        {
            var door = Doors.FirstOrDefault(candidate => candidate.Id == id);
            if (door == null || door.IsOpen == open) return false;
            door.IsOpen = open; Revision++; Changed?.Invoke(); return true;
        }
        private static bool Accessible(NavigationRoom room, TeamSide side, bool playerBreached, bool enemyBreached) =>
            room.Side == side || (side == TeamSide.Player ? enemyBreached : playerBreached);
        public bool CanStand(NavPoint point, TraversalAbilities abilities, TeamSide side, bool playerBreached, bool enemyBreached)
        {
            if (!point.IsFinite || side != TeamSide.Player && side != TeamSide.Enemy) return false;
            var found = Rooms.Where(room => room.Geometry.Contains(point) && Accessible(room, side, playerBreached, enemyBreached)).ToArray();
            if (found.Length == 0) return false;
            if (found.Length == 1) return true;
            return Doors.Any(door => found.Any(room => room.Id == door.From) && found.Any(room => room.Id == door.To) && door.CanUse(abilities) && NavPoint.Distance(point, door.Point) <= door.Width / 2 + 0.0001f);
        }
        public bool CanTraverse(NavPoint from, NavPoint to, TraversalAbilities abilities, TeamSide side, bool playerBreached, bool enemyBreached)
        {
            if (!CanStand(from, abilities, side, playerBreached, enemyBreached) || !CanStand(to, abilities, side, playerBreached, enemyBreached)) return false;
            var cuts = new List<float> { 0, 1 };
            var dx = to.x - from.x; var dy = to.y - from.y;
            foreach (var room in Rooms)
            {
                if (Math.Abs(dx) > 1e-7f) { AddCut((room.Geometry.minX - from.x) / dx); AddCut((room.Geometry.maxX - from.x) / dx); }
                if (Math.Abs(dy) > 1e-7f) { AddCut((room.Geometry.minY - from.y) / dy); AddCut((room.Geometry.maxY - from.y) / dy); }
            }
            void AddCut(float value) { if (value > 0 && value < 1) cuts.Add(value); }
            cuts.Sort();
            NavigationRoom previous = null;
            for (var index = 0; index < cuts.Count - 1; index++)
            {
                if (cuts[index + 1] - cuts[index] < 1e-6f) continue;
                var mid = (cuts[index + 1] + cuts[index]) / 2;
                var point = new NavPoint(from.x + dx * mid, from.y + dy * mid);
                var room = Rooms.FirstOrDefault(candidate => candidate.Geometry.Contains(point) && Accessible(candidate, side, playerBreached, enemyBreached));
                if (room == null || !CanStand(point, abilities, side, playerBreached, enemyBreached)) return false;
                if (previous != null && previous != room)
                {
                    var crossing = new NavPoint(from.x + dx * cuts[index], from.y + dy * cuts[index]);
                    if (!Doors.Any(door => (door.From == previous.Id && door.To == room.Id || door.To == previous.Id && door.From == room.Id) &&
                        door.CanUse(abilities) && NavPoint.Distance(crossing, door.Point) <= door.Width / 2 + 0.0001f)) return false;
                }
                previous = room;
            }
            return true;
        }
        private sealed class PathNode { public string Room; public int Door; public NavPoint Point; }
        private static NavPoint InsidePortal(NavigationRoom room, NavPoint point)
        {
            var bounds = room.Geometry;
            var inset = Math.Min(0.03f, Math.Min(bounds.maxX - bounds.minX, bounds.maxY - bounds.minY) / 4);
            var x = point.x; var y = point.y;
            if (Math.Abs(x - bounds.minX) < 0.0001f) x = bounds.minX + inset;
            else if (Math.Abs(x - bounds.maxX) < 0.0001f) x = bounds.maxX - inset;
            if (Math.Abs(y - bounds.minY) < 0.0001f) y = bounds.minY + inset;
            else if (Math.Abs(y - bounds.maxY) < 0.0001f) y = bounds.maxY - inset;
            return new NavPoint(x, y);
        }
        public bool TryFindPath(NavPoint start, NavPoint goal, TraversalAbilities abilities, TeamSide side, bool playerBreached, bool enemyBreached, out IReadOnlyList<NavPoint> path)
        {
            path = Array.Empty<NavPoint>();
            if (!CanStand(start, abilities, side, playerBreached, enemyBreached) || !CanStand(goal, abilities, side, playerBreached, enemyBreached)) return false;
            var allowed = Rooms.Where(room => Accessible(room, side, playerBreached, enemyBreached)).ToDictionary(room => room.Id);
            var starts = allowed.Values.Where(room => room.Geometry.Contains(start)).Select(room => room.Id).ToHashSet();
            var goals = allowed.Values.Where(room => room.Geometry.Contains(goal)).Select(room => room.Id).ToHashSet();
            if (starts.Overlaps(goals) && CanTraverse(start, goal, abilities, side, playerBreached, enemyBreached)) { path = Array.AsReadOnly(new[] { goal }); return true; }
            var nodes = new List<PathNode>();
            for (var index = 0; index < Doors.Count; index++)
            {
                var door = Doors[index];
                if (!door.CanUse(abilities) || !allowed.ContainsKey(door.From) || !allowed.ContainsKey(door.To)) continue;
                // Separate points inside each room keep same-edge routes away from solid wall spans.
                nodes.Add(new PathNode { Room = door.From, Door = index, Point = InsidePortal(allowed[door.From], door.Point) });
                nodes.Add(new PathNode { Room = door.To, Door = index, Point = InsidePortal(allowed[door.To], door.Point) });
            }
            var distances = new float[nodes.Count]; var parents = new int[nodes.Count]; var visited = new bool[nodes.Count];
            for (var index = 0; index < nodes.Count; index++)
            {
                distances[index] = starts.Contains(nodes[index].Room) && CanTraverse(start, nodes[index].Point, abilities, side, playerBreached, enemyBreached)
                    ? NavPoint.Distance(start, nodes[index].Point) : float.PositiveInfinity;
                parents[index] = -1;
            }
            var best = float.PositiveInfinity; var lastNode = -1;
            for (var count = 0; count < nodes.Count; count++)
            {
                var current = -1;
                for (var index = 0; index < nodes.Count; index++) if (!visited[index] && (current < 0 || distances[index] < distances[current])) current = index;
                if (current < 0 || float.IsPositiveInfinity(distances[current])) break;
                visited[current] = true;
                if (goals.Contains(nodes[current].Room) && CanTraverse(nodes[current].Point, goal, abilities, side, playerBreached, enemyBreached))
                {
                    var cost = distances[current] + NavPoint.Distance(nodes[current].Point, goal);
                    if (cost < best) { best = cost; lastNode = current; }
                }
                for (var next = 0; next < nodes.Count; next++)
                {
                    if (visited[next] || nodes[current].Room != nodes[next].Room && nodes[current].Door != nodes[next].Door) continue;
                    if (!CanTraverse(nodes[current].Point, nodes[next].Point, abilities, side, playerBreached, enemyBreached)) continue;
                    var cost = distances[current] + NavPoint.Distance(nodes[current].Point, nodes[next].Point);
                    if (cost >= distances[next]) continue;
                    distances[next] = cost; parents[next] = current;
                }
            }
            if (lastNode < 0) return false;
            var result = new List<NavPoint> { goal };
            for (var index = lastNode; index >= 0; index = parents[index])
                if (NavPoint.Distance(result[result.Count - 1], nodes[index].Point) > 0.0001f) result.Add(nodes[index].Point);
            result.Reverse(); path = result.AsReadOnly(); return true;
        }
    }
}
