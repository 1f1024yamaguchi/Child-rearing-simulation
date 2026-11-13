using System;
using System.Reflection;
using Maruyama.Events;
using Maruyama.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 編集ウィンドウの中に、ゲームの EventDialog を再生せずに描いて見せる。
/// 専用の「プレビュー用シーン」にダイアログを作り、カメラで画像にして表示する。
/// 選んでいるイベントが変わる／中身を直したときは、自動で作り直す。
/// （見るだけ。ボタンは押せない。効果の適用やゲームの状態にも触らない）
/// </summary>
public static class EventCardPreview
{
    private const string PrefKeyAspect = "Kosodate.EventPreview.Aspect";
    private const float ReferenceWidth = 800f;          // ゲームの CanvasScaler の基準幅
    private static readonly Vector2[] Aspects = { new Vector2(16f, 9f), new Vector2(4f, 3f) };
    private static readonly string[] AspectNames = { "16:9", "4:3" };

    private const int PreviewLayer = 31;               // 他の画面に映らないよう、専用のレイヤー番号を使う
    private static readonly Vector3 Origin = new Vector3(5000f, 5000f, 0f); // ゲームの物から遠く離れた場所

    private static Camera cam;
    private static RenderTexture rt;
    private static GameObject camGo;
    private static GameObject canvasGo;
    private static int builtId;
    private static int builtDirty = -1;
    private static int builtAspect = -1;
    private static string error;
    private static double lastDrawTime;
    private static bool watching;

    /// <summary>プレビューの枠を描く。</summary>
    public static void DrawBox(EventData e)
    {
        using (new SupportEditorGUI.BoxScope("見た目プレビュー（再生しなくても見られます）"))
        {
            if (e == null)
            {
                EditorGUILayout.LabelField("イベントを選ぶと表示されます。", EditorStyles.miniLabel);
                return;
            }

            int aspect = Mathf.Clamp(EditorPrefs.GetInt(PrefKeyAspect, 0), 0, Aspects.Length - 1);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("画面の比率", GUILayout.Width(64));
                int next = EditorGUILayout.Popup(aspect, AspectNames, GUILayout.Width(70));
                if (next != aspect) { aspect = next; EditorPrefs.SetInt(PrefKeyAspect, aspect); }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("作り直す", EditorStyles.miniButton, GUILayout.Width(70))) builtDirty = -1;
            }

            var ratio = Aspects[aspect];
            float w = Mathf.Max(200f, Mathf.Min(EditorGUIUtility.currentViewWidth - 52f, 620f));
            float h = w * ratio.y / ratio.x;
            Rect r = GUILayoutUtility.GetRect(w, w, h, h, GUILayout.ExpandWidth(false));

            lastDrawTime = EditorApplication.timeSinceStartup;
            EnsureWatcher();

            if (Event.current.type != EventType.Repaint) return;

            try
            {
                Render(e, aspect, r);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.LogException(ex);
                Dispose();
            }

