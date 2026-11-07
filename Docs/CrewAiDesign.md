# 船員AI・方針設計

## 目的

5人編成の船員が、キャラクター固有能力・チーム方針・個人方針・観測情報・学習値に応じて異なる行動を取る。

スラもり3の「仲間ごとに選べる作戦が違う」構造を参考にしつつ、Kadokaでは固定スクリプトではなくUtility AIへ統合する。

## 制御の階層

```text
Team Policy
    ↓
Character Policy
    ↓
Character Capability / Allowed Policies
    ↓
Perception + Known Map + Team Blackboard
    ↓
Utility Candidate Scoring
    ↓
Task Reservation
    ↓
Action Execution
```

## プレイヤー操作との切替

直接操作は常に1人だけ。

別の船員へ切り替えた瞬間、元の船員はAIへ戻る。

AI復帰時に保持するもの:

- 現在位置
- 現在向き
- 現在HP
- 所持中の弾
- 学習値
- キャラクター方針

AI復帰時に破棄するもの:

- プレイヤー入力
- 直接操作中の一時的な移動意図
- プレイヤー専用のインタラクション状態

復帰したAIは、その位置・向きから視界を更新し、新しく行動評価を行う。

## AIが知ってよい情報

### 常時既知

自船についてのみ:

- Room Graph
- ドア接続
- 砲台位置
- 侵入口
- コア位置
- 弾出現地点

### 観測が必要

- 弾の現在位置
- 敵船員の現在位置
- 味方／敵の現在行動
- 敵船内部の状況
- 敵船内の弾
- 敵キャラのHP

視界は前方扇状。

### 禁止

- プレイヤー入力の読み取り
- 未来の弾出現結果
- 視界外の敵の現在座標の直接参照
- 未観測の敵船内部オブジェクトへの直接参照

## 観測共有

各AIのVisionSensorは、その瞬間に観測した情報をSnapshotとしてチームBlackboardへ報告できる。

共有情報は「最後に見た事実」であり、対象へのライブ参照を持たない。

```text
Observation
├─ targetId
├─ kind
├─ observedPosition
├─ observedHp
├─ observedAmmoData
├─ observedAt
└─ observerId
```

古い観測にはTTLを設ける。

行動を実行する直前には、必要なら本人の視界で再確認する。

## チーム方針

艦全体の大きな目的。

例:

- `Balanced`: 状況に応じる。
- `Bombard`: 弾運搬・装填・砲撃を強化。
- `Board`: 敵船侵入を強化。
- `Defend`: 自船内の敵排除を強化。

チーム方針は「行動を強制」するのではなくUtility Scoreへ倍率を掛ける。

ただし最終フェーズなど、戦闘ルール上不可能な行動は候補にならない。

## キャラクター方針

### 能力だけでは決めない

同じCombat能力を持つ2キャラでも、選択できる方針は異なってよい。

そのため、方針の利用可否を次の2段階で判定する。

1. `requiredCapabilities` を満たす。
2. CharacterDefinition の `allowedPolicyIds` に含まれる。

```text
CharacterDefinition
├─ capabilities
├─ allowedPolicyIds[]
└─ uniqueActionProviders[]
```

これにより「戦闘可能だから全員が警備を選べる」という構造を避けられる。

### 個人方針の例

- 自由行動
- 弾を持ってこい
- 直射砲を使え
- 山なり砲を使え
- 敵を追い出せ
- 敵船に乗り込め
- リーダーを守れ
- 弾を迎撃しろ
- 弾を盗め
- キャラクター固有方針

キャラ固有方針は、単なる倍率だけで表現できない場合に専用Action Providerを追加できる。

## Utility AI

### 候補生成

能力と現在状態から、実行可能な候補だけ作る。

例:

- CarryAmmo
- LoadDirectCannon
- LoadArcCannon
- FireDirectCannon
- FireArcCannon
- DefendShip
- BoardEnemyShip
- AttackCrew
- AttackCore
- InterceptProjectile
- CharacterUniqueAction

### 評価値

基本形:

```text
finalScore =
    baseScore
  × suitability
  × teamPolicyWeight
  × characterPolicyWeight
  × learningWeight
  × tacticalModifier
```

行動不能・能力不足・到達不能は0点ではなく「候補から除外」にする。

### 適性

キャラクター固有の違いを評価へ反映する。

- combatSkill
- carrySkill
- moveSpeed
- carryCapacity
- maxCarryCount
- attackRange
- currentHp
- flying / phase flags

戦闘が強いキャラは運搬能力を低く設定できるが、AI側で「戦闘型」という固定職を持たせる必要はない。能力値の結果として行動傾向が変わる。

## タスク予約

5人が同じ対象へ殺到しないよう、対象を予約できる。

対象例:

- 弾
- 砲台
- 侵入者
- 侵入先
- 防衛地点

予約には期限と優先度を持つ。

専門方針を設定されたキャラは、一般キャラより高い優先度で予約を引き継げる。

直接操作へ切り替わったキャラのAI予約は直ちに解放する。

## 敵AI

敵も同じ低レベルUtility AIを使う。

上位に `EnemyTacticalDirector` を置き、観測された状況からチーム方針を選ぶ。

敵専用のチート情報は与えない。

```text
EnemyTacticalDirector
   ↓ Team Policy
Enemy Crew Utility AI × 5
```

敵が賢く見える要因は、全知ではなく:

- 観測共有
- タスク分担
- 状況に応じたチーム方針変更
- プレイヤーの過去行動からの傾向推定
- キャラクター固有方針

で作る。

## 学習

学習は方針と分離する。

- 方針: プレイヤーが意図的に指定する。
- 学習値: 行動結果から少しずつ更新される。

学習対象はAction Weightを中心にする。

例:

- このデッキでは軽量弾の運搬成功率が高い。
- このキャラは侵入より砲撃補助の成功率が高い。
- 特定の弾を優先したときに砲撃効率が上がった。

学習値が能力制約を解除することはない。

攻撃手段を持たないキャラが学習によってGuard方針を使えるようになることもない。

## キャラクター固有実装

キャラ名でAIコードを分岐しない。

```text
CharacterDefinition
  + Capability
  + Allowed Policies
  + Action Provider
  + Presentation Profile
```

固有モーションはPresentation側。

固有行動はAction Provider側。

かどか／まるのように演出を優遇するキャラでも、性能値そのものを特別扱いせず実装できる。

## スラもり3から参考にする点

スラもり3では仲間ごとにHP・持てる数・選べる作戦が異なり、「弾を運ぶ」「砲撃する」「侵入する」「リーダーを守る」「弾を迎撃する」など役割がキャラ固有になっている。

Kadokaではこの「キャラごとの作戦差」を、固定行動AIではなく:

- CharacterDefinition
- AllowedPolicyIds
- Utility Action Provider
- Policy Weight

へ分解して実装する。
