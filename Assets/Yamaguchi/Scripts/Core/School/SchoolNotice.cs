using UnityEngine;

/// <summary>学校・進路の短い通知。画面上部の通知（SupportToast）に出し、無ければログだけ。</summary>
public static class SchoolNotice
{
    public static void Toast(string message)
    {
        var toast = Object.FindFirstObjectByType<SupportToast>();
        if (toast != null) toast.Show(message);
        Debug.Log("[学校] " + message);
    }
}
