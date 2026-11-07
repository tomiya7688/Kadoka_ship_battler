using UnityEngine;

namespace KadokaShipBattler.Characters
{
    public readonly struct CrewInput
    {
        public Vector2 Movement { get; }
        public bool Interact { get; }
        public bool Attack { get; }
        public bool SwitchNext { get; }
        public CrewInput(Vector2 movement, bool interact = false, bool attack = false, bool switchNext = false)
        {
            Movement = movement;
            Interact = interact;
            Attack = attack;
            SwitchNext = switchNext;
        }
    }

    public interface ICrewInputSource
    {
        CrewInput Read();
    }

    public sealed class KeyboardCrewInput : ICrewInputSource
    {
        public CrewInput Read() => new CrewInput(
            new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
            Input.GetKeyDown(KeyCode.E), Input.GetKeyDown(KeyCode.Space), Input.GetKeyDown(KeyCode.Tab));
    }
}
