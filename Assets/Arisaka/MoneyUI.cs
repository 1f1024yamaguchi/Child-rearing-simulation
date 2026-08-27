using UnityEngine;
using TMPro;

/// <summary>
/// 既存コードを変更せず、追加のUI監視用スクリプト。
/// GameState の金額変更イベントを購読して自分のTextだけを更新する。
/// </summary>
public class MoneyUI : MonoBehaviour
{
    [SerializeField] private string label = "Money:";
    [SerializeField] private string currencyPrefix = "¥";

    private TextMeshProUGUI moneyText;

    private void Awake()
    {
        moneyText = GetComponent<TextMeshProUGUI>();
    }

    private void Start()
    {
        if (GameState.I == null)
        {
            return;
        }

        ApplyMoney(GameState.I.Money);
        GameState.I.OnMoneyChanged += ApplyMoney;
    }

    private void OnDestroy()
    {
        if (GameState.I != null)
        {
            GameState.I.OnMoneyChanged -= ApplyMoney;
        }
    }

    private void ApplyMoney(int money)
    {
        if (moneyText == null)
        {
            return;
        }

        moneyText.text = $"{label} {currencyPrefix}{money:N0}";
    }
}
