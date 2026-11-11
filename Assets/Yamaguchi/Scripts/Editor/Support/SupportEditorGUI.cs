using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 支援制度の編集ウィンドウで使う、共通の入力部品（GameDay・円・月トグル・文字列リストなど）。
/// 値の書き換えはすべて SerializedProperty 経由（Undo とダーティが効く）。
/// </summary>
public static class SupportEditorGUI
{
    public const string DataDir = "Assets/Yamaguchi/Data/Supports";
    public const string RequirementDir = DataDir + "/Requirements";

    // ─── enum の日本語ラベル（enum の並び順と同じ） ─────────────────
    public static readonly string[] CategoryNames = { "政府（国）", "地方自治体", "会社・仕事", "アプリ・民間" };
    public static readonly string[] KindNames = { "お金", "ストレス", "体調", "成績", "趣味", "いいイベント率(%)", "悪いイベント率(%)", "特性を付ける", "状態を付ける" };
    public static readonly string[] TargetNames = { "世帯", "母親", "父親", "子ども", "両親", "家族全員" };
    public static readonly string[] DurationNames = { "その場で1回", "今月のあいだ", "日数のあいだ" };
    public static readonly string[] DeadlineNames = { "なし", "1回きり", "毎年" };
    public static readonly string[] CoverTypeNames = { "全額", "割合", "○円まで" };
    public static readonly string[] WorkModeNames = { "通常", "産休", "育休", "時短" };
    public static readonly string[] ParentRoleNames = { "母親", "父親" };
    public static readonly string[] WageTargetNames = { "働き方の対象の親", "両親" };
    public static readonly string[] WageBaseNames = { "休業前の給与", "今の給与" };

    private static readonly Dictionary<int, string[]> numberCache = new Dictionary<int, string[]>();
    private static readonly Dictionary<int, int[]> valueCache = new Dictionary<int, int[]>();

    private static string[] Numbers(int count)
    {
        if (!numberCache.TryGetValue(count, out var a))
        {
            a = new string[count];
            for (int i = 0; i < count; i++) a[i] = (i + 1).ToString();
            numberCache[count] = a;
        }
        return a;
    }

    private static int[] Values(int count)
    {
        if (!valueCache.TryGetValue(count, out var a))
        {
            a = new int[count];
            for (int i = 0; i < count; i++) a[i] = i + 1;
            valueCache[count] = a;
        }
        return a;
    }

    // ─── 枠 ──────────────────────────────────────────────────────
    /// <summary>見出し付きの枠。using で使う。</summary>
    public class BoxScope : GUI.Scope
    {
        public BoxScope(string title)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (!string.IsNullOrEmpty(title)) EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        protected override void CloseScope() => EditorGUILayout.EndVertical();
    }

