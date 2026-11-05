using System;
using System.Collections.Generic;
using KadokaShipBattler.Core;

namespace KadokaShipBattler.AI
{
    public sealed class VisionCone
    {
        public float ViewAngle { get; }
        public float ViewDistance { get; }
        private readonly double cosine;
        public VisionCone(float viewAngle, float viewDistance)
        {
            if (!Finite(viewAngle) || viewAngle <= 0 || viewAngle > 360 || !Finite(viewDistance) || viewDistance <= 0)
                throw new ArgumentOutOfRangeException(nameof(viewAngle), "Vision angle must be in (0,360] and distance positive and finite.");
            ViewAngle = viewAngle;
            ViewDistance = viewDistance;
            cosine = Math.Cos(viewAngle * Math.PI / 360);
        }
        public bool Contains(float originX, float originY, float facingX, float facingY, float targetX, float targetY)
        {
            if (!Finite(originX) || !Finite(originY) || !Finite(facingX) || !Finite(facingY) || !Finite(targetX) || !Finite(targetY)) return false;
            var directionLength = (double)facingX * facingX + (double)facingY * facingY;
            if (directionLength <= 1e-12) return false;
            var x = (double)targetX - originX;
            var y = (double)targetY - originY;
            var distanceSquared = x * x + y * y;
            if (distanceSquared > (double)ViewDistance * ViewDistance) return false;
            if (distanceSquared <= 1e-10 || ViewAngle == 360) return true;
            var dot = (x * facingX + y * facingY) / Math.Sqrt(distanceSquared * directionLength);
            return dot + 1e-6 >= cosine;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public enum ObservedTargetKind { Ammo, Crew, Core }

    // Immutable data from one scan. No live Transform, HP or inventory references are exposed.
    public sealed class VisionObservation
    {
        public int TargetId { get; }
        public ObservedTargetKind Kind { get; }
        public TeamSide? TeamSide { get; }
        public float X { get; }
        public float Y { get; }
        public float Hp { get; }
        public float AmmoWeight { get; }
        public float AmmoDamage { get; }
        public float SeenAt { get; }
        public VisionObservation(int targetId, ObservedTargetKind kind, TeamSide? side, float x, float y,
            float hp, float ammoWeight, float ammoDamage, float seenAt)
        {
            TargetId = targetId;
            Kind = kind;
            TeamSide = side;
            X = x;
            Y = y;
            Hp = hp;
            AmmoWeight = ammoWeight;
            AmmoDamage = ammoDamage;
            SeenAt = seenAt;
        }
    }

    public sealed class VisionMemory
    {
        private Dictionary<int, VisionObservation> byId = new();
        public IReadOnlyList<VisionObservation> Observations { get; private set; } = Array.Empty<VisionObservation>();
        public void Replace(IEnumerable<VisionObservation> observed)
        {
            if (observed == null) throw new ArgumentNullException(nameof(observed));
            var next = new Dictionary<int, VisionObservation>();
            foreach (var observation in observed)
            {
                if (observation == null) throw new ArgumentException("Null vision observation.");
                next[observation.TargetId] = observation;
            }
            var list = new List<VisionObservation>(next.Values);
            list.Sort((left, right) => left.TargetId.CompareTo(right.TargetId));
            byId = next;
            Observations = list.AsReadOnly();
        }
        public bool IsCurrent(VisionObservation observation) => observation != null &&
            byId.TryGetValue(observation.TargetId, out var current) && ReferenceEquals(observation, current);
        public void Remove(int targetId)
        {
            if (!byId.Remove(targetId)) return;
            Replace(byId.Values);
        }
        public void Clear() => Replace(Array.Empty<VisionObservation>());
    }
}
