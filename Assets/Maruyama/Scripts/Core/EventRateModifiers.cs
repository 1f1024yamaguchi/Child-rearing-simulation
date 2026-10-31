using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maruyama.Core
{
    /// <summary>
    /// イベント発生率の補正（GoodEventRate / BadEventRate）の置き場。
    ///
    /// ■ 役割
    /// - EffectApplier が GoodEventRate / BadEventRate の効果を受け取ったら Add する。
    /// - EventManager が抽選のとき TotalPercent で「今日の補正（％ポイント）」を読む。
    ///
    /// ■ 期間の扱い（Effect.duration）
    /// - ThisMonth : 追加した日から、その月の最終日まで。
    /// - Days      : 追加した日から days 日間。
    /// - Instant   : 発生率には「その場」の意味がないため無視する（警告ログを出す）。
    ///
    /// ■ 対象（Effect.target）
    /// 発生率は世帯全体のものなので target は見ない。
    ///
    /// ■ セーブ
    /// EventManager がセーブに参加し、Export / Import でこの補正も保存・復元する。
    /// </summary>
    public static class EventRateModifiers
    {
        private class Entry
        {
            public EffectKind kind;
            public float value;          // ％ポイント（+5 なら +5%）
            public int startDay;         // 通算日数。この日から有効
            public int endDayExclusive;  // 通算日数。この日になったら無効
        }

        private static readonly List<Entry> entries = new List<Entry>();

        /// <summary>セーブ用の1件ぶん。</summary>
        [Serializable]
        public class SavedEntry
        {
            public int kind;      // EffectKind の数値
            public float value;
            public int startDay;
            public int endDay;    // この日になったら無効
        }

        /// <summary>今も有効な補正をセーブ用の形で返す。</summary>
        public static List<SavedEntry> Export()
        {
            int today = CurrentDay();
            var list = new List<SavedEntry>();
            foreach (var en in entries)
            {
                if (en.endDayExclusive <= today) continue;
                list.Add(new SavedEntry { kind = (int)en.kind, value = en.value, startDay = en.startDay, endDay = en.endDayExclusive });
            }
            return list;
        }

        /// <summary>セーブ用の形から、補正を丸ごと戻す（今ある補正は捨てる）。</summary>
        public static void Import(List<SavedEntry> saved)
        {
            entries.Clear();
            if (saved == null) return;
            foreach (var s in saved)
            {
                if (s == null) continue;
                var kind = (EffectKind)s.kind;
                if (kind != EffectKind.GoodEventRate && kind != EffectKind.BadEventRate) continue;
                entries.Add(new Entry { kind = kind, value = s.value, startDay = s.startDay, endDayExclusive = s.endDay });
            }
        }

        /// <summary>再生開始のたびに空にする（エディタのドメインリロード無効時の取り残し対策）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            entries.Clear();
        }

        /// <summary>補正を全部消す。</summary>
        public static void Clear()
        {
            entries.Clear();
        }

        /// <summary>
        /// GoodEventRate / BadEventRate の効果を登録する。それ以外の種類は無視する。
        /// </summary>
        public static void Add(Effect e)
        {
            if (e == null) return;
            if (e.kind != EffectKind.GoodEventRate && e.kind != EffectKind.BadEventRate) return;

            int today = CurrentDay();
            int end;

            switch (e.duration)
            {
                case EffectDuration.ThisMonth:
                    GameDate.FromAgeDays(today, out _, out int month, out int day);
                    // 今月1日 + 今月の日数 = 来月1日。来月1日になったら無効。
                    end = today - (day - 1) + GameDate.DaysInMonth(month);
                    break;

                case EffectDuration.Days:
                    if (e.days <= 0)
                    {
                        Debug.LogWarning($"[EventRateModifiers] {e.kind} の duration が Days なのに days={e.days} です。無視します。");
                        return;
                    }
                    end = today + e.days;
                    break;

                default: // Instant
                    Debug.LogWarning($"[EventRateModifiers] {e.kind} に Instant は使えません（発生率は期間のある効果です）。ThisMonth か Days を指定してください。無視します。");
                    return;
            }

            entries.Add(new Entry
            {
                kind = e.kind,
                value = e.value,
                startDay = today,
                endDayExclusive = end,
            });

            Debug.Log($"[EventRateModifiers] {e.kind} {e.value:+0.#;-0.#;0}% を登録（day{today}〜day{end - 1}）");
        }

        /// <summary>
        /// Effect 由来の補正だけの合計（％ポイント）。期限切れは取り除く。
        /// </summary>
        public static float SumFromEffects(EffectKind kind)
        {
            int today = CurrentDay();
            float sum = 0f;

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var en = entries[i];
                // 期限切れ、または今より未来に始まる（前のプレイの取り残し）ものは捨てる
                if (en.endDayExclusive <= today || en.startDay > today)
                {
                    entries.RemoveAt(i);
                    continue;
                }
                if (en.kind == kind) sum += en.value;
            }
            return sum;
        }

        /// <summary>
        /// 抽選で使う補正の合計（％ポイント）。
        /// Effect 由来の補正に、家族全員の特性・状態が持つ補正（加算のみ）を足す。
        /// → traits.md 3章「イベント抽選のときに、家族全員ぶんを合算して使う」
        /// </summary>
        public static float TotalPercent(EffectKind kind)
        {
            float sum = SumFromEffects(kind);

            if (Household.I != null)
            {
                foreach (var member in Household.I.Members)
                {
                    if (member == null) continue;
                    sum += member.CollectModifier(kind).addSum;
                }
            }
            return sum;
        }

        private static int CurrentDay()
        {
            return GameState.I != null ? Mathf.FloorToInt(GameState.I.AgeDays) : 0;
        }
    }
}
