using Maruyama.Events;
using UnityEngine;

public enum HistoryCheckType
{
    起きた,
    起きていない,
    選んだ,
    選ばなかった
}

/// <summary>
/// 過去のイベントの発生や選択肢の選択履歴を条件とする部品。
/// </summary>
public class ChoiceHistoryCondition : EventConditionSO, IDescribable
{
    [Tooltip("対象のイベント")]
    public EventData targetEvent;

    [Tooltip("判定する履歴の種類")]
    public HistoryCheckType checkType = HistoryCheckType.起きた;

    [Tooltip("対象の選択肢ID（選んだ／選ばなかったで使用）")]
    public string choiceId = "";

    public override bool IsSatisfied()
    {
        if (targetEvent == null) return false;
        if (StoryState.I == null) return true;

        string eventId = targetEvent.id;
        bool occurred = StoryState.I.HasOccurred(eventId);

        switch (checkType)
        {
            case HistoryCheckType.起きた:
                return occurred;

            case HistoryCheckType.起きていない:
                return !occurred;

            case HistoryCheckType.選んだ:
                if (!occurred) return false;
                return StoryState.I.ChoiceOf(eventId) == choiceId;

            case HistoryCheckType.選ばなかった:
                // 「起きていて、別の選択肢を選んだ」とする（起きていないときは false）
                if (!occurred) return false;
                return StoryState.I.ChoiceOf(eventId) != choiceId;

            default:
                return true;
        }
    }

    public string Describe()
    {
        if (targetEvent == null) return "（イベント未設定）";
        string eventTitle = string.IsNullOrEmpty(targetEvent.title) ? targetEvent.name : targetEvent.title;

        switch (checkType)
        {
            case HistoryCheckType.起きた:
                return $"「{eventTitle}」が起きた";

            case HistoryCheckType.起きていない:
                return $"「{eventTitle}」が起きていない";

            case HistoryCheckType.選んだ:
                return $"「{eventTitle}」で「{GetChoiceLabel()}」を選んだ";

            case HistoryCheckType.選ばなかった:
                return $"「{eventTitle}」で「{GetChoiceLabel()}」以外を選んだ（未発生時は満たさない）";

            default:
                return $"「{eventTitle}」の履歴";
        }
    }

    private string GetChoiceLabel()
    {
        if (targetEvent == null || targetEvent.choices == null) return choiceId;
        var c = targetEvent.choices.Find(x => x != null && x.id == choiceId);
        if (c != null && !string.IsNullOrEmpty(c.label)) return c.label;
        return string.IsNullOrEmpty(choiceId) ? "（未選択）" : choiceId;
    }
}

