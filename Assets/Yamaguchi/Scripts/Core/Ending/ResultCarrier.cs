/// <summary>シーンをまたいで EndingResult を1つ渡すだけ（result-scene.md 6章）。</summary>
public static class ResultCarrier
{
    public static EndingResult Current { get; private set; }
    public static bool HasResult => Current != null;
    public static void Set(EndingResult result) => Current = result;
    public static void Clear() => Current = null;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Current = null;
}
