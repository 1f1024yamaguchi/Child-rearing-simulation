namespace Arisaka
{
    /// <summary>
    /// 突発的な家電故障等の特別支出設定（有坂担当）。
    /// 「洗濯機が壊れた」イベント（demerit_sudden_expense）の買い替え・修理費用などを一元管理する。
    /// </summary>
    public static class SuddenExpenseSettings
    {
        /// <summary>洗濯機を新しく買い替える場合の費用（円）</summary>
        public const int WashingMachineReplacementCost = 80000;

        /// <summary>洗濯機を修理する場合の費用（円）</summary>
        public const int WashingMachineRepairCost = 25000;

        /// <summary>洗濯機修理時の親ストレス増加量</summary>
        public const int WashingMachineRepairStress = 4;
    }
}

