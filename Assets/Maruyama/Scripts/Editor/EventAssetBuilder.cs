#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Maruyama.Core;
using Maruyama.Events;

namespace Maruyama.Editor
{
    /// <summary>
    /// 0〜5歳のイベントと、その発生条件のアセットをまとめて作る。
    /// メニュー：ChildRearing / イベント / 0〜5歳のイベントを作る（抽選で起きるもの）
    ///           ChildRearing / イベント / 0〜5歳の固定イベントを作る（決まった日付に起きるもの）
    ///
    /// - すでに同じパスにアセットがあれば上書きしない（Inspector で調整した値を守る）。
    /// - 文面・効果・重みはすべてたたき台（docs/design/events.md 6章）。
    /// - イベントは Resources/Events に置く。EventManager が自動で読み込む。
    /// - 既存の「風邪」「一時預かり保育」には、条件が空のときだけ 0〜5歳の条件を付ける。
    /// </summary>
    public static class EventAssetBuilder
    {
        private const string EventFolder = "Assets/Maruyama/Resources/Events";
        private const string ConditionFolder = "Assets/Maruyama/Data/Conditions";

        [MenuItem("ChildRearing/イベント/0〜5歳のイベントを作る")]
        public static void BuildAgeZeroToFive()
        {
            EnsureFolder("Assets/Maruyama", "Resources");
            EnsureFolder("Assets/Maruyama/Resources", "Events");
            EnsureFolder("Assets/Maruyama", "Data");
            EnsureFolder("Assets/Maruyama/Data", "Conditions");

            // ---- 発生条件 ----
            var age0to5 = Age("Cond_Age_0to5", 0, 5);
            var age0to3 = Age("Cond_Age_0to3", 0, 3);
            var age0to2 = Age("Cond_Age_0to2", 0, 2);
            var age1to5 = Age("Cond_Age_1to5", 1, 5);
            var age1to3 = Age("Cond_Age_1to3", 1, 3);
            var age2to5 = Age("Cond_Age_2to5", 2, 5);
            var parentsStressed = Stress("Cond_Stress_ParentsDanger", StressSource.ParentsAverage, StressLevel.危険, StressLevel.限界);

            // ---- 既存イベントに年齢条件を付ける（条件が空のときだけ） ----
            AttachIfEmpty("Assets/Maruyama/Data/Events/Event_Demerit_ChildSick.asset", age0to5);
            AttachIfEmpty("Assets/Maruyama/Data/Events/Event_Merit_Daycare.asset", age0to5);

            // ---- デメリット ----
            Create("Event_Demerit_Injury", Event("demerit_injury", EventCategory.Demerit,
                "みらいが転んでケガをした", "公園で転んで、膝をすりむいた。血が出ている。",
                1f, new EventConditionSO[] { age1to5 },
                Choice("病院に行く", ExpenseCategory.医療, true,
                    Fx(EffectKind.Money, EffectTarget.Household, -5000)),
                Choice("家で手当てする", ExpenseCategory.医療, false,
                    Fx(EffectKind.Health, EffectTarget.Child, -8),
                    Fx(EffectKind.Stress, EffectTarget.Child, 5))));

            Create("Event_Demerit_NightCrying", Event("demerit_night_crying", EventCategory.Demerit,
                "夜泣きが続いている", "ここ数日、夜中に何度も起こされる。寝不足がたまってきた。",
                1.5f, new EventConditionSO[] { age0to2 },
                Choice("夫婦で交代であやす", ExpenseCategory.その他, false,
                    Fx(EffectKind.Stress, EffectTarget.Parents, 3),
                    Fx(EffectKind.Stress, EffectTarget.Child, -2)),
                Choice("母がひとりで対応する", ExpenseCategory.その他, false,
                    Fx(EffectKind.Stress, EffectTarget.Mother, 10))));

            Create("Event_Demerit_IyaIya", Event("demerit_iyaiya", EventCategory.Demerit,
                "イヤイヤ期が始まった", "何を言っても「イヤ！」。着替えも食事も一苦労。",
                1f, new EventConditionSO[] { age1to3 },
                Choice("気持ちに付き合う", ExpenseCategory.その他, false,
                    Fx(EffectKind.Stress, EffectTarget.Parents, 5),
                    Fx(EffectKind.Stress, EffectTarget.Child, -3)),
                Choice("ピシャリと叱る", ExpenseCategory.その他, false,
                    Fx(EffectKind.Stress, EffectTarget.Child, 8),
                    Fx(EffectKind.Stress, EffectTarget.Parents, 2))));

            Create("Event_Demerit_SuddenExpense", Cooldown(730, Event("demerit_sudden_expense", EventCategory.Demerit,
                "洗濯機が壊れた", "毎日使うものが、突然動かなくなった。",
                0.25f, new EventConditionSO[] { age0to5 },
                Choice("新しく買い替える", ExpenseCategory.その他, false,
                    Fx(EffectKind.Money, EffectTarget.Household, -40000)),
                Choice("修理して、しばらく様子を見る", ExpenseCategory.その他, false,
                    Fx(EffectKind.Money, EffectTarget.Household, -12000),
                    Fx(EffectKind.Stress, EffectTarget.Parents, 4)))));

            // ---- メリット ----
            Create("Event_Merit_SupportCenter", Event("merit_support_center", EventCategory.Merit,
                "子育て支援センターに行ってみた", "同じ年ごろの子の親と話せて、相談にも乗ってもらえた。",
                1f, new EventConditionSO[] { age0to5 },
                Choice("参加する", ExpenseCategory.その他, false,
                    Fx(EffectKind.Stress, EffectTarget.Parents, -8),
                    Fx(EffectKind.Stress, EffectTarget.Child, -2)),
                Choice("今回は見送る", ExpenseCategory.その他, false)));

            Create("Event_Merit_Playground", Event("merit_playground", EventCategory.Merit,
                "公園でお友達ができた", "同じ年ごろの子と、すっかり仲良くなった。",
                1f, new EventConditionSO[] { age2to5 },
                Choice("たくさん遊ばせる", ExpenseCategory.その他, false,
                    Fx(EffectKind.Stress, EffectTarget.Child, -8),
                    Fx(EffectKind.Stress, EffectTarget.Parents, 2)),
                Choice("今日は早めに帰る", ExpenseCategory.その他, false,
                    Fx(EffectKind.Stress, EffectTarget.Child, 3))));

            Create("Event_Merit_HandMeDown", Event("merit_hand_me_down", EventCategory.Merit,
                "ご近所からおさがりをもらった", "まだきれいなベビー服やおもちゃがたくさん。",
                1f, new EventConditionSO[] { age0to3 },
                Choice("ありがたくいただく", ExpenseCategory.その他, false,
                    Fx(EffectKind.Stress, EffectTarget.Parents, -4)),
                Choice("気持ちだけいただく", ExpenseCategory.その他, false)));

            Create("Event_Merit_FamilyOuting", Event("merit_family_outing", EventCategory.Merit,
                "家族でお出かけ", "休日に、近くの公園でお弁当。みんなで過ごすと気持ちが軽くなる。",
                1f, new EventConditionSO[] { age0to5 },
                Choice("お弁当を持って出かける", ExpenseCategory.その他, false,
                    Fx(EffectKind.Money, EffectTarget.Household, -3000),
                    Fx(EffectKind.Stress, EffectTarget.Everyone, -6)),
                Choice("家でゆっくり過ごす", ExpenseCategory.その他, false,
                    Fx(EffectKind.Stress, EffectTarget.Parents, -2))));

            // 親のストレスが高いときだけ出る救済イベント（raw の「親子で相談」を0〜5歳向けにしたもの）
            Create("Event_Merit_CoupleTalk", Event("merit_couple_talk", EventCategory.Merit,
                "夫婦でじっくり話し合った", "お互いの大変さを話したら、少し気持ちが軽くなった。",
                3f, new EventConditionSO[] { age0to5, parentsStressed },
                Choice("家事の分担を見直す", ExpenseCategory.その他, false,
                    Fx(EffectKind.Stress, EffectTarget.Parents, -12)),
                Choice("実家に手伝いに来てもらう", ExpenseCategory.その他, false,
                    Fx(EffectKind.Stress, EffectTarget.Parents, -8),
                    Fx(EffectKind.Stress, EffectTarget.Child, -4))));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EventAssetBuilder] 0〜5歳のイベントを作りました（既存のアセットは上書きしていません）。");
        }

