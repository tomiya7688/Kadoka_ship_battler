using System.Collections.Generic;

namespace KadokaShipBattler.Core
{
    public interface IControlledCrew
    {
        TeamSide TeamSide { get; }
        bool IsAvailable { get; }
        void SetDirectControl(bool directControl);
    }

    // Shared by Unity and the license-free CI runner.
    public sealed class CrewControlState
    {
        private readonly List<IControlledCrew> members = new();
        public IReadOnlyList<IControlledCrew> Members => members.AsReadOnly();
        public IControlledCrew Current { get; private set; }

        public bool Register(IControlledCrew member)
        {
            if (member == null || !member.IsAvailable || member.TeamSide != TeamSide.Player || members.Contains(member))
                return false;
            members.Add(member);
            if (Current == null) Select(member);
            else member.SetDirectControl(false);
            return true;
        }

        public bool TrySwitch(IControlledCrew member)
        {
            if (member == null || !members.Contains(member) || !member.IsAvailable || member.TeamSide != TeamSide.Player || ReferenceEquals(member, Current))
                return false;
            Select(member);
            return true;
        }

        public bool CycleNext()
        {
            var index = members.IndexOf(Current);
            for (var offset = 1; offset <= members.Count; offset++)
            {
                var candidate = members[(index + offset) % members.Count];
                if (!ReferenceEquals(candidate, Current) && candidate.IsAvailable && candidate.TeamSide == TeamSide.Player)
                    return TrySwitch(candidate);
            }
            return false;
        }

        public void EnsureAvailableSelection()
        {
            if (Current != null && Current.IsAvailable && Current.TeamSide == TeamSide.Player) return;
            IControlledCrew next = null;
            foreach (var member in members)
                if (member.IsAvailable && member.TeamSide == TeamSide.Player) { next = member; break; }
            Select(next);
        }

        public void ReleaseAll()
        {
            Select(null);
            members.Clear();
        }

        private void Select(IControlledCrew next)
        {
            var previous = Current;
            Current = next;
            previous?.SetDirectControl(false);
            next?.SetDirectControl(true);
        }
    }
}
