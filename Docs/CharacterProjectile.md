# キャラクターの弾化

Issue #16。キャラクターを砲台へ装填して敵船へ発射し、飛行中は弾として扱います。

## 構成

| 要素 | 役割 |
| --- | --- |
| `CharacterProjectileModel` | 純C#の飛行・衝突・着地モデル。CIで直接検証します。 |
| `CharacterProjectile` | Unity側。発射中の船員の行動を止め、着地で復帰、撃墜で戦闘不能にします。 |
| `CannonController.TryLoadCharacter` | 同じ陣営の生存した船員を空の砲台へ装填します。 |
| `IProjectileBehaviour` | キャラ固有の発射中挙動の拡張口。飛行速度の倍率だけを返します。 |

## 規則

- 硬さは `CharacterDefinition.ProjectileHardness`。弾同士は硬い方が残り、同じ硬さなら両方落ちます。
- 通常の砲弾は硬さ以下の飛翔体を撃ち落とします（`ClashWithShell`）。
- 生きて敵船へ届いた船員は、そのまま `PlayerCrewController` を再有効化して行動へ復帰します。
- 入力は共通です。砲台の操作権を持つ船員が `FireAt` するだけで、キャラ固有の挙動は `IProjectileBehaviour` に閉じます。

## 既知の限界

- 通常の砲弾はまだ即着弾で飛翔体を持たないため、Unity上で砲弾とキャラ弾が空中衝突することはありません。`ClashWithShell` はモデルとして用意済みで、砲弾が飛翔体になった時に接続します。
- 着地点は敵船の中心です。船内の部屋への配置は未実装です。