            if (!string.IsNullOrEmpty(error))
            {
                EditorGUI.DrawRect(r, new Color(0.2f, 0.2f, 0.2f));
                GUI.Label(r, "プレビューを作れませんでした:\n" + error, EditorStyles.wordWrappedMiniLabel);
            }
        }

        if (!string.IsNullOrEmpty(error) && Event.current.type == EventType.Layout)
        {
            // 次の表示でもう一度試せるように、エラーはここでは消さない（「作り直す」で再挑戦）
        }
    }

    private static void Render(EventData e, int aspect, Rect r)
    {
        int id = e.GetInstanceID();
        int dirty = EditorUtility.GetDirtyCount(e);
        if (canvasGo == null || cam == null || rt == null || id != builtId || dirty != builtDirty || aspect != builtAspect)
        {
            Build(e, aspect);
            builtId = id;
            builtDirty = dirty;
            builtAspect = aspect;
        }
        if (canvasGo == null || cam == null || !string.IsNullOrEmpty(error)) return;

        Canvas.ForceUpdateCanvases();
        cam.Render();
        GUI.DrawTexture(r, rt, ScaleMode.StretchToFill, false);
    }

    private static void SetLayerAndHide(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = PreviewLayer;
            t.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    private static void Build(EventData e, int aspect)
    {
        error = null;
        Dispose();

        var prefab = FindDialogPrefab();
        if (prefab == null)
        {
            error = "EventDialog のプレハブが見つかりません。";
            return;
        }

        // 作ったものでシーンが「変更あり」にならないようにする
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool wasDirty = scene.isDirty;

        var ratio = Aspects[aspect];
        float canvasH = ReferenceWidth * ratio.y / ratio.x;

        int texW = 1024;
        int texH = Mathf.RoundToInt(texW * ratio.y / ratio.x);
        rt = new RenderTexture(texW, texH, 24, RenderTextureFormat.ARGB32) { hideFlags = HideFlags.HideAndDontSave };

        camGo = new GameObject("EventPreviewCamera", typeof(Camera));
        camGo.hideFlags = HideFlags.HideAndDontSave;
        cam = camGo.GetComponent<Camera>();
        cam.enabled = false;
        cam.orthographic = true;
        cam.orthographicSize = canvasH * 0.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.18f, 0.2f, 1f);
        cam.cullingMask = 1 << PreviewLayer;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.targetTexture = rt;
        camGo.transform.position = Origin + new Vector3(0f, 0f, -10f);

        canvasGo = new GameObject("EventPreviewCanvas", typeof(RectTransform), typeof(Canvas));
        canvasGo.hideFlags = HideFlags.HideAndDontSave;
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        var crt = (RectTransform)canvasGo.transform;
        crt.sizeDelta = new Vector2(ReferenceWidth, canvasH);
        crt.position = Origin;
        crt.localScale = Vector3.one;

        var dlg = Object.Instantiate(prefab, canvasGo.transform);
        var drt = (RectTransform)dlg.transform;
        drt.anchorMin = Vector2.zero;
        drt.anchorMax = Vector2.one;
        drt.offsetMin = Vector2.zero;
        drt.offsetMax = Vector2.zero;
        drt.localScale = Vector3.one;
        dlg.SetActive(true);

        var ui = dlg.GetComponent<EventDialogUI>();
        if (ui == null) { error = "EventDialog に EventDialogUI が付いていません。"; return; }
        ui.Show(e.category == EventCategory.Demerit ? e : null,
                e.category == EventCategory.Merit ? e : null,
                null);
        SetLayerAndHide(canvasGo);

        // 文字の大きさが決まってから、並びとカードの縮小を確定させる（数回まわして落ち着かせる）
        var lateUpdate = typeof(CardFitScaler).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        for (int i = 0; i < 3; i++)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(crt);
            if (lateUpdate == null) continue;
            foreach (var fit in canvasGo.GetComponentsInChildren<CardFitScaler>())
                lateUpdate.Invoke(fit, null);
        }
        Canvas.ForceUpdateCanvases();

    }

    private static GameObject FindDialogPrefab()
    {
        foreach (var guid in AssetDatabase.FindAssets("EventDialog t:Prefab"))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (go != null && go.GetComponent<EventDialogUI>() != null) return go;
        }
        return null;
    }

    // ---- 後片付け：しばらく表示されなければ、プレビュー用のものを消す ----

    private static void EnsureWatcher()
    {
        if (watching) return;
        watching = true;
        EditorApplication.update += Watch;
        AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        EditorApplication.quitting += Dispose;
    }

    private static void Watch()
    {
        if (canvasGo != null && EditorApplication.timeSinceStartup - lastDrawTime > 5.0) Dispose();
    }

    private static void Dispose()
    {
        if (canvasGo != null) { Object.DestroyImmediate(canvasGo); canvasGo = null; }
        if (camGo != null) { Object.DestroyImmediate(camGo); camGo = null; }
        cam = null;
        if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); rt = null; }
        builtDirty = -1;
    }
}
