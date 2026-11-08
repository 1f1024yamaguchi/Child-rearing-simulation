using UnityEngine;

/// <summary>
/// デバッグ機能の入口。DebugAutoPilot と DebugTimeJump を必要になったときに1つだけ作る。
/// メニュー「子育て → デバッグツール」のウィンドウから使う。MCP の execute_code からも呼べる。
///   例）DebugTools.JumpToAge(10);       // 10歳1月1日まで進める
///       DebugTools.SetAuto(true);       // 全部オート ON
/// 再生中にだけ動く。本編のシーンには置かないので、ビルドには影響しない。
/// </summary>
public static class DebugTools
{
    public static GameObject Ensure()
    {
        if (!Application.isPlaying) return null;

        GameObject go = null;
        if (DebugAutoPilot.I != null) go = DebugAutoPilot.I.gameObject;
        else if (DebugTimeJump.I != null) go = DebugTimeJump.I.gameObject;

        if (go == null)
        {
            go = new GameObject("DebugTools");
            Object.DontDestroyOnLoad(go);
        }
        if (DebugAutoPilot.I == null) go.AddComponent<DebugAutoPilot>();
        if (DebugTimeJump.I == null) go.AddComponent<DebugTimeJump>();
        return go;
    }

    /// 全部オートの ON/OFF。
    public static void SetAuto(bool on)
    {
        if (Ensure() == null) return;
        DebugAutoPilot.I.AutoEnabled = on;
        Debug.Log($"[DebugTools] 全部オート：{(on ? "ON" : "OFF")}");
    }

    /// 指定の年齢（歳・月・日）まで進める。
    public static bool JumpToAge(int year, int month = 1, int day = 1)
    {
        if (Ensure() == null) return false;
        return DebugTimeJump.I.JumpTo(year, month, day);
    }

    /// ジャンプを止める。
    public static void StopJump()
    {
        if (DebugTimeJump.I != null) DebugTimeJump.I.Stop();
    }
}
