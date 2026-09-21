using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// スクロールするステータス欄の行を追加・更新・削除する。
/// 行の見た目は StatusRowUI、スクロール自体は ScrollRect が担当し、
/// このクラスは「どの項目を何行出すか」だけを管理する。
/// </summary>
public class StatusListUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private StatusRowUI rowPrefab;

    private readonly Dictionary<string, StatusRowUI> rows = new Dictionary<string, StatusRowUI>();

    private void Awake()
    {
        if (scrollRect == null)
        {
            scrollRect = GetComponent<ScrollRect>();
        }
        if (content == null && scrollRect != null)
        {
            content = scrollRect.content;
        }
        CollectExistingRows();
    }

    /// <summary>シーンに最初から置いてある行を辞書に取り込む。</summary>
    private void CollectExistingRows()
    {
        if (content == null)
        {
            return;
        }
        foreach (var row in content.GetComponentsInChildren<StatusRowUI>(true))
        {
            if (!string.IsNullOrEmpty(row.Key) && !rows.ContainsKey(row.Key))
            {
                rows.Add(row.Key, row);
            }
        }
    }

    /// <summary>
    /// 項目を設定する。キーが未登録なら行を追加し、登録済みなら内容を更新する。
    /// </summary>
    public StatusRowUI SetStatus(string key, string label, string value)
    {
        StatusRowUI row;
        if (!rows.TryGetValue(key, out row) || row == null)
        {
            if (rowPrefab == null || content == null)
            {
                Debug.LogWarning("StatusListUI: rowPrefab か content が未設定です。", this);
                return null;
            }
            row = Instantiate(rowPrefab, content);
            row.gameObject.SetActive(true);
            rows[key] = row;
        }
        row.Set(key, label, value);
        RebuildLayout();
        return row;
    }

    /// <summary>既存の行の値だけを更新する（行が無ければ何もしない）。</summary>
    public void SetValue(string key, string value)
    {
        StatusRowUI row;
        if (rows.TryGetValue(key, out row) && row != null)
        {
            row.SetValue(value);
        }
    }

    /// <summary>指定した行を削除する。</summary>
    public bool RemoveStatus(string key)
    {
        StatusRowUI row;
        if (!rows.TryGetValue(key, out row))
        {
            return false;
        }
        if (row != null)
        {
            Destroy(row.gameObject);
        }
        rows.Remove(key);
        RebuildLayout();
        return true;
    }

    /// <summary>すべての行を消す。</summary>
    public void Clear()
    {
        foreach (var pair in rows)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value.gameObject);
            }
        }
        rows.Clear();
        RebuildLayout();
    }

    /// <summary>一覧を一番上までスクロールさせる。</summary>
    public void ScrollToTop()
    {
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    /// <summary>行数が変わったあとにレイアウトとスクロール範囲を即座に更新する。</summary>
    private void RebuildLayout()
    {
        if (content == null)
        {
            return;
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }
}
