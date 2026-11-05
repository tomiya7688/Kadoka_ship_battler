using KadokaShipBattler.Core;
using UnityEngine;

namespace KadokaShipBattler.Characters
{
    public sealed class CrewControlDirector : MonoBehaviour
    {
        private readonly CrewControlState state = new();
        public PlayerCrewController Current => state.Current as PlayerCrewController;
        public ICrewInputSource InputSource { get; set; } = new KeyboardCrewInput();
        public bool Register(PlayerCrewController crew) => state.Register(crew);
        public bool TrySwitch(PlayerCrewController crew) => state.TrySwitch(crew);
        public bool CycleNext() => state.CycleNext();

        private void Update()
        {
            state.EnsureAvailableSelection();
            if (Current == null || Current.Arena != null && Current.Arena.IsFinished) return;
            ApplyInput(InputSource.Read(), Time.deltaTime);
        }

        public void ApplyInput(CrewInput input, float deltaTime)
        {
            state.EnsureAvailableSelection();
            if (Current == null || Current.Arena != null && Current.Arena.IsFinished) return;
            if (input.SwitchNext) state.CycleNext();
            Current.ApplyInput(input, deltaTime);
        }

        private void OnDestroy() => state.ReleaseAll();
    }
}
