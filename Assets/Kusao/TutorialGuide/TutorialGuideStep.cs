using System;
using UnityEngine;

namespace Kusao.TutorialGuide
{
    /// <summary>
    /// ガイドステップにおけるプレイヤーの進行待機タイプ
    /// </summary>
    public enum GuideActionType
    {
        /// <summary>ポップアップの［次へ］ボタンで進行する（テキスト・中身説明など）</summary>
        WaitNextButton,

        /// <summary>スポットライトで指定されたボタンをプレイヤーが押すのを待つ</summary>
        WaitButtonClick,
    }

    /// <summary>
    /// ステップ進行時に開閉を連動させる画面の対象
    /// </summary>
    public enum GuideScreenTarget
    {
        None,
        DetailPanel,       // 家族詳細パネル (SlidePanel)
        SupportPanel,      // サポート制度画面 (supports)
        CalendarPanel,     // カレンダー画面 (CalendarUI)
        LedgerPanel        // 家計簿画面 (LedgerPanel)
    }

    /// <summary>
    /// チュートリアルガイドの1ステップを表すデータモデル
    /// </summary>
    [Serializable]
    public class TutorialGuideStep
    {
        [Tooltip("ステップの識別用ID（デバッグや並び替え用）")]
        public string stepId;

        [Tooltip("ポップアップの見出しタイトル")]
        public string title;

        [TextArea(2, 6)]
        [Tooltip("ポップアップに表示する説明文")]
        public string description;

        [Tooltip("ハイライト対象のUI GameObject名またはパス（空の場合は全画面）")]
        public string targetUiPath;

        [Tooltip("インスペクターで直接RectTransformを割り当てる場合（任意）")]
        public RectTransform targetRect;

        [Tooltip("プレイヤーの進行操作タイプ")]
        public GuideActionType actionType = GuideActionType.WaitNextButton;

        [Tooltip("このステップで開くべき画面（None以外ならコントローラーが自動で開きます）")]
        public GuideScreenTarget openScreen = GuideScreenTarget.None;

        [Tooltip("このステップを抜ける際、開いていた画面を自動で閉じるか")]
        public bool closeScreenOnExit = false;

        [Tooltip("スポットライトの余白（パディング・幅と高さのサイズ調整）")]
        public Vector2 padding = new Vector2(16f, 16f);

        [Tooltip("スポットライト位置の手動微調整オフセット（X, Y ピクセル）")]
        public Vector2 positionOffset = Vector2.zero;

        [Tooltip("ボタン操作を待つ場合の補足ヒント文")]
        public string buttonPromptHint = "↑ ボタンを押してみましょう！";
    }
}

