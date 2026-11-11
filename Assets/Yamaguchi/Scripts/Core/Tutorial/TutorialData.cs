using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// チュートリアル1本ぶんのデータ（ページのリスト）。
/// 右クリック「作成 → 子育て → チュートリアル」か、メニュー「子育て → チュートリアル → 編集ウィンドウ」で作る。
/// 新しいチュートリアルを足すときは、このアセットを1つ作ってページと「出すタイミング」を決めるだけ（スクリプトは触らない）。
/// </summary>
[CreateAssetMenu(fileName = "Tutorial_New", menuName = "子育て/チュートリアル")]
public class TutorialData : ScriptableObject
{
    [Tooltip("プログラム用のID。「もう見た」をセーブするときのキーになるので、後から変えない。空ならアセット名を使う")]
    [SerializeField] private string id;

    [Tooltip("いつ出すか。TutorialScheduler に登録されていると、この条件を満たした日に1回だけ出る")]
    [SerializeField] private TutorialTiming timing = new TutorialTiming();

    [Tooltip("ページ。左から順に表示される（スワイプ・矢印で次へ）")]
    [SerializeField] private List<TutorialPage> pages = new List<TutorialPage>();

    public string Id => string.IsNullOrEmpty(id) ? name : id;
    public int PageCount => pages.Count;
    public IReadOnlyList<TutorialPage> Pages => pages;
    public TutorialTiming Timing => timing;

    /// 範囲外なら null。
    public TutorialPage GetPage(int index) => (index >= 0 && index < pages.Count) ? pages[index] : null;
}
