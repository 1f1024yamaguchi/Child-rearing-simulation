using UnityEngine;
using UnityEngine.UI;

/// Assets/Maruyama/StressUI.cs を Yamaguchi 側に置いたもの。
/// 違いは、GameState.I ではなく [SerializeField] で指定した PersonStatus の
/// ストレスを見ること。母・父・子どのカードにも使える。
///
/// ※ Assets/Maruyama/StressUI.cs は変更も削除もしない。
[RequireComponent(typeof(Slider))]
public class PersonStressUI : MonoBehaviour
{
    [Tooltip("ストレスを見る対象の PersonStatus（母・父・子どれでも可）")]
    [SerializeField] private PersonStatus target;

    private Slider slider;

    private void Awake()
    {
        slider = GetComponent<Slider>();
    }

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("PersonStressUI: target が設定されていません。", this);
        }
    }

    private void OnEnable()
    {
        if (target != null)
        {
            slider.SetValueWithoutNotify(target.Stress / 100f);
            target.OnStressChanged += UpdateSlider;
        }
    }

    private void OnDisable()
    {
        if (target != null) target.OnStressChanged -= UpdateSlider;
    }

    private void UpdateSlider(int stress)
    {
        slider.SetValueWithoutNotify(stress / 100f);
    }
}
