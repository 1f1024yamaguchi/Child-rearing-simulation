/// <summary>
/// タイトル → 本編の受け渡し（static。MonoBehaviour にしない）。
/// 既定は None：本編シーンをエディタで直接再生したときは今までと同じ動きになる。
/// </summary>
public static class GameLaunchRequest
{
    public enum Mode { None, NewGame, Continue }

    public static Mode CurrentMode { get; private set; } = Mode.None;
    public static SaveSlot ContinueSlot { get; private set; } = SaveSlot.Auto;

    public static void RequestNewGame()
    {
        CurrentMode = Mode.NewGame;
    }

    public static void RequestContinue(SaveSlot slot)
    {
        CurrentMode = Mode.Continue;
        ContinueSlot = slot;
    }

    /// <summary>今の値を返して、None に戻す。</summary>
    public static Mode Consume(out SaveSlot slot)
    {
        var mode = CurrentMode;
        slot = ContinueSlot;
        CurrentMode = Mode.None;
        return mode;
    }
}