        [MenuItem("ChildRearing/イベント/0〜5歳の固定イベントを作る")]
        public static void BuildFixedAgeZeroToFive()
        {
            EnsureFolder("Assets/Maruyama", "Resources");
            EnsureFolder("Assets/Maruyama/Resources", "Events");

            // 暦は 0歳1月1日が誕生日（年齢は毎年1月1日に上がる）。→ GameDate
            // 日付はすべてたたき台（docs/design/events.md 7章）。同じ日に重ならないように決めている。

            Create("Event_Fixed_FirstStand", Fixed("fixed_first_stand", EventCategory.Merit,
                "つかまり立ちができた", "ひとりで立ち上がった。昨日まではできなかったのに。",
                10, 15, 0, 0,
                Choice("拍手して喜ぶ", ExpenseCategory.その他, false,
                    Fx(EffectKind.Stress, EffectTarget.Parents, -8))));

            Create("Event_Fixed_FirstWord", Fixed("fixed_first_word", EventCategory.Merit,
                "初めての言葉", "「ママ」って言った！ 聞き間違いじゃない、たしかに言った。",
                3, 10, 1, 1,
                Choice("胸がいっぱいになる", ExpenseCategory.その他, false,
                    Fx(EffectKind.Stress, EffectTarget.Parents, -8))));

            Create("Event_Fixed_Birthday", Fixed("fixed_birthday", EventCategory.Merit,
                "みらいの誕生日", "また一つ大きくなった。どんなお祝いにしよう？",
                1, 1, 1, 5,
                Choice("ケーキとプレゼントでお祝い", ExpenseCategory.その他, false,
                    Fx(EffectKind.Money, EffectTarget.Household, -5000),
                    Fx(EffectKind.Stress, EffectTarget.Everyone, -8)),
                Choice("家で普通にお祝い", ExpenseCategory.その他, false,
                    Fx(EffectKind.Stress, EffectTarget.Everyone, -3))));

            Create("Event_Fixed_Otoshidama", Fixed("fixed_otoshidama", EventCategory.Merit,
                "お年玉をもらった", "親戚から、みらいにお年玉。どう使おう？",
                1, 3, 1, 5,
                Choice("全部貯金する", ExpenseCategory.その他, false,
                    Fx(EffectKind.Money, EffectTarget.Household, 10000),
                    Fx(EffectKind.Stress, EffectTarget.Child, 8)),
                Choice("子どもに全部渡す", ExpenseCategory.その他, false,
                    Fx(EffectKind.Stress, EffectTarget.Child, -12))));

            Create("Event_Fixed_Recital", Fixed("fixed_recital", EventCategory.Merit,
                "お遊戯会の日", "練習の成果を見せる日。ステージの上で、みらいが堂々と踊っている。",
                12, 15, 3, 5,
                Choice("客席から見守る", ExpenseCategory.その他, false,
                    Fx(EffectKind.Stress, EffectTarget.Parents, -6),
                    Fx(EffectKind.Stress, EffectTarget.Child, -4))));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EventAssetBuilder] 0〜5歳の固定イベントを作りました（既存のアセットは上書きしていません）。");
        }

        // ---- 部品 ----

        private static AgeRangeCondition Age(string name, int min, int max)
        {
            var path = $"{ConditionFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AgeRangeCondition>(path);
            if (existing != null) return existing;

            var c = ScriptableObject.CreateInstance<AgeRangeCondition>();
            c.minAgeYears = min;
            c.maxAgeYears = max;
            AssetDatabase.CreateAsset(c, path);
            return c;
        }

        private static StressLevelCondition Stress(string name, StressSource source, StressLevel min, StressLevel max)
        {
            var path = $"{ConditionFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<StressLevelCondition>(path);
            if (existing != null) return existing;

            var c = ScriptableObject.CreateInstance<StressLevelCondition>();
            c.source = source;
            c.minLevel = min;
            c.maxLevel = max;
            AssetDatabase.CreateAsset(c, path);
            return c;
        }

        private static void AttachIfEmpty(string eventPath, EventConditionSO condition)
        {
            var e = AssetDatabase.LoadAssetAtPath<EventData>(eventPath);
            if (e == null)
            {
                Debug.LogWarning($"[EventAssetBuilder] {eventPath} が見つかりません。条件は付けませんでした。");
                return;
            }
            if (e.conditions != null && e.conditions.Count > 0) return;

            e.conditions = new List<EventConditionSO> { condition };
            EditorUtility.SetDirty(e);
        }

        private static EventData Event(string id, EventCategory category, string title, string description,
            float weight, EventConditionSO[] conditions, params EventChoice[] choices)
        {
            var e = ScriptableObject.CreateInstance<EventData>();
            e.id = id;
            e.category = category;
            e.title = title;
            e.description = description;
            e.weight = weight;
            e.conditions = new List<EventConditionSO>(conditions);
            e.choices = new List<EventChoice>(choices);
            return e;
        }

        /// <summary>同じイベントが続けて出ないよう、出てから days 日あける（365日 = 1歳）。</summary>
        private static EventData Cooldown(int days, EventData e)
        {
            e.cooldownDays = days;
            return e;
        }

        /// <summary>固定イベント。month / day に、minAge〜maxAge 歳の毎年（同じ値なら1回だけ）起きる。</summary>
        private static EventData Fixed(string id, EventCategory category, string title, string description,
            int month, int day, int minAge, int maxAge, params EventChoice[] choices)
        {
            var e = Event(id, category, title, description, 1f, new EventConditionSO[0], choices);
            e.trigger = EventTrigger.Fixed;
            e.fixedMonth = month;
            e.fixedDay = day;
            e.fixedMinAgeYears = minAge;
            e.fixedMaxAgeYears = maxAge;
            return e;
        }

        private static EventChoice Choice(string label, ExpenseCategory ledger, bool insuredMedical, params Effect[] effects)
        {
            return new EventChoice
            {
                label = label,
                effects = new List<Effect>(effects),
                ledgerCategory = ledger,
                insuredMedicalExpense = insuredMedical,
            };
        }

        private static Effect Fx(EffectKind kind, EffectTarget target, float value)
        {
            return new Effect { kind = kind, target = target, value = value, duration = EffectDuration.Instant };
        }

        /// <summary>すでにあれば作らない。</summary>
        private static void Create(string assetName, EventData data)
        {
            var path = $"{EventFolder}/{assetName}.asset";
            if (AssetDatabase.LoadAssetAtPath<EventData>(path) != null)
            {
                Object.DestroyImmediate(data);
                return;
            }
            AssetDatabase.CreateAsset(data, path);
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
#endif
