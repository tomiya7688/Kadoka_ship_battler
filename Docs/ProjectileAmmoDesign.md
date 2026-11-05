# Projectile・弾特殊効果設計

## 目的

弾、キャラクター弾、特殊弾を同じ飛翔体基盤で扱いながら、スラもり3のような多様な効果を弾名による分岐なしで追加できるようにする。

## 1. 硬さの意味

`Hardness` は大小比較用の防御力ではなく、**飛翔体同士の通常衝突に耐えられる回数を表す耐久値**とする。

定義値と実行時残量を分離する。

```text
MaxHardness       定義値
RemainingHardness 実行時残量
```

通常の発射可能Projectileは `MaxHardness >= 1`。

### 通常衝突

敵対する2つのProjectileが衝突した場合、特殊効果による上書きがなければ双方の `RemainingHardness` を1減らす。

```text
left.remainingHardness  -= 1
right.remainingHardness -= 1

remainingHardness <= 0 -> Destroyed
```

例:

- 硬さ1 vs 硬さ1 -> 両方消滅
- 硬さ2 vs 硬さ1 -> 前者は残り1で継続、後者は消滅
- 硬さ3の盾 -> 通常衝突を3回処理した時点で消滅
- 硬さ5 -> 通常衝突を5回処理できる

硬いProjectileが柔らかいProjectileに当たっても無傷ではない。

### 同一ペアの多重判定

1回の交差で同じ2個が複数フレーム衝突した扱いにならないようにする。

- 物理イベントを使う場合はEnter相当のみ。
- 独自判定の場合は「分離するまで同一ペアを再解決しない」。

## 2. 衝突対象

通常衝突を行う条件:

- 敵対陣営である。
- 2D上の衝突範囲が重なる。
- 論理高度の差が衝突許容値以内。
- 双方が `Interceptable`。
- 同一ペアの現在の交差が未処理。

味方同士のProjectileは通常衝突しない。

## 3. 弾道と論理高度

2D見下ろし表示とは別に論理高度を持つ。

```text
ProjectileState
├─ position2D
├─ trajectory
├─ logicalHeight
├─ speed
├─ remainingHardness
└─ status
```

- Direct: 低高度中心。
- Arc: 中盤で高高度になる。
- Special: Behaviourが高度曲線を変更できる。

この高度差により、直射弾と山なり弾が画面上で交差しても相殺されない場合を作る。

## 4. 特殊効果による通常衝突の上書き

特殊弾は `PreClash` で通常硬さ処理を上書きできる。

```text
PreClash effects
    ↓ handled?
Yes -> override result
No  -> normal hardness -1 / -1
    ↓
PostClash effects
    ↓
destroy / continue
```

上書きが複数ある場合は `Priority` の高いものを1つだけ採用する。同Priorityは定義順で決め、データ検証で警告する。

### CollisionOverride

最低限:

- `None`: 通常処理
- `Ignore`: 互いに影響せず通過
- `DestroySelf`
- `DestroyOther`
- `DestroyBoth`
- `ReflectOther`
- `TransformSelf`

## 5. Projectile Tag

効果対象を弾名で判定しない。

```text
ProjectileTag
- Shield
- Character
- Summon
- Explosive
- Reflectable
- Interceptable
```

必要になった時だけ追加する。

例:

- HammerEffect: 相手がShieldならDestroyOther。
- MirrorEffect: 相手がReflectableならReflectOther。
- GhostEffect: 通常弾との衝突をIgnoreし、専用Breakerだけ有効。

## 6. 特殊効果は4領域に分ける

特殊な「弾アイテム」の効果を1つの巨大Behaviourへ入れない。

### A. ProjectileEffect

発射後にだけ作用する。

Hook:

- OnLaunch
- Tick
- PreClash
- PostClash
- OnHitShip
- OnHitCrew
- OnDestroyed
- OnExpire

例:

- 盾
- ハンマー
- 反射
- 周囲の弾を消す
- 召喚攻撃
- 多段ヒット
- 着弾後に残留
- 追尾
- 8の字飛行

### B. CarryEffect

船員が所持している間だけ作用する。

例:

