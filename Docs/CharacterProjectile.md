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

通常砲弾は AmmoProjectile 化済みです（下記「通常砲弾の飛翔体化」）。Direct / Arc の砲台別選択も実装済みです。

## 着地点

現状は敵船中心への着地です。最終設計ではShipDefinitionの侵入用着地Anchorを使います。

## 弾道（Direct / Arc）

最終確認日 2026-11-07。弾道は `ProjectileTrajectory`（`Assets/KadokaShipBattler/Scripts/Runtime/Ammo`）が2D上の通り道として表します。

- Direct: 発射位置から目標への直線です。
- Arc: 直線に、頂点の高さが距離の35%の放物線を足した山なりの曲線です。
- 砲台ごとに `CannonController.TrajectoryKind` で選びます。
- 高度による衝突の有無は使いません。2D上で当たり判定が実際に重なった時刻にだけ衝突します。軌道が交差しても通過時刻が違えば衝突しません（`Tools/CI/Projectiles` で検証）。

通常砲弾（AmmoProjectile）も同じ弾道を使います。

## 通常砲弾の飛翔体化

最終確認日 2026-11-07。Issue #38 の最後の残件で、通常砲弾を即時着弾から飛翔体へ変えました。

| 要素 | 役割 |
| --- | --- |
| `AmmoShotModel`（純C#） | 砲弾1発の飛行・結末。飛行は `CharacterProjectileModel`（衝突耐久 `UpdateContact`/`Hit` と `ProjectileTrajectory`）を使う。 |
| `ProjectileClashField`（純C#） | 飛行中の通常砲弾とキャラクター弾を登録し、敵対する弾同士の重なりを `UpdateContact` へ渡す。味方同士は衝突しない。 |
| `AmmoProjectile`（Unity） | 砲弾の座標反映・後始末。`AdvanceAll` で複数弾をフレームを待たず進められる。 |
| `CannonState.Detach` | 発射条件を満たした装填弾を砲台から取り外す。ダメージと山札返却は着弾側が行う。 |
| `IHardAmmo` | 弾の硬さ。`AmmoStats` / `AmmoDefinition` / `AmmoDeckRound` が実装し、未実装の弾は1。 |

- 発射時にダメージは与えません。着地した時にだけ `ShipBattleState.ApplyHullDamage` を呼びます。
- 迎撃（撃墜）・取消（場面の破棄）ではダメージなしで山札へ返します。結末は1回だけ確定し、山札へも1回だけ返します。
- 硬さは四捨五入した整数（1未満・非有限は1、上限1000）で、通常衝突1回につき1減ります。硬さ3の砲弾は硬さ1の砲弾を迎撃して残り2で着弾します。
- 弾道は `CannonController.TrajectoryKind` に従い、キャラクター弾と同じ衝突場に参加します（軌道の種類は問わず、2D上で重なった時だけ衝突）。
- 検証: `Tools/CI/Projectiles`（純C#）と PlayMode `AmmoProjectileTests`。`CannonState.FireAt`（即時着弾）は純C#モデルのCI用に残してあり、ゲーム本体の `CannonController.FireAt` は使いません。
- 未対応: `ProjectileRuntime` への完全統合、`PreClash` の通常砲弾への特殊効果の差し込み、盾・反射などのEffect（Issue #41）。
