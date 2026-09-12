using System.Collections.Generic;
using UnityEngine;

namespace KadokaShipBattler.Ammo
{
    [CreateAssetMenu(menuName = "Kadoka Ship Battler/Ammo Deck", fileName = "AmmoDeck")]
    public sealed class AmmoDeckDefinition : ScriptableObject
    {
        [SerializeField] private List<AmmoDefinition> entries = new();

        public IReadOnlyList<AmmoDefinition> Entries => entries;
    }
}
