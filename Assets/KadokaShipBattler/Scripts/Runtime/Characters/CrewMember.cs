using KadokaShipBattler.Core;
using UnityEngine;

namespace KadokaShipBattler.Characters
{
    public sealed class CrewMember : MonoBehaviour
    {
        [SerializeField] private CharacterDefinition definition;
        [SerializeField] private TeamSide teamSide;

        public CharacterDefinition Definition => definition;
        public TeamSide TeamSide => teamSide;

        public bool Can(CharacterCapability capability)
        {
            return definition != null && definition.HasCapability(capability);
        }
    }
}
