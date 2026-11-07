# キャラクターの弾化

Issue #16。キャラクターを砲台へ装填して敵船へ発射し、飛行中は通常弾と同じProjectile基盤へ参加させます。

詳細な硬さ・衝突・特殊効果の確定仕様は [Projectile・弾特殊効果設計](ProjectileAmmoDesign.md) を正とします。

## 構成

| 要素 | 役割 |
| --- | --- |
| `CharacterProjectileModel` | 純C#の飛行・衝突・着地モデル。共通Projectile Runtimeへ統合予定です。 |
| `CharacterProjectile` | Unity側。発射中の船員の行動を止め、着地で復帰、撃墜でDownedへ移します。 |
| `CannonController.TryLoadCharacter` | 同じ陣営の生存した船員を空の砲台へ装填します。 |
| `IProjectileBehaviour` | 既存の拡張口。Issue #41のEffect Runtimeへ段階的に統合します。 |

## 確定規則

- 定義硬さは `CharacterDefinition.ProjectileHardness`。
- 実行時には `RemainingHardness` を持つ。
- 通常衝突1回につき双方のRemainingHardnessを1減らす。
- RemainingHardnessが0以下になったキャラクター弾は撃墜される。
- 例: 硬さ2のキャラは通常弾1発を受けても残り1で飛び続け、2発目で撃墜される。
- Direct / Arcなど利用可能な弾道をキャラ定義で制限できる。
- 生きて敵船へ届いた船員は船員状態へ復帰する。
- 撃墜された船員は共通のDowned / Respawnサイクルへ入る。
- 特殊キャラの飛行中挙動は弾名・キャラ名分岐ではなくEffect / Action Providerで追加する。

## 現行コードとの差分

衝突耐久（硬さ）は Issue #38 の確定仕様どおりに実装済みです（最終確認日 2026-11-07）。

- `CharacterProjectileModel` は整数の `MaxHardness` / `RemainingHardness` を持ちます。硬さは1以上の整数です。
- `UpdateContact(left, right, overlapping)` が、重なり始めの1回だけ双方を `Hit()`（-1）します。重なりが外れると次の接触を数え直します。同じ2弾の接触を複数フレームで加算しません。
- `IProjectileClashEffect.PreClash` が `true` を返すと通常の耐久処理を上書きします（盾割り・すり抜け等）。
- 通常砲弾1発は `HitByShell()` で耐久を1減らします。

```text
left.remainingHardness  -= 1
right.remainingHardness -= 1
remainingHardness <= 0 -> Destroyed
```

通常砲弾は現在まだ即時着弾処理を含むため、AmmoProjectile化と2砲台（Direct / Arc）化は Issue #38 の残件です。

## 着地点

現状は敵船中心への着地です。最終設計ではShipDefinitionの侵入用着地Anchorを使います。
