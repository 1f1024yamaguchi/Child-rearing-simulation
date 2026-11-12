#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Kusao.TutorialGuide.Editor
{
    /// <summary>
    /// TutorialGuideController のインスペクター拡張。
    /// 実行中のテストボタンや、各ステップのオフセット微調整をサポートします。
    /// </summary>
    [CustomEditor(typeof(TutorialGuideController))]
    public class TutorialGuideControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var controller = (TutorialGuideController)target;

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "【スタートガイド（チュートリアル）】\n" +
                "ゲーム開始直後に画面上の数字やボタンをスポットライトで解説します。\n" +
                "・ボタンのハイライト枠がズレている場合は、下の一覧の [Position Offset] や [Padding] で微調整できます。\n" +
                "・再生中（Playモード）に数値を変更すると、リアルタイムに画面上の枠が動きます！",
                MessageType.Info
            );

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Play モード デバッグ操作", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("チュートリアル開始/リプレイ"))
                {
                    controller.ReplayTutorial();
                }
                if (GUILayout.Button("次のステップへ"))
                {
                    controller.NextStep();
                }
                if (GUILayout.Button("スキップ/完了"))
                {
                    controller.CompleteTutorial();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(6);
            DrawDefaultInspector();
        }
    }
}
#endif