- 移動速度補正
- 透明化
- 所持者への状態変化

ProjectileEffectとは別。

### C. DeckPassiveEffect

25枠デッキに入っているだけで作用する。

例:

- 弾出現間隔補正

同じ弾を複数積んだ場合の重複方式は各EffectDefinitionが定義する。

### D. AmmoCombinationRule

複数アイテムを同時に担いだ時の変化。

例:

- Aを3つ -> B
- A/B/Cを指定順で保持 -> D

レシピは複数AmmoDefinitionを参照するため、AmmoDefinition内部ではなく独立したルールデータにする。

## 7. データ構造

```text
AmmoDefinition
├─ id
├─ damage
├─ weight
├─ maxHardness : int
├─ speed
├─ allowedTrajectories
├─ projectileTags
├─ projectileEffects[]
├─ carryEffects[]
└─ deckPassiveEffects[]

AmmoCombinationDefinition
├─ ingredients[]
├─ orderingRule
└─ resultAmmo
```

### Effect Definition

Unity側では各効果をScriptableObjectの定義アセットとして持つ。

```csharp
public abstract class ProjectileEffectDefinition : ScriptableObject
{
    public int Priority { get; }
    public abstract IProjectileEffect CreateRuntime();
}
```

RuntimeはUnity Objectを直接ルール状態として保持せず、純C#のEffect Runtimeへ変換する。

これによりCIで衝突ルールをUnity無しでも検証可能にする。

## 8. 共通Projectile Runtime

AmmoProjectileとCharacterProjectileを最終的に共通状態へ寄せる。

```text
ProjectileRuntime
├─ Source
│  ├─ AmmoDeckRound
│  └─ CrewMember
├─ TeamSide
├─ Damage
├─ MaxHardness
├─ RemainingHardness
├─ Trajectory
├─ LogicalHeight
├─ Tags
├─ Effects
└─ Status
```

キャラクター弾がHardness 2なら、通常弾1発との衝突後はRemainingHardness 1で飛び続ける。次の通常衝突で撃墜される。

撃墜されたCharacterProjectileは共通のDowned / Respawn処理へ入る。

## 9. 特殊効果例

### Shield

- maxHardnessを高くする。
- Shield tag。
- 基本的には専用コード不要。

### Shield Breaker Hammer

- PreClashで相手のShield tagを確認。
- 該当する場合 `DestroyOther`。
- 自分の硬さを通常衝突では減らさない。
- 盾以外には通常衝突。

### Reflector

- PreClashでReflectableなら `ReflectOther`。
- 反射後は所有陣営、進行方向、対象船を更新する。

### Area Clear

- OnHit / OnDestroyedで範囲内Projectileへ破壊要求。
- 自分を起点とする一度だけのArea Effect。

### Transform On Clash

- 1回目の衝突で別Profileへ変化。
- damage / hardness / presentationを差し替える。
- 日本刀のような「強力な1回目の迎撃後に弱体化」を表現できる。

### Summon

- Projectile自体が直接船体へダメージを与えるのではなく、OnHitShipでSummon Actionを発行する。
- 演出とダメージ計算は分離する。

## 10. AIとの接続

AIは弾名ではなく評価用情報を読む。

最低限:

- damage
- weight
- remaining / max hardness
- flight speed
- usable cannon trajectories
- AI tags / expected effects

特殊効果は `AiUtilityHints` を定義できる。

例:

- antiShield
- interception
- areaClear
- boardingSupport
- shipDamage
- defense

AIはEffectクラスの型名を直接条件分岐に使わない。

## 11. スラもり3から参考にした点

スラもり3では、通常弾の多くが硬さ1、スライダーバッジが硬さ2、鉄の盾が3、メタルキングの盾が5で、鉄の盾は3発まで相手弾を防ぐ。キャラクターのキラーマシンも硬さ2で、侵入時に1発なら耐える。したがってKadokaでは硬さを衝突耐久として扱う。

また、盾を一方的に壊すハンマー、反射、周囲の弾を消す弾、デッキ常駐効果、所持中効果、複数アイテムの合成など、硬さだけでは表現できない効果が多いため、Effectを領域別に分離する。
