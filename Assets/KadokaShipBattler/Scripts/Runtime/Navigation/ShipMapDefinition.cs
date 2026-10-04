using UnityEngine;

namespace KadokaShipBattler.Navigation
{
    public sealed class ShipMapDefinition : ScriptableObject
    {
        public ShipMapData Map { get; private set; }
        public void Initialize(ShipMapData data) { data.Validate(); Map = data; }
    }
}
