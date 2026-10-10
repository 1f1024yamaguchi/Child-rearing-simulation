using System;
using UnityEngine;

/// 趣味の種類（たたき台。増やしてよい）
public enum HobbyKind { なし, スポーツ, 音楽, 芸術, ゲーム, 読書 }

/// 子ども固有のステータス。
/// 表示名・ストレスの共通部分は PersonStatus に持ち、
/// 勉強⇔遊び・成績・趣味・体調をここで管理する。
public class ChildStatus : PersonStatus
{
    [Header("勉強⇔遊び")]
    [Tooltip("0=勉強寄り、1=遊び寄り")]
    [Range(0f, 1f)][SerializeField] private float studyPlayBalance = 0.5f;

    [Header("成績・体調")]
    [Range(0f, 100f)][SerializeField] private float grade = 50f;
    [Range(0f, 100f)][SerializeField] private float health = 80f;

    [Header("趣味")]
    [SerializeField] private HobbyKind hobby = HobbyKind.なし;
    [SerializeField] private float hobbyExp = 0f;
    [Tooltip("1レベルアップに必要な経験値（たたき台）")]
    [SerializeField] private float expPerLevel = 100f;

    [Header("合格ライン")]
    [Tooltip("合格ラインの基準（0〜100）（たたき台）")]
    [SerializeField] private float passLine = 60f;

    // ─── 変更通知 ───────────────────────────────────────────
    /// 勉強⇔遊びバランスが変わったときに通知される。
    public event Action<float> OnStudyPlayBalanceChanged;
    /// 成績が変わったときに通知される。
    public event Action<int> OnGradeChanged;
    /// 体調が変わったときに通知される。
    public event Action<int> OnHealthChanged;
    /// 趣味が変わったときに通知される（趣味種別・レベル）。
    public event Action<HobbyKind, int> OnHobbyChanged;

    // ─── 読み取り ───────────────────────────────────────────
    /// 勉強⇔遊びバランス（0=勉強寄り、1=遊び寄り）。
    public float StudyPlayBalance => studyPlayBalance;

    /// 成績（0〜100 の整数）。
    public int Grade => Mathf.RoundToInt(grade);

    /// 体調（0〜100 の整数）。
    public int Health => Mathf.RoundToInt(health);

    /// 現在の趣味の種類。
    public HobbyKind Hobby => hobby;

    /// 趣味の習熟レベル（hobbyExp ÷ expPerLevel の整数部分）。
    public int HobbyLevel => Mathf.FloorToInt(hobbyExp / Mathf.Max(expPerLevel, 1f));

    /// 趣味の累積経験値。
    public float HobbyExp => hobbyExp;

    /// 合格ライン（0〜100）。
    public float PassLine => passLine;

    /// 現在の成績が合格ラインを上回っているか。
    /// 表示と判定を揃えるため、丸めた整数（Grade）で比べる。
    public bool IsAbovePassLine => Grade >= Mathf.RoundToInt(passLine);

    /// GameState から子どもの現在の年齢を求める（自前で持たない）。
    public float AgeYears => GameState.I != null ? GameState.I.AgeYears : 0f;

    /// 子どもカード用の1行プロフィール。
    public string ProfileLine
    {
        get
        {
            int age = Mathf.FloorToInt(AgeYears);
            if (hobby == HobbyKind.なし)
                return $"{age}歳・好きなことを探し中";
            return $"{age}歳・{hobby}が好き";
        }
    }

    // ─── 書き込み ───────────────────────────────────────────
    /// 勉強⇔遊びバランスをセットする（0〜1 にクランプ）。
    public void SetStudyPlayBalance(float value)
    {
        studyPlayBalance = Mathf.Clamp01(value);
        OnStudyPlayBalanceChanged?.Invoke(studyPlayBalance);
    }

    /// 成績を delta 分増減する（0〜100 にクランプ）。
    /// delta > 0 のときだけ Grades 補正を掛ける。traits.md 3章。
    public void AddGrade(float delta)
    {
        int prev = Grade;
        if (delta > 0f)
        {
            var (addSum, mulSum) = CollectModifier(EffectKind.Grades);
            delta = (delta + addSum) * (1f + mulSum);
        }
        grade = Mathf.Clamp(grade + delta, 0f, 100f);
        if (Grade != prev)
            OnGradeChanged?.Invoke(Grade);
    }

    /// 体調を delta 分増減する（0〜100 にクランプ）。
    /// delta > 0 のときだけ Health 補正を掛ける。traits.md 3章。
    public void AddHealth(float delta)
    {
        int prev = Health;
        if (delta > 0f)
        {
            var (addSum, mulSum) = CollectModifier(EffectKind.Health);
            delta = (delta + addSum) * (1f + mulSum);
        }
        health = Mathf.Clamp(health + delta, 0f, 100f);
        if (Health != prev)
            OnHealthChanged?.Invoke(Health);
    }

    /// 趣味経験値を delta 分増やす（マイナスにはしない）。
    /// 趣味が「なし」のときは経験値を加算しない。
    /// Hobby 補正を適用する（AddHobbyExp はプラスしか呼ばれない前提）。traits.md 3章。
    public void AddHobbyExp(float delta)
    {
        if (hobby == HobbyKind.なし) return;
        int prevLevel = HobbyLevel;
        if (delta > 0f)
        {
            var (addSum, mulSum) = CollectModifier(EffectKind.Hobby);
            delta = (delta + addSum) * (1f + mulSum);
        }
        hobbyExp = Mathf.Max(0f, hobbyExp + delta);
        if (HobbyLevel != prevLevel)
            OnHobbyChanged?.Invoke(hobby, HobbyLevel);
    }

    /// 趣味の種類をセットする。
    public void SetHobby(HobbyKind newHobby)
    {
        hobby = newHobby;
        OnHobbyChanged?.Invoke(hobby, HobbyLevel);
    }

    /// <summary>
    /// セーブ復元用。成績・体調・バランス・趣味・経験値をまとめて戻す。
    /// 上限・下限は現在の Clamp に合わせる。
    /// </summary>
    public void RestoreValues(float newGrade, float newHealth, float newStudyPlayBalance, HobbyKind newHobby, float newHobbyExp)
    {
        grade = Mathf.Clamp(newGrade, 0f, 100f);
        health = Mathf.Clamp(newHealth, 0f, 100f);
        studyPlayBalance = Mathf.Clamp01(newStudyPlayBalance);
        hobby = newHobby;
        hobbyExp = Mathf.Max(0f, newHobbyExp);

        OnGradeChanged?.Invoke(Grade);
        OnHealthChanged?.Invoke(Health);
        OnStudyPlayBalanceChanged?.Invoke(studyPlayBalance);
        OnHobbyChanged?.Invoke(hobby, HobbyLevel);
    }

    // ─── 毎日の効果（dailyEffects） override ────────────────
    /// PersonStatus の実装（Stress / Money）に加えて、
    /// Health / Grades / Hobby を処理する。
    /// 書き込み窓口を通すので、マイナスには補正は掛からない（仕様どおり）。
    /// 親の Grades など「この人が持っていない値」は何もしない。
    public override void ApplyDailyEffect(DailyEffect effect)
    {
        switch (effect.kind)
        {
            case EffectKind.Health:
                AddHealth(effect.value);
                break;
            case EffectKind.Grades:
                AddGrade(effect.value);
                break;
            case EffectKind.Hobby:
                AddHobbyExp(effect.value);
                break;
            default:
                // Stress / Money は PersonStatus に任せる
                base.ApplyDailyEffect(effect);
                break;
        }
    }
}
