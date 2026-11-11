namespace Maruyama.Events
{
    /// <summary>
    /// イベントで選択肢が選ばれたときに外へ渡す情報。値だけの入れ物。
    /// 振り返りの「できごとの記録」（LifeRecorder）やストーリー状態（StoryState）が受け取る。
    /// </summary>
    public readonly struct EventResolvedInfo
    {
        public readonly string eventId;
        public readonly string title;
        public readonly EventCategory category;
        public readonly bool isFixed;
        public readonly string choiceLabel;
        public readonly int choiceIndex;
        public readonly string choiceId;
        public readonly int money;
        public readonly int ageDays;

        public EventResolvedInfo(string eventId, string title, EventCategory category, bool isFixed,
                                 string choiceLabel, int choiceIndex, string choiceId, int money, int ageDays)
        {
            this.eventId = eventId;
            this.title = title;
            this.category = category;
            this.isFixed = isFixed;
            this.choiceLabel = choiceLabel;
            this.choiceIndex = choiceIndex;
            this.choiceId = choiceId;
            this.money = money;
            this.ageDays = ageDays;
        }
    }
}

