using Maruyama.Core;
using UnityEditor;
using UnityEngine;

/// <summary>「使う項目」のON/OFFの状態。データには保存せず、中身が入っているかどうかで決める。</summary>
public struct SupportFeatures
{
    public bool tiers;     // 毎月の支給表 monthlyTiers
    public bool payout;    // 支給月 payoutMonths
    public bool covers;    // 支出の肩代わり covers
    public bool medical;   // 医療費の助成 medicalExpenseCovers
    public bool work;      // 働き方を変える changesWorkMode
    public bool wage;      // 給与に連動 wageLinkedTiers
}

/// <summary>使う項目のトグル。ONで空の1行を足し、OFFは確認してから中身を消す。OFFの欄は画面に出さない。</summary>
public static class SupportFeatureToggles
{
    public static SupportFeatures Read(SerializedObject so)
    {
        return new SupportFeatures
        {
            tiers = so.FindProperty("monthlyTiers").arraySize > 0,
            payout = so.FindProperty("payoutMonths").arraySize > 0,
            covers = so.FindProperty("covers").arraySize > 0,
            medical = so.FindProperty("medicalExpenseCovers").arraySize > 0,
            work = so.FindProperty("changesWorkMode").boolValue,
            wage = so.FindProperty("wageLinkedTiers").arraySize > 0,
        };
    }

    public static SupportFeatures Draw(SerializedObject so)
    {
        var f = Read(so);
        using (new SupportEditorGUI.BoxScope("使う項目（ONにした項目の欄だけが下に出ます）"))
        {
            ListToggle(so, "monthlyTiers", "毎月の支給表", f.tiers, "毎月いくらもらえるか（児童手当など）",
                (list, i) => SupportPaymentSection.AppendTier(list));
            ListToggle(so, "payoutMonths", "支給月", f.payout, "決まった月にまとめて支給する（空＝毎月）",
                (list, i) => list.GetArrayElementAtIndex(i).intValue = 1);
            ListToggle(so, "covers", "支出の肩代わり", f.covers, "保育料の無償化など、支出を減らす",
                (list, i) => SupportCoverSection.AppendCover(list, true));
            ListToggle(so, "medicalExpenseCovers", "医療費の助成", f.medical, "病気イベントの自己負担を助成する",
                (list, i) => SupportCoverSection.AppendCover(list, false));

            // 働き方を変える（bool）
            bool work = EditorGUILayout.ToggleLeft(new GUIContent("働き方を変える", "育休・時短など、親の働き方を変える"), f.work);
            if (work != f.work)
            {
                if (work || Confirm("働き方を変える"))
                    so.FindProperty("changesWorkMode").boolValue = work;
            }

            ListToggle(so, "wageLinkedTiers", "給与に連動", f.wage, "給与の何％を支給するか（育休給付金など）",
                (list, i) => SupportWorkSection.AppendWageTier(list));
        }
        return Read(so);
    }

    private static void ListToggle(SerializedObject so, string path, string label, bool on, string hint,
        System.Action<SerializedProperty, int> init)
    {
        bool now = EditorGUILayout.ToggleLeft(new GUIContent(label, hint), on);
        if (now == on) return;

        var list = so.FindProperty(path);
        if (now)
        {
            int i = list.arraySize;
            list.arraySize = i + 1;
            init(list, i);
        }
        else if (Confirm(label))
        {
            list.arraySize = 0;
        }
    }

    private static bool Confirm(string label) =>
        EditorUtility.DisplayDialog("使う項目", $"「{label}」をOFFにします。入っている内容は消えます。よろしいですか？", "消す", "やめる");
}
