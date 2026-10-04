using System.Collections.Generic;
using System.Linq;
using KadokaShipBattler.AI;
using UnityEngine;

namespace KadokaShipBattler.Navigation
{
    public sealed class NavigationMapView : MonoBehaviour
    {
        private BattleNavigation map;
        private Sprite sprite;
        private readonly List<GameObject> walls = new();
        public void Initialize(BattleNavigation navigation, Sprite square)
        {
            if (map != null) map.Changed -= Rebuild;
            map = navigation; sprite = square; map.Changed += Rebuild; Rebuild();
        }
        private void OnDestroy() { if (map != null) map.Changed -= Rebuild; }
        private void Rebuild()
        {
            foreach (var wall in walls) { wall.SetActive(false); Destroy(wall); }
            walls.Clear();
            for (var i = 0; i < map.Rooms.Count; i++)
            for (var j = i + 1; j < map.Rooms.Count; j++)
            {
                var a = map.Rooms[i]; var b = map.Rooms[j];
                if (a.Side != b.Side || !RoomData.SharedEdge(a.Bounds, b.Bounds, out var start, out var end)) continue;
                var vertical = start.x == end.x;
                float Coordinate(NavPoint p) => vertical ? p.y : p.x;
                var cursor = Coordinate(start); var finish = Coordinate(end);
                var doors = map.Doors.Where(d => d.IsOpen && (d.From == a.Id && d.To == b.Id || d.To == a.Id && d.From == b.Id)).OrderBy(d => Coordinate(d.Point));
                foreach (var door in doors)
                {
                    AddWall(cursor, Coordinate(door.Point) - door.Width / 2, start, vertical);
                    cursor = Mathf.Max(cursor, Coordinate(door.Point) + door.Width / 2);
                }
                AddWall(cursor, finish, start, vertical);
            }
        }
        private void AddWall(float start, float end, NavPoint edge, bool vertical)
        {
            if (end - start <= 0.0001f) return;
            var wall = new GameObject("Room Wall"); wall.transform.SetParent(transform);
            wall.transform.position = vertical ? new Vector3(edge.x, (start + end) / 2) : new Vector3((start + end) / 2, edge.y);
            wall.transform.localScale = vertical ? new Vector3(0.04f, end - start, 1) : new Vector3(end - start, 0.04f, 1);
            var renderer = wall.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = new Color(0.7f, 0.6f, 0.35f); renderer.sortingOrder = 1;
            wall.AddComponent<BoxCollider2D>().isTrigger = true; wall.AddComponent<VisionBlocker>(); walls.Add(wall);
        }
    }
}
