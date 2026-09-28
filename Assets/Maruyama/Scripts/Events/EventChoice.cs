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
    }
}
