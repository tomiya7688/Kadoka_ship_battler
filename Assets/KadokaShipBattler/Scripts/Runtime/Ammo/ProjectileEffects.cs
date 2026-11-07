using System;
using System.Collections.Generic;
using KadokaShipBattler.Characters;

namespace KadokaShipBattler.Ammo
{
    /* {
        責務: 弾の衝突時の特殊効果を、弾名ではなくタグと順序で組み合わせて解決する純粋なチェーンです。
        前提: 効果は登録順に評価し、最初にtrueを返した効果が通常の耐久処理を上書きします。同じ効果を複数の弾定義で再利用できます。
        フィールド: effects=登録順の効果。
        計算量: 衝突1回につき登録数に比例します。
    } */
    public sealed class ClashEffectChain : IProjectileClashEffect
    {
        private readonly List<IProjectileClashEffect> effects = new();

        public int Count => effects.Count;

        /* { 処理: 効果を末尾に登録します。引数: effectはnull不可。戻り値: 自身（連結用）。例外: nullを拒否します。 } */
        public ClashEffectChain Add(IProjectileClashEffect effect)
        {
            effects.Add(effect ?? throw new ArgumentNullException(nameof(effect)));
            return this;
        }

        /* { 処理: 登録順に評価し、最初に上書きした効果で止めます。戻り値: どれかが上書きしたらtrueです。 } */
        public bool PreClash(CharacterProjectileModel self, CharacterProjectileModel other)
        {
            foreach (var effect in effects)
                if (effect.PreClash(self, other)) return true;
            return false;
        }
    }

    /* { 責務: 指定タグを持つ相手を、耐久に関係なく撃墜する効果（盾割り）。自分は消耗しません。 } */
    public sealed class BreakTaggedProjectileEffect : IProjectileClashEffect
    {
        private readonly string tag;

        /* { 処理: 破壊対象のタグを決めます。例外: 空のタグを拒否します。 } */
        public BreakTaggedProjectileEffect(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) throw new ArgumentException("Tag is required.", nameof(tag));
            this.tag = tag;
        }

        public bool PreClash(CharacterProjectileModel self, CharacterProjectileModel other)
        {
            if (other == null || !other.HasTag(tag)) return false;
            other.Destroy();
            return true;
        }
    }

    /* { 責務: 指定タグを持つ相手との衝突を無視する効果（すり抜け）。双方とも消耗しません。 } */
    public sealed class IgnoreTaggedProjectileEffect : IProjectileClashEffect
    {
        private readonly string tag;

        /* { 処理: 無視する相手のタグを決めます。例外: 空のタグを拒否します。 } */
        public IgnoreTaggedProjectileEffect(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) throw new ArgumentException("Tag is required.", nameof(tag));
            this.tag = tag;
        }

        public bool PreClash(CharacterProjectileModel self, CharacterProjectileModel other) => other != null && other.HasTag(tag);
    }

    /* {
        責務: 複数の素材の組み合わせから結果の弾IDを引く、弾定義の外に置く独立した規則表です。
        前提: 順序なし。同じ素材IDの個数も区別する（多重集合）。同じ素材の組は一度だけ登録できます。
        フィールド: rules=素材集合のキーから結果IDへの表。
        計算量: 登録・検索とも素材数に比例します。
    } */
    public sealed class AmmoCombinationRules
    {
        private readonly Dictionary<string, string> rules = new();

        /* { 処理: 規則を登録します。引数: ingredientsは2個以上の空でないID、resultは結果ID。例外: 不正値と重複登録を拒否します。 } */
        public void Register(IReadOnlyCollection<string> ingredients, string result)
        {
            if (ingredients == null || ingredients.Count < 2) throw new ArgumentException("Two or more ingredients are required.", nameof(ingredients));
            if (string.IsNullOrWhiteSpace(result)) throw new ArgumentException("Result is required.", nameof(result));
            var key = KeyOf(ingredients);
            if (!rules.TryAdd(key, result)) throw new InvalidOperationException("Combination already registered: " + key);
        }

        /* { 処理: 素材から結果IDを引きます。戻り値: 見つかればtrueです。 } */
        public bool TryResolve(IReadOnlyCollection<string> ingredients, out string result)
        {
            result = null;
            return ingredients != null && ingredients.Count >= 2 && rules.TryGetValue(KeyOf(ingredients), out result);
        }

        private static string KeyOf(IEnumerable<string> ingredients)
        {
            var sorted = new List<string>();
            foreach (var id in ingredients)
            {
                if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Ingredient id is required.");
                sorted.Add(id);
            }
            sorted.Sort(StringComparer.Ordinal);
            return string.Join("\u001f", sorted);
        }
    }
}
