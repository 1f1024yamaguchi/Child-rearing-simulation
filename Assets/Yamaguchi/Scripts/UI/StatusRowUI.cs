using TMPro;
using UnityEngine;

/// <summary>
/// ステータス欄の 1 行（ラベル : 値）を表示するだけのコンポーネント。
/// 行の中身の見た目だけを受け持ち、一覧の管理は StatusListUI が行う。
/// </summary>
public class StatusRowUI : MonoBehaviour
{
    [SerializeField] private string key;
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private TextMeshProUGUI valueText;

    /// <summary>この行の識別キー（"salary" など）。StatusListUI が更新対象を探すのに使う。</summary>
    public string Key { get { return key; } }

    private void OnEnable()
    {
        // プレハブから複製した直後は TMP のメッシュが未生成で表示されないことがあるため、
        // 有効化のタイミングで必ず作り直させる。
        RefreshMesh(labelText);
        RefreshMesh(valueText);
    }

    private static void RefreshMesh(TextMeshProUGUI text)
    {
        if (text == null)
        {
            return;
        }
        text.SetAllDirty();
        text.ForceMeshUpdate(true, true);
    }

    /// <summary>ラベルと値をまとめて設定する。</summary>
    public void Set(string rowKey, string label, string value)
    {
        key = rowKey;
        if (!string.IsNullOrEmpty(rowKey))
        {
            gameObject.name = "row_" + rowKey;
        }
        SetLabel(label);
        SetValue(value);
    }

    /// <summary>左側のラベルだけを変更する。</summary>
    public void SetLabel(string label)
    {
        if (labelText != null)
        {
            labelText.text = label;
            labelText.ForceMeshUpdate(true, true);
        }
    }

    /// <summary>右側の値だけを変更する。</summary>
    public void SetValue(string value)
    {
        if (valueText != null)
        {
            valueText.text = value;
            valueText.ForceMeshUpdate(true, true);
        }
    }
}
