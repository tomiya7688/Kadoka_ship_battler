# Kadoka ship battler

船内で弾を運び、砲台を動かし、仲間と役割分担しながら敵船と戦う船バトルゲームです。

## Development

- Engine: **Unity 6.3 LTS**
- Language: **C#**
- Recommended baseline editor: `6000.3.15f1`

Unity Hub からリポジトリのルートディレクトリを開いてください。

## Architecture

ゲーム定義と実行ロジックを分離します。

- `ScriptableObject`: キャラクター、弾、AI方針などの定義データ
- `MonoBehaviour`: 船、船員、戦闘中の実体
- Utility AI: 行動候補を評価値で比較して選択
- Team policy / Character policy: Utility AI の評価値へ補正を加える
- Capability: キャラクターが実行可能な行動そのものを制限

キャラクター固有モーションや見た目は判断ロジックから分離し、同じ行動でもキャラクターごとに表現を差し替えられる構造を目指します。

## Directory

```text
Assets/KadokaShipBattler/
├─ Scripts/
│  └─ Runtime/
│     ├─ Core/
│     ├─ Ships/
│     ├─ Characters/
│     ├─ Ammo/
│     └─ AI/
├─ Data/
├─ Prefabs/
├─ Scenes/
└─ Tests/
```

## Initial gameplay target

1. プレイヤーと敵の船を1隻ずつ配置する
2. プレイヤーが船内を移動する
3. 弾を拾って砲台へ運ぶ
4. 砲台へ装填して発射する
5. 敵船にダメージを与える
6. 味方4人・敵5人をAIで動かす
7. AI方針・弾デッキ・キャラクター適性によって行動傾向を変える

## AI principles

- 味方AIは更新型評価関数を持ち、弾デッキや戦闘傾向へ徐々に適応する
- チーム方針とキャラクター方針は別々に設定する
- キャラクターごとに選択可能な方針が異なる
- 戦闘が強いキャラクターの多くは運搬を不得意とするなど、役割間にトレードオフを持たせる
- 敵AIは賢くするが、未観測の隠し情報を直接読むチートAIにはしない

## Characters

`Obake_Lisense` の「かどか」「まる」も登場予定です。両キャラクターは性能を大きく優遇するのではなく、専用モーションやリアクションなど演出面を厚くする方針です。
