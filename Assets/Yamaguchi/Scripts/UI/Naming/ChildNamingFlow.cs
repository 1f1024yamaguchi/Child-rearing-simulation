using UnityEngine;
using Kusao.TutorialGuide;

/// <summary>
/// 子どもの名前を決める流れを担当する。GameManager に付ける。
/// 時間は止めない（ChildNamePanelUI が OnEnable / OnDisable で止める）。
/// </summary>
public class ChildNamingFlow : MonoBehaviour
{
    [Tooltip("名前を決めるパネルの UI")]
    [SerializeField] private ChildNamePanelUI panel;

    // 二重呼び出し防止フラグ
    private bool isRunning = false;
    private bool isWaitingForTutorial = false;

    /// <summary>
    /// シーンに接続が無くても、実行時に名前パネルを生成する。
    /// </summary>
    public void EnsurePanelRuntime()
    {
        if (panel != null) return;
        panel = FindFirstObjectByType<ChildNamePanelUI>(FindObjectsInactive.Include);
        if (panel == null)
        {
            var panelObject = new GameObject("ChildNamePanel_Runtime", typeof(RectTransform));
            panelObject.transform.SetParent(transform, false);
            panel = panelObject.AddComponent<ChildNamePanelUI>();
        }
        panel.EnsureRuntimeUI();
    }

    /// <summary>
    /// 名前決めの流れを開始する。GameStartup から NewGame のときだけ呼ぶ。
    /// 二重に呼ばれても1回しか動かない。
    /// </summary>
    public void Begin()
    {
        if (isRunning)
        {
            Debug.LogWarning("[ChildNamingFlow] すでに実行中です。二重呼び出しを無視します。");
            return;
        }

        // Household / Child が無い場合は何もしない
        if (Household.I == null || Household.I.Child == null)
        {
            Debug.LogWarning("[ChildNamingFlow] Household.I または Child が見つかりません。名前画面を開きません。");
            return;
        }

        if (panel == null)
        {
            Debug.LogWarning("[ChildNamingFlow] ChildNamePanelUI が設定されていません。名前は「みらい」のまま始めます。");
            return;
        }

        isRunning = true;

        // パネルを開く。決まったら SetDisplayName → パネルを閉じる（パネル自身が時間を戻す）
        panel.Open(name =>
        {
            Household.I.Child.SetDisplayName(name);
            isRunning = false;
        });
    }

    /// <summary>
    /// 開始直後のスポットライト型チュートリアルが終了してから名前画面を開く。
    /// チュートリアルが無い場合、または既に完了済みの場合はすぐ開く。
    /// </summary>
    public void BeginAfterTutorial()
    {
        if (isRunning)
        {
            Debug.LogWarning("[ChildNamingFlow] すでに実行中です。二重呼び出しを無視します。");
            return;
        }
        if (isWaitingForTutorial)
        {
            Debug.LogWarning("[ChildNamingFlow] チュートリアル終了を待っています。二重呼び出しを無視します。");
            return;
        }

        var guide = FindFirstObjectByType<TutorialGuideController>(FindObjectsInactive.Include);
        if (guide == null || TutorialGuideController.HasCompletedInSession)
        {
            Begin();
            return;
        }

        isWaitingForTutorial = true;
        TutorialGuideController.OnGuideCompleted += HandleGuideCompleted;
    }

    private void OnDisable()
    {
        TutorialGuideController.OnGuideCompleted -= HandleGuideCompleted;
    }

    private void OnDestroy()
    {
        TutorialGuideController.OnGuideCompleted -= HandleGuideCompleted;
    }

    private void HandleGuideCompleted()
    {
        if (!isWaitingForTutorial) return;

        isWaitingForTutorial = false;
        TutorialGuideController.OnGuideCompleted -= HandleGuideCompleted;
        Begin();
    }
}