    // ─── 日付 ────────────────────────────────────────────────────
    /// <summary>GameDay を「□歳 [月▼] [日▼]」で入力する。見た目の補正だけでは値を書き換えない。</summary>
    public static void DrawGameDay(SerializedProperty day, string label)
    {
        var y = day.FindPropertyRelative("year");
        var m = day.FindPropertyRelative("month");
        var d = day.FindPropertyRelative("day");
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(label);

            int rawYear = EditorGUILayout.IntField(y.intValue, GUILayout.Width(44));
            if (rawYear != y.intValue) y.intValue = Mathf.Clamp(rawYear, 0, 19);
            GUILayout.Label("歳", GUILayout.Width(18));

            int shownMonth = Mathf.Clamp(m.intValue, 1, 12);
            int newMonth = EditorGUILayout.IntPopup(shownMonth, Numbers(12), Values(12), GUILayout.Width(44));
            bool monthChanged = newMonth != shownMonth;
            if (monthChanged) m.intValue = newMonth;
            GUILayout.Label("月", GUILayout.Width(18));

            int month = Mathf.Clamp(m.intValue, 1, 12);
            int maxDay = GameDate.DaysInMonth(month);
            if (monthChanged && d.intValue > maxDay) d.intValue = maxDay;
            int shownDay = Mathf.Clamp(d.intValue, 1, maxDay);
            int newDay = EditorGUILayout.IntPopup(shownDay, Numbers(maxDay), Values(maxDay), GUILayout.Width(44));
            if (newDay != shownDay) d.intValue = newDay;
            GUILayout.Label("日", GUILayout.Width(18));

            GUILayout.FlexibleSpace();
        }
    }

    /// <summary>MonthDay（毎年の○月○日）を「[月▼] [日▼]」で入力する。</summary>
    public static void DrawMonthDay(SerializedProperty monthDay, string label)
    {
        var m = monthDay.FindPropertyRelative("month");
        var d = monthDay.FindPropertyRelative("day");
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(label);

            int shownMonth = Mathf.Clamp(m.intValue, 1, 12);
            int newMonth = EditorGUILayout.IntPopup(shownMonth, Numbers(12), Values(12), GUILayout.Width(44));
            bool monthChanged = newMonth != shownMonth;
            if (monthChanged) m.intValue = newMonth;
            GUILayout.Label("月", GUILayout.Width(18));

            int month = Mathf.Clamp(m.intValue, 1, 12);
            int maxDay = GameDate.DaysInMonth(month);
            if (monthChanged && d.intValue > maxDay) d.intValue = maxDay;
            int shownDay = Mathf.Clamp(d.intValue, 1, maxDay);
            int newDay = EditorGUILayout.IntPopup(shownDay, Numbers(maxDay), Values(maxDay), GUILayout.Width(44));
            if (newDay != shownDay) d.intValue = newDay;
            GUILayout.Label("日", GUILayout.Width(18));

            GUILayout.FlexibleSpace();
        }
    }

    /// <summary>GameDay に値を入れる。</summary>
    public static void SetDay(SerializedProperty day, int year, int month, int d)
    {
        day.FindPropertyRelative("year").intValue = year;
        day.FindPropertyRelative("month").intValue = month;
        day.FindPropertyRelative("day").intValue = d;
    }

    // ─── 数値 ────────────────────────────────────────────────────
    /// <summary>円の入力（int でも float でもよい）。</summary>
    public static void DrawYen(SerializedProperty p, string label)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (p.propertyType == SerializedPropertyType.Float)
            {
                int cur = Mathf.RoundToInt(p.floatValue);
                int n = EditorGUILayout.IntField(label, cur);
                if (n != cur) p.floatValue = n;
            }
            else
            {
                int n = EditorGUILayout.IntField(label, p.intValue);
                if (n != p.intValue) p.intValue = n;
            }
            GUILayout.Label("円", GUILayout.Width(18));
        }
    }

    /// <summary>0〜1 の値を 0〜100% のスライダーで入力する。</summary>
    public static void DrawPercent(SerializedProperty p, string label)
    {
        float pct = Mathf.Clamp01(p.floatValue) * 100f;
        float np = EditorGUILayout.Slider(label, pct, 0f, 100f);
        if (np != pct) p.floatValue = np / 100f;
    }

    /// <summary>「+15,000 円」のような表示。</summary>
    public static string Yen(float v) => v.ToString("+#,0;-#,0;0") + " 円";

    // ─── enum ────────────────────────────────────────────────────
    /// <summary>日本語ラベルの enum 入力。label が null ならラベルなし。</summary>
    public static void EnumPopup(SerializedProperty p, string label, string[] names, params GUILayoutOption[] options)
    {
        int cur = p.intValue;
        int n = label == null
            ? EditorGUILayout.Popup(cur, names, options)
            : EditorGUILayout.Popup(label, cur, names, options);
        if (n != cur) p.intValue = n;
    }

    // ─── 1〜12 月のトグル ────────────────────────────────────────
    /// <summary>1〜12月のトグル。keepOne が true なら最後の1つは外せない。</summary>
    public static void DrawMonthToggles(SerializedProperty list, bool keepOne)
    {
        var set = new HashSet<int>();
        for (int i = 0; i < list.arraySize; i++) set.Add(list.GetArrayElementAtIndex(i).intValue);

        bool changed = false;
        for (int row = 0; row < 2; row++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int k = 1; k <= 6; k++)
                {
                    int month = row * 6 + k;
                    bool on = set.Contains(month);
                    using (new EditorGUI.DisabledScope(keepOne && on && set.Count == 1))
                    {
                        bool now = GUILayout.Toggle(on, month + "月", EditorStyles.miniButton, GUILayout.Width(48));
                        if (now != on)
                        {
                            if (now) set.Add(month); else set.Remove(month);
                            changed = true;
                        }
                    }
                }
                GUILayout.FlexibleSpace();
            }
        }

        if (changed)
        {
            var sorted = new List<int>(set);
            sorted.Sort();
            list.arraySize = sorted.Count;
            for (int i = 0; i < sorted.Count; i++) list.GetArrayElementAtIndex(i).intValue = sorted[i];
        }
    }

    // ─── 文字列のリスト ──────────────────────────────────────────
    public static void DrawStringList(SerializedProperty list, string label)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        for (int i = 0; i < list.arraySize; i++)
        {
            bool remove = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(list.GetArrayElementAtIndex(i), GUIContent.none);
                remove = GUILayout.Button("✕", GUILayout.Width(24));
            }
            if (remove)
            {
                list.DeleteArrayElementAtIndex(i);
                break;
            }
        }
        if (GUILayout.Button("＋ 追加", GUILayout.Width(80)))
        {
            int n = list.arraySize;
            list.arraySize = n + 1;
            list.GetArrayElementAtIndex(n).stringValue = "";
        }
    }

    // ─── 並べ替え・削除ボタン ────────────────────────────────────
    /// <summary>↑↓削除のボタン（TutorialEditorWindow のページと同じ）。並べ替え・削除をしたら true。</summary>
    public static bool ArrayButtons(SerializedProperty array, int i, string what)
    {
        using (new EditorGUI.DisabledScope(i == 0))
            if (GUILayout.Button("↑", GUILayout.Width(26))) { array.MoveArrayElement(i, i - 1); return true; }
        using (new EditorGUI.DisabledScope(i == array.arraySize - 1))
            if (GUILayout.Button("↓", GUILayout.Width(26))) { array.MoveArrayElement(i, i + 1); return true; }
        if (GUILayout.Button("削除", GUILayout.Width(44)))
        {
            if (EditorUtility.DisplayDialog("支援制度", $"{what} {i + 1} を消しますか？", "消す", "やめる"))
            {
                array.DeleteArrayElementAtIndex(i);
                return true;
            }
        }
        return false;
    }

    // ─── アイコンのサムネイル ────────────────────────────────────
    /// <summary>TutorialEditorWindow のプレビューと同じ書き方。場所は毎回同じだけ取る（Layout と Repaint で数がずれないように）。</summary>
    public static void DrawThumbnail(Sprite sprite, EditorWindow repaintTarget)
    {
        var rect = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));
        var tex = AssetPreview.GetAssetPreview(sprite);
        if (tex != null) GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
        else if (repaintTarget != null) repaintTarget.Repaint();
    }

    // ─── フォルダ ────────────────────────────────────────────────
    /// <summary>Assets/... のフォルダが無ければ作る。</summary>
    public static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder)) return;
        string parent = System.IO.Path.GetDirectoryName(assetFolder).Replace('\\', '/');
        string name = System.IO.Path.GetFileName(assetFolder);
        if (!string.IsNullOrEmpty(parent) && parent != "Assets") EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
