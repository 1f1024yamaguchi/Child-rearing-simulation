using UnityEngine;

/// <summary>
/// デバッグ用の「全部オート」。ON の間、判断画面をプレイヤーの代わりに処理し、支援制度も自動で申請する。
/// 判断画面の処理は DebugDialogResolver、支援制度は DebugSupportAutoApplier に任せる。
/// シーンには置かない。メニュー「子育て → デバッグツール」から作られる（DebugTools.Ensure）。
/// </summary>
[DefaultExecutionOrder(-50)]   // DebugTimeJump より先に動き、閉じた画面があればそのフレームは日を進めさせない
public class DebugAutoPilot : MonoBehaviour
{
    public static DebugAutoPilot I { get; private set; }

    [Header("オート")]
    [Tooltip("ON の間、判断画面（食事・イベント・税金・赤字警告・ミニゲーム）を自動で処理する")]
    [SerializeField] private bool autoEnabled = false;
    [Tooltip("イベントの選択肢の選び方")]
    [SerializeField] private DebugDialogResolver.ChoicePolicy eventChoice = DebugDialogResolver.ChoicePolicy.最初の選択肢;
    [Tooltip("画面が開いてから処理するまでの待ち（実時間・秒）。ジャンプ中は 0 になる")]
    [SerializeField] private float waitSeconds = 0.3f;

    [System.Serializable]
    public class FixedChoiceEntry
    {
        public string eventId;
        public string choiceId;
    }

    [Header("選択肢の固定（イベントID → 選択肢ID）")]
    [SerializeField] private System.Collections.Generic.List<FixedChoiceEntry> fixedChoices = new System.Collections.Generic.List<FixedChoiceEntry>();

    [Header("支援制度")]
    [Tooltip("申請できる制度を自動で申請し、毎年の手続き・再開も自動で行う")]
    [SerializeField] private bool autoApplySupports = true;
    [Tooltip("育休・時短など、親の働き方が変わる制度も申請する")]
    [SerializeField] private bool includeWorkModeSchemes = false;

    private readonly DebugDialogResolver resolver = new DebugDialogResolver();
    private int lastSupportDay = -1;

    public bool AutoEnabled { get => autoEnabled; set => autoEnabled = value; }
    public DebugDialogResolver.ChoicePolicy EventChoice { get => eventChoice; set => eventChoice = value; }
    public bool AutoApplySupports { get => autoApplySupports; set => autoApplySupports = value; }
    public bool IncludeWorkModeSchemes { get => includeWorkModeSchemes; set => includeWorkModeSchemes = value; }

    public System.Collections.Generic.IReadOnlyList<FixedChoiceEntry> FixedChoices => fixedChoices;

    public void SetFixedChoice(string eventId, string choiceId)
    {
        if (string.IsNullOrEmpty(eventId)) return;
        var existing = fixedChoices.Find(e => e.eventId == eventId);
        if (existing != null)
        {
            existing.choiceId = choiceId;
        }
        else
        {
            fixedChoices.Add(new FixedChoiceEntry { eventId = eventId, choiceId = choiceId });
        }
    }

    public void ClearFixedChoice(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return;
        fixedChoices.RemoveAll(e => e.eventId == eventId);
    }

    public void ClearAllFixedChoices()
    {
        fixedChoices.Clear();
    }

    public string GetFixedChoice(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return null;
        var entry = fixedChoices.Find(e => e.eventId == eventId);
        return entry?.choiceId;
    }

    /// ジャンプ中は、オートの ON/OFF に関係なく判断画面を処理する（DebugTimeJump が立てる）。
    public bool ForcedByJump { get; set; }
    public bool IsActive => autoEnabled || ForcedByJump;

    /// 最後に判断画面を処理したフレーム。同じフレームでは日を進めない。
    public int LastResolvedFrame { get; private set; } = -1;
    public int ResolvedCount { get; private set; }
    public int SupportActionCount { get; private set; }

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(this);
            return;
        }
        I = this;
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
    }

    private void Update()
    {
        if (!IsActive) return;

        resolver.EventChoice = eventChoice;
        resolver.WaitSeconds = ForcedByJump ? 0f : waitSeconds;
        if (resolver.ResolveAll())
        {
            LastResolvedFrame = Time.frameCount;
            ResolvedCount++;
        }

        RunSupportsOncePerDay();
    }

    private void RunSupportsOncePerDay()
    {
        if (!autoApplySupports || GameState.I == null) return;
        int today = Mathf.FloorToInt(GameState.I.AgeDays);
        if (today == lastSupportDay) return;
        lastSupportDay = today;
        SupportActionCount += DebugSupportAutoApplier.Run(includeWorkModeSchemes);
    }
}
