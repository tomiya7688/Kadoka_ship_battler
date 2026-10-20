using System;
using KadokaShipBattler.Characters;

namespace KadokaShipBattler.AI
{
    /* {
        責務: 役割割り当てに必要な船員の能力を、Unity参照なしの値として保持します。
        前提: IdはUnityのインスタンスIDなどの戦闘内で一意な非0の値、Slotは編成順で小さいほど先頭です。
    } */
    public readonly struct EnemyCrewProfile
    {
        public int Id { get; }
        public int Slot { get; }
        public CharacterCapability Capabilities { get; }
        public bool CanAttack { get; }
        public float CarryCapacity { get; }

        /* { 処理: 船員の能力値を検証して保持します。例外: 未指定のID、負のスロット、範囲外の積載量を拒否します。 } */
        public EnemyCrewProfile(int id, int slot, CharacterCapability capabilities, bool canAttack, float carryCapacity)
        {
            // 実在しない船員や不正な能力値を役割へ割り当てません。
            if (id == 0 || slot < 0 || !UtilityRules.Range(carryCapacity, 0, float.MaxValue))
                throw new ArgumentException("Invalid enemy crew profile.");
            Id = id;
            Slot = slot;
            Capabilities = capabilities;
            CanAttack = canAttack && (capabilities & CharacterCapability.Combat) == CharacterCapability.Combat;
            CarryCapacity = carryCapacity;
        }

        /* { 処理: 指定した能力をすべて持つか判定します。 } */
        public bool Has(CharacterCapability capability) => (Capabilities & capability) == capability;
    }
}
