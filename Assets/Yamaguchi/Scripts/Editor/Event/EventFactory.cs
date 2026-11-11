using System.Collections.Generic;
using Maruyama.Core;
using Maruyama.Events;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// イベントの新規作成（テンプレ）と複製。保存先は Assets/Maruyama/Resources/Events/（EventManager が読み込む場所）。
/// ファイル名を決めるダイアログのあと、ID をファイル名から自動で付ける。
/// </summary>
public static class EventFactory
{
    public enum Template
    {
        Empty,          // 空
        RandomMerit,    // 抽選のメリット
        RandomDemerit,  // 抽選のデメリット
        Fixed,          // 決まった日（1回・選択肢1つ「わかった」）
        FollowUp,       // 続き（出方＝続き、選択肢1つ）
        OnCondition,    // 条件を満たしたら（出方＝条件、一生に1回）
        BranchIntro,    // 分岐の入口（2択）（抽選・一生に1回、続きを予約）
    }

    public static readonly string[] TemplateNames =
    {
        "空",
        "抽選のメリット",
        "抽選のデメリット",
        "決まった日（1回・選択肢1つ）",
        "続き（選択肢1つ）",
        "条件を満たしたら",
        "分岐の入口（2択・続きを予約）",
    };

    // ファイル名の初期値（ダイアログに出す）
    private static readonly string[] DefaultFileNames =
    {
        "Event_New",
        "Event_Merit_New",
        "Event_Demerit_New",
        "Event_Fixed_New",
        "Event_FollowUp_New",
        "Event_OnCondition_New",
        "Event_Branch_New",
    };

    // ─── 新しく作る ──────────────────────────────────────────────
    public static EventData Create(Template template)
    {
        string path = AskPath("イベントを新しく作る", DefaultFileNames[(int)template], EventRegistry.DefaultDir);
        if (path == null) return null;

        var e = ScriptableObject.CreateInstance<EventData>();
        e.id = EventRegistry.MakeId(System.IO.Path.GetFileNameWithoutExtension(path), null);

        switch (template)
        {
            case Template.RandomMerit:
                e.category = EventCategory.Merit;
                e.trigger = EventTrigger.Random;
                e.choices = EmptyChoices(2);
                break;
            case Template.RandomDemerit:
                e.category = EventCategory.Demerit;
                e.trigger = EventTrigger.Random;
                e.choices = EmptyChoices(2);
                break;
            case Template.Fixed:
                e.trigger = EventTrigger.Fixed;
                e.fixedMinAgeYears = 0;
                e.fixedMaxAgeYears = 0; // 同じ値 = 1回だけ
                e.choices = new List<EventChoice> { NewChoice("わかった") };
                break;
            case Template.FollowUp:
                e.trigger = EventTrigger.FollowUp;
                e.choices = new List<EventChoice> { NewChoice("わかった") };
                break;
            case Template.OnCondition:
                e.trigger = EventTrigger.OnCondition;
                e.choices = EmptyChoices(1);
                break;
            case Template.BranchIntro:
                e.trigger = EventTrigger.Random;
                e.choices = new List<EventChoice>
                {
                    new EventChoice
                    {
                        id = "c1",
                        label = "選択肢A",
                        effects = new List<Effect>
                        {
                            new Effect { kind = EffectKind.ScheduleEvent, target = EffectTarget.Household, days = 180 }
                        }
                    },
                    new EventChoice
                    {
                        id = "c2",
                        label = "選択肢B",
                        effects = new List<Effect>
                        {
                            new Effect { kind = EffectKind.ScheduleEvent, target = EffectTarget.Household, days = 180 }
                        }
                    }
                };
                break;
        }

        AssetDatabase.CreateAsset(e, path);

        // 一生に1回条件（OnCondition と BranchIntro）をサブアセットとして作成・登録
        if (template == Template.OnCondition || template == Template.BranchIntro)
        {
            var cond = ScriptableObject.CreateInstance<ChoiceHistoryCondition>();
            cond.name = "Cond_OnceInLifetime";
            cond.targetEvent = e;
            cond.checkType = HistoryCheckType.起きていない;
            AssetDatabase.AddObjectToAsset(cond, e);
            e.conditions.Add(cond);
            EditorUtility.SetDirty(e);
        }

        AssetDatabase.SaveAssets();
        EventRegistry.Invalidate();
        return AssetDatabase.LoadAssetAtPath<EventData>(path);
    }

    private static List<EventChoice> EmptyChoices(int count)
    {
        var list = new List<EventChoice>();
        for (int i = 0; i < count; i++) list.Add(NewChoice("", $"c{i + 1}"));
        return list;
    }

    private static EventChoice NewChoice(string label, string id = "c1")
    {
        return new EventChoice { id = id, label = label, effects = new List<Effect>() };
    }

    // ─── 複製して作る ────────────────────────────────────────────
    /// <summary>今のイベントをコピーする。ID は新しいファイル名から付け直し、サブアセットの条件もコピー先の中に入れ直す。</summary>
    public static EventData Duplicate(EventData source)
    {
        if (source == null) return null;

        string srcPath = AssetDatabase.GetAssetPath(source);
        string dir = System.IO.Path.GetDirectoryName(srcPath).Replace('\\', '/');
        string dstPath = AskPath("イベントを複製して作る", source.name + "_Copy", dir);
        if (dstPath == null) return null;

        // 同名があっても上書きしない
        dstPath = AssetDatabase.GenerateUniqueAssetPath(dstPath);
        if (!AssetDatabase.CopyAsset(srcPath, dstPath))
        {
            EditorUtility.DisplayDialog("イベント", "複製に失敗しました。", "OK");
            return null;
        }

        var copy = AssetDatabase.LoadAssetAtPath<EventData>(dstPath);
        if (copy == null) return null;

        // コピー元のサブアセットを指したままにしない（CopyAsset はサブアセットごと写すので、通常は残らない。念のため確認する）
        var so = new SerializedObject(copy);
        var list = so.FindProperty("conditions");
        bool fixedAny = false;
        for (int i = 0; i < list.arraySize; i++)
        {
            var el = list.GetArrayElementAtIndex(i);
            var cond = el.objectReferenceValue as EventConditionSO;
            if (cond != null && AssetDatabase.IsSubAsset(cond) && AssetDatabase.GetAssetPath(cond) == srcPath)
            {
                var clone = Object.Instantiate(cond);
                clone.name = cond.name;
                AssetDatabase.AddObjectToAsset(clone, copy);
                el.objectReferenceValue = clone;
                fixedAny = true;
            }
        }

        so.FindProperty("id").stringValue = EventRegistry.MakeId(System.IO.Path.GetFileNameWithoutExtension(dstPath), copy);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(copy);
        AssetDatabase.SaveAssets();
        if (fixedAny) Debug.Log("[EventFactory] 複製のとき、コピー元の条件サブアセットを指していたのでコピー先に入れ直しました。");

        EventRegistry.Invalidate();
        return copy;
    }

    // ─── ファイル名のダイアログ ──────────────────────────────────
    private static string AskPath(string title, string defaultName, string dir)
    {
        SupportEditorGUI.EnsureFolder(dir);
        string path = EditorUtility.SaveFilePanelInProject(title, defaultName, "asset", "イベントのファイル名を決めてください（Event_○○ の形）", dir);
        return string.IsNullOrEmpty(path) ? null : path;
    }
}

