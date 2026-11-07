using System.Collections.Generic;
using UnityEngine;

namespace KadokaShipBattler.Ammo
{
    [CreateAssetMenu(menuName = "Kadoka Ship Battler/Ammo Deck", fileName = "AmmoDeck")]
    public sealed class AmmoDeckDefinition : ScriptableObject
    {
        [SerializeField] private List<AmmoDefinition> entries = new();

        public IReadOnlyList<AmmoDefinition> Entries => entries.AsReadOnly();

        public void Initialize(IReadOnlyList<AmmoDefinition> definitions)
        {
            if (definitions == null || definitions.Count != AmmoDeckState.DeckSize)
                throw new System.ArgumentException("An ammo deck requires exactly 25 entries.");
            var copy = new List<AmmoDefinition>(AmmoDeckState.DeckSize);
            foreach (var definition in definitions)
            {
                if (definition == null) throw new System.ArgumentException("Null ammo deck entry.");
                copy.Add(definition);
            }
            entries = copy;
        }

        public AmmoDeckState CreateState(int seed) => new AmmoDeckState(entries, seed);
    }
}
