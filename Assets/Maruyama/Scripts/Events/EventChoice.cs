using System;
using System.Collections.Generic;
using Maruyama.Core;

namespace Maruyama.Events
{
    [Serializable]
    public class EventChoice
    {
        public string label;                 // ボタン表示ラベル（例: "病院に行く"）
        public List<Effect> effects;         // この選択肢を押した時に適用される Effect リスト
        public ExpenseCategory ledgerCategory = ExpenseCategory.その他;   // お金が動くとき、家計簿に載せる種類（病院なら 医療）
        public bool insuredMedicalExpense;   // Money の負の効果を保険診療の自己負担として助成対象にする
    }
}
