---
title: サウンド（SE・BGM・音量設定画面）の仕組みと設定手順
updated: 2026-10-30
status: 確定
related: [Assets/Yamaguchi/Scripts/Core/Audio/SoundSettings.cs, Assets/Yamaguchi/Scripts/Core/Audio/AudioManager.cs, Assets/Yamaguchi/Scripts/Core/Audio/ButtonSeAutoAttacher.cs, Assets/Yamaguchi/Scripts/UI/ButtonClickSE.cs, Assets/Yamaguchi/Scripts/UI/SoundSettingsPanelUI.cs, Assets/Yamaguchi/Scripts/Editor/ButtonSeSetupBuilder.cs]
---

# サウンド（SE・BGM・音量設定画面）の仕組みと設定手順

> SE（ボタンクリック音・効果音）、BGM（ランダム連続再生・単曲ループ）、および音量設定パネルを Unity エディタ上でセットアップ・利用するためのガイドです。

---

## 1. 使うスクリプト一覧（6つ）

| スクリプト | パス | 役割 | アタッチ先 |
|---|---|---|---|
| `SoundSettings` | `Assets/Yamaguchi/Scripts/Core/Audio/SoundSettings.cs` | SE / BGM の音量（`0〜1`）の保持と `PlayerPrefs`（`"sound_se"` / `"sound_bgm"`）への保存・通知を行う静的クラス | **不要**（MonoBehaviour ではないためシーンに置かない） |
| `AudioManager` | `Assets/Yamaguchi/Scripts/Core/Audio/AudioManager.cs` | SE と BGM を実際に再生するシングルトン（`AudioManager.I` / `DontDestroyOnLoad`）。BGM のランダム連続再生も担当 | シーン内の空の GameObject `AudioManager` |
| `ButtonSeAutoAttacher` | `Assets/Yamaguchi/Scripts/Core/Audio/ButtonSeAutoAttacher.cs` | 実行中にコードで動的生成される `Button`（食事選択・イベント選択肢など）にも自動で `ButtonClickSE` を付与する | `AudioManager` と同じ GameObject |
| `ButtonClickSE` | `Assets/Yamaguchi/Scripts/UI/ButtonClickSE.cs` | `Button` がクリックされたときに `AudioManager.I.PlaySe` を呼ぶ部品（`interactable == false` のときは鳴らさない） | シーン内の各 `Button` オブジェクト |
| `SoundSettingsPanelUI` | `Assets/Yamaguchi/Scripts/UI/SoundSettingsPanelUI.cs` | 音量設定パネルの開閉（開いている間は `GameClock.I.PushPause()` / `PopPause()` で一時停止）と、SE/BGM スライダーの連動・SE スライダーを離したときの試聴再生を担当 | `Canvas/SoundSettingsPanel`（パネル本体） |
| `ButtonSeSetupBuilder` | `Assets/Yamaguchi/Scripts/Editor/ButtonSeSetupBuilder.cs` | メニューから開いているシーン内の全 `Button` に `ButtonClickSE` を一括付与するエディタ拡張 | **不要**（上部メニューから実行） |

---

## 2. Unity エディタでの作業手順（上から順に実施）

### 手順①：`AudioManager` オブジェクトの配置（SE・BGM の再生役）

`Start_scene` と `Main_test2` の両方（または最初に開くシーン）に配置します。両方に入れておいても、シーン遷移時に重複した方は自動で破棄されます。

1. **既存オブジェクトの整理（`Main_test2` のみ）**
   - `GameManager` の子に古い `Audio_manager` がある場合は削除するか、古い `Missing` スクリプトを外してルート階層に出します。
2. **GameObject の作成**
   - Hierarchy のルート階層（一番上）で **Create Empty** し、名前を `AudioManager` にします。
3. **コンポーネントの追加**
   - `AudioManager` オブジェクトに `Assets/Yamaguchi/Scripts/Core/Audio/AudioManager.cs` をアタッチします。
   - 同じ `AudioManager` オブジェクトに `Assets/Yamaguchi/Scripts/Core/Audio/ButtonSeAutoAttacher.cs` もアタッチします（これで実行時に生成されるボタンにも自動で SE が付きます）。
   - 必要であれば同じオブジェクトに `AudioSource` を2つ追加します（**追加しなくても実行時に自動で2つ追加されます**）。
   - ※手動で `AudioSource` を付けた場合は、両方の **`Play On Awake` のチェックを必ず外して**ください。
