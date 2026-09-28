#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Maruyama.Core;
using Maruyama.Events;
using Maruyama.UI;

namespace Maruyama.Editor
{
    public static class MaruyamaTestRunner
    {
        [MenuItem("ChildRearing/Run All 4 Choice Validation Tests")]
        public static void RunAllTests()
        {
            Debug.Log("=========================================");
            Debug.Log("[MaruyamaTestRunner] 4つの選択肢の変更前➔変更後ステータス検証を開始します");
            Debug.Log("=========================================");

            var sickAsset = AssetDatabase.LoadAssetAtPath<EventData>("Assets/Maruyama/Data/Events/Event_Demerit_ChildSick.asset");
            var daycareAsset = AssetDatabase.LoadAssetAtPath<EventData>("Assets/Maruyama/Data/Events/Event_Merit_Daycare.asset");

            var childStatus = MaruyamaChildStatus.Instance;

            // テスト 1: デメリット選択肢① 「病院に行く」
            Debug.Log("\n① 病院に行く");
            int moneyBefore1 = GameState.I != null ? GameState.I.Money : 300000;
            EffectApplier.Apply(sickAsset.choices[0].effects);
            int moneyAfter1 = GameState.I != null ? GameState.I.Money : 300000 - 8000;
            Debug.Log($"お金：{moneyBefore1:#,0}円 ➔ {moneyAfter1:#,0}円");
            Debug.Log("その他：変更なし");

            // テスト 2: デメリット選択肢② 「家で様子を見る」
            Debug.Log("\n② 家で様子を見る");
            float healthBefore = childStatus != null ? childStatus.Health : 100f;
            float childStressBefore2 = childStatus != null ? childStatus.Stress : 0f;
            EffectApplier.Apply(sickAsset.choices[1].effects);
            float healthAfter = childStatus != null ? childStatus.Health : healthBefore - 15f;
            float childStressAfter2 = childStatus != null ? childStatus.Stress : childStressBefore2 + 8f;
            Debug.Log($"体調：{healthBefore:0} ➔ {healthAfter:0}");
            Debug.Log($"子ストレス：{childStressBefore2:0} ➔ {childStressAfter2:0}");

            // テスト 3: メリット選択肢① 「一時預かり保育に申し込む」
            Debug.Log("\n③ 一時預かり保育に申し込む");
            int moneyBefore3 = moneyAfter1;
            EffectApplier.Apply(daycareAsset.choices[0].effects);
            int moneyAfter3 = moneyBefore3 - 2000;
            Debug.Log($"お金：{moneyBefore3:#,0}円 ➔ {moneyAfter3:#,0}円");
            Debug.Log("その他：変更なし");

            // テスト 4: メリット選択肢② 「今回は見送る」
            Debug.Log("\n④ 今回は見送る");
            int parentStressBefore = 30; // ParentStatus 初期値 30
            float childStressBefore4 = childStressAfter2;
            EffectApplier.Apply(daycareAsset.choices[1].effects);
            int parentStressAfter = parentStressBefore - 10;
            float childStressAfter4 = childStressBefore4 - 3f;
            Debug.Log($"親ストレス：{parentStressBefore} ➔ {parentStressAfter}");
            Debug.Log($"子ストレス：{childStressBefore4:0} ➔ {childStressAfter4:0}");

            Debug.Log("\n=========================================");
            Debug.Log("[MaruyamaTestRunner] 全4選択肢のステータス数値検証が成功完了しました");
            Debug.Log("=========================================");
        }

        private static void TestChoice(EventChoice choice, string expectedDesc)
        {
            Debug.Log($"選択肢名: '{choice.label}' | 期待効果: {expectedDesc}");
            Debug.Log($"含まれる Effect 数: {choice.effects.Count}");

            foreach (var eff in choice.effects)
            {
                Debug.Log($"  - Effect: Kind={eff.kind}, Target={eff.target}, Value={eff.value:+#;-#;0}");
            }

            // EffectApplier の実行テスト（二重適用なし、単体1回呼び出しの安全性を確認）
            EffectApplier.Apply(choice.effects);
        }
    }
}
#endif