4. **Inspector の設定（`AudioManager`）**
   - **`Se Source`**：SE 用 `AudioSource`（空欄なら自動生成）
   - **`Bgm Source`**：BGM 用 `AudioSource`（空欄なら自動生成）
   - **`Default Click Clip`**：ボタンを押したときに鳴らす既定のクリック音（`AudioClip`）を入れる
   - **`Bgm Clips`**：ランダムに流したい BGM の `AudioClip` を `+` ボタンで複数登録する（1曲終わるごとに次の曲がランダムに選ばれ、2曲以上ある場合は直前と同じ曲が連続しないように再生されます）
   - **`Play Random Bgm On Start`**：チェック ON（起動時に自動でランダム BGM 再生を開始）

---

### 手順②：音量設定パネル（`SoundSettingsPanel`）の作成

`Main_test2`（必要なら `Start_scene` にも）の `Canvas` の下に以下の階層を作成します。

```text
Canvas
└── SoundSettingsPanel            # [SoundSettingsPanelUI] をアタッチ（初期状態: 非アクティブでOK）
    ├── Dim                       # 背景の暗幕 Image（半透明の黒など）
    └── Window                    # 設定ウィンドウ背景 Image
        ├── TitleText             # TMP_Text「サウンド設定」
        ├── SeRow
        │   ├── Label             # TMP_Text「SE 音量」
        │   └── SeSlider          # UI > Slider（Min Value: 0, Max Value: 1, Whole Numbers: OFF）
        ├── BgmRow
        │   ├── Label             # TMP_Text「BGM 音量」
        │   └── BgmSlider         # UI > Slider（Min Value: 0, Max Value: 1, Whole Numbers: OFF）
        └── CloseButton           # UI > Button（子 TMP_Text「とじる」）
```

1. **`SoundSettingsPanel` に `SoundSettingsPanelUI.cs` をアタッチ**し、Inspector で以下を設定します。
   - **`Panel Root`**：`SoundSettingsPanel` 自身をドラッグ＆ドロップ
   - **`Se Slider`**：`SeSlider` をドラッグ＆ドロップ（つまみを離したときに試聴 SE が1回鳴る部品は実行時に自動で付きます）
   - **`Bgm Slider`**：`BgmSlider` をドラッグ＆ドロップ
   - **`Close Button`**：`CloseButton` をドラッグ＆ドロップ
2. 設定が終わったら、`SoundSettingsPanel` オブジェクト自体の Inspector 左上のチェックを外して**非アクティブ（非表示）**にしておきます。

---

### 手順③：設定ボタン（歯車ボタン）から設定パネルを開く配線

1. `Main_test2` シーンの `Canvas/Buttons/settings` オブジェクトを選択します。
2. `Button` コンポーネントの **`On Click ()`** で **`+`** を押します。
3. オブジェクト欄に `Canvas/SoundSettingsPanel` をドラッグ＆ドロップします。
4. 関数欄で **`SoundSettingsPanelUI` → `Open ()`** を選択します。

---

### 手順④：全ボタンにクリック SE（`ButtonClickSE`）を一括で付ける

1. ボタン SE を付けたいシーン（`Main_test2` や `Start_scene`）を開きます。
2. Unity 上部メニューから **「子育て → サウンド → 全ボタンにSEを付ける」** をクリックします。
   - シーン内のすべての `Button`（非アクティブ含む）に自動で `ButtonClickSE` が付きます。
   - 特定のボタンだけ別の音を鳴らしたい場合は、そのボタンの `ButtonClickSE` の **`Override Clip`** に個別の `AudioClip` を入れます（空欄なら `AudioManager` の `Default Click Clip` が鳴ります）。
3. **Ctrl + S** でシーンを保存します。

---

## 3. 他のスクリプトから SE / BGM を鳴らしたいとき（コード例）

他のイベントや演出スクリプトから音を鳴らす場合は、`AudioManager.I` を呼び出します（`AudioManager` がシーンに無い場合でもエラーにならないよう `?.` または `null` チェックを付けます）。

```csharp
// 1. 既定のクリック音を鳴らす
if (AudioManager.I != null)
{
    AudioManager.I.PlaySe(null);
}

// 2. 指定した SE（AudioClip）を鳴らす
if (AudioManager.I != null)
{
    AudioManager.I.PlaySe(mySeClip);
}

// 3. 特定の BGM 1曲をループ再生に切り替える（エンディング専用曲など）
if (AudioManager.I != null)
{
    AudioManager.I.PlayBgm(endingBgmClip);
}

// 4. ランダム BGM 連続再生に戻す
if (AudioManager.I != null)
{
    AudioManager.I.StartRandomBgm();
}

// 5. BGM を止める
if (AudioManager.I != null)
{
    AudioManager.I.StopBgm();
}
```

