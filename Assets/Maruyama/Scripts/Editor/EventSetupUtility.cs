#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Maruyama.Core;
using Maruyama.Events;
using Maruyama.UI;

namespace Maruyama.Editor
{
    public static class EventSetupUtility
    {
        [MenuItem("ChildRearing/Generate Sample Event Assets")]
        public static void GenerateSampleEvents()
        {
            string folderPath = "Assets/Maruyama/Data/Events";
            EnsureFolder("Assets/Maruyama", "Data");
            EnsureFolder("Assets/Maruyama/Data", "Events");

            // 1. デメリットイベント: みらいが風邪をひいた
            var childSick = ScriptableObject.CreateInstance<EventData>();
            childSick.id = "demerit_child_sick";
            childSick.category = EventCategory.Demerit;
            childSick.title = "みらいが風邪をひいた";
            childSick.description = "熱があるみたい。\n病院で診てもらう？それとも家で様子を見る？";

            var sickChoice1 = new EventChoice
            {
                label = "病院に行く",
                effects = new List<Effect>
                {
                    new Effect { kind = EffectKind.Money, target = EffectTarget.Household, value = -8000 }
                }
            };

            var sickChoice2 = new EventChoice
            {
                label = "家で様子を見る",
                effects = new List<Effect>
                {
                    new Effect { kind = EffectKind.Health, target = EffectTarget.Child, value = -15 },
                    new Effect { kind = EffectKind.Stress, target = EffectTarget.Child, value = 8 }
                }
            };

            childSick.choices = new List<EventChoice> { sickChoice1, sickChoice2 };

            string sickPath = $"{folderPath}/Event_Demerit_ChildSick.asset";
            AssetDatabase.CreateAsset(childSick, sickPath);

            // 2. メリットイベント: 一時預かり保育が使える
            var daycare = ScriptableObject.CreateInstance<EventData>();
            daycare.id = "merit_daycare";
            daycare.category = EventCategory.Merit;
            daycare.title = "一時預かり保育が使える";
            daycare.description = "自治体の制度。少しのあいだ預けて自分の時間をつくれる。";

            var daycareChoice1 = new EventChoice
            {
                label = "一時預かり保育に申し込む",
                effects = new List<Effect>
                {
                    new Effect { kind = EffectKind.Money, target = EffectTarget.Household, value = -2000 }
                }
            };

            var daycareChoice2 = new EventChoice
            {
                label = "今回は見送る",
                effects = new List<Effect>
                {
                    new Effect { kind = EffectKind.Stress, target = EffectTarget.Parents, value = -10 },
                    new Effect { kind = EffectKind.Stress, target = EffectTarget.Child, value = -3 }
                }
            };

            daycare.choices = new List<EventChoice> { daycareChoice1, daycareChoice2 };

            string daycarePath = $"{folderPath}/Event_Merit_Daycare.asset";
            AssetDatabase.CreateAsset(daycare, daycarePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EventSetupUtility] サンプルイベントアセットを生成しました:\n1. {sickPath}\n2. {daycarePath}");
        }

        [MenuItem("ChildRearing/Build Event Test Scene & Prefabs")]
        public static void BuildEventTestScene()
        {
            GenerateSampleEvents();

            EnsureFolder("Assets/Maruyama", "Prefabs");
            EnsureFolder("Assets/Maruyama/Prefabs", "Events");
            EnsureFolder("Assets/Maruyama", "Scenes");

            // フォント読み込み (Yamaguchi の Resources/JPFont があれば利用、無ければ Default)
            TMP_FontAsset jpFont = Resources.Load<TMP_FontAsset>("JPFont");

            // --- 1. EffectChip Prefab ---
            GameObject chipObj = new GameObject("EffectChip", typeof(RectTransform), typeof(Image), typeof(EffectChipUI));
            var chipRt = chipObj.GetComponent<RectTransform>();
            chipRt.sizeDelta = new Vector2(140, 32);
            var chipImg = chipObj.GetComponent<Image>();
            chipImg.color = new Color(1, 0.85f, 0.85f);

            GameObject chipTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            chipTextObj.transform.SetParent(chipObj.transform, false);
            var chipTmp = chipTextObj.GetComponent<TextMeshProUGUI>();
            if (jpFont != null) chipTmp.font = jpFont;
            chipTmp.fontSize = 16;
            chipTmp.fontStyle = FontStyles.Bold;
            chipTmp.alignment = TextAlignmentOptions.Center;
            chipTmp.color = Color.black;
            var chipTextRt = chipTextObj.GetComponent<RectTransform>();
            chipTextRt.anchorMin = Vector2.zero;
            chipTextRt.anchorMax = Vector2.one;
            chipTextRt.offsetMin = Vector2.zero;
            chipTextRt.offsetMax = Vector2.zero;

            var chipUI = chipObj.GetComponent<EffectChipUI>();
            SerializedObject soChip = new SerializedObject(chipUI);
            soChip.FindProperty("labelText").objectReferenceValue = chipTmp;
            soChip.FindProperty("bgImage").objectReferenceValue = chipImg;
            soChip.ApplyModifiedProperties();

            string chipPrefabPath = "Assets/Maruyama/Prefabs/Events/EffectChip.prefab";
            GameObject chipPrefab = PrefabUtility.SaveAsPrefabAsset(chipObj, chipPrefabPath);
            Object.DestroyImmediate(chipObj);

            // --- 2. ChoiceButton Prefab ---
            GameObject btnObj = new GameObject("ChoiceButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(EventChoiceButtonUI));
            var btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(400, 52);
            var btnImg = btnObj.GetComponent<Image>();
            btnImg.color = new Color(0.25f, 0.55f, 0.9f);

            GameObject btnTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var btnTmp = btnTextObj.GetComponent<TextMeshProUGUI>();
            if (jpFont != null) btnTmp.font = jpFont;
            btnTmp.fontSize = 18;
            btnTmp.fontStyle = FontStyles.Bold;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.color = Color.white;
            var btnTextRt = btnTextObj.GetComponent<RectTransform>();
            btnTextRt.anchorMin = Vector2.zero;
            btnTextRt.anchorMax = Vector2.one;
            btnTextRt.offsetMin = Vector2.zero;
            btnTextRt.offsetMax = Vector2.zero;

            var btnUI = btnObj.GetComponent<EventChoiceButtonUI>();
            SerializedObject soBtn = new SerializedObject(btnUI);
            soBtn.FindProperty("button").objectReferenceValue = btnObj.GetComponent<Button>();
            soBtn.FindProperty("labelText").objectReferenceValue = btnTmp;
            soBtn.ApplyModifiedProperties();

            string btnPrefabPath = "Assets/Maruyama/Prefabs/Events/ChoiceButton.prefab";
            GameObject btnPrefab = PrefabUtility.SaveAsPrefabAsset(btnObj, btnPrefabPath);
            Object.DestroyImmediate(btnObj);

            // --- 3. ChoiceBlock Prefab ---
            GameObject blockObj = new GameObject("ChoiceBlock", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ChoiceBlockUI));
            var blockRt = blockObj.GetComponent<RectTransform>();
            blockRt.sizeDelta = new Vector2(420, 90);
            var blockVlg = blockObj.GetComponent<VerticalLayoutGroup>();
            blockVlg.childAlignment = TextAnchor.MiddleCenter;
            blockVlg.spacing = 6;
            blockVlg.childControlWidth = false;
            blockVlg.childControlHeight = false;

            GameObject chipsContainerObj = new GameObject("ChipsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            chipsContainerObj.transform.SetParent(blockObj.transform, false);
            var chipsHlg = chipsContainerObj.GetComponent<HorizontalLayoutGroup>();
            chipsHlg.childAlignment = TextAnchor.MiddleCenter;
            chipsHlg.spacing = 8;
            chipsHlg.childControlWidth = false;
            chipsHlg.childControlHeight = false;
            var chipsRt = chipsContainerObj.GetComponent<RectTransform>();
            chipsRt.sizeDelta = new Vector2(400, 32);

            GameObject buttonInstanceObj = PrefabUtility.InstantiatePrefab(btnPrefab, blockObj.transform) as GameObject;

            var blockUI = blockObj.GetComponent<ChoiceBlockUI>();
            SerializedObject soBlock = new SerializedObject(blockUI);
            soBlock.FindProperty("chipsContainer").objectReferenceValue = chipsContainerObj.transform;
            soBlock.FindProperty("chipPrefab").objectReferenceValue = chipPrefab.GetComponent<EffectChipUI>();
            soBlock.FindProperty("choiceButton").objectReferenceValue = buttonInstanceObj.GetComponent<EventChoiceButtonUI>();
            soBlock.ApplyModifiedProperties();

            string blockPrefabPath = "Assets/Maruyama/Prefabs/Events/ChoiceBlock.prefab";
            GameObject blockPrefab = PrefabUtility.SaveAsPrefabAsset(blockObj, blockPrefabPath);
            Object.DestroyImmediate(blockObj);

            // --- 4. EventCard Prefab ---
            GameObject cardObj = new GameObject("EventCard", typeof(RectTransform), typeof(Image), typeof(EventCardUI));
            var cardRt = cardObj.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(450, 550);
            var cardImg = cardObj.GetComponent<Image>();
            cardImg.color = new Color(0.98f, 0.96f, 0.92f); // ベージュ背景

            // Header Background
            GameObject headerObj = new GameObject("HeaderBg", typeof(RectTransform), typeof(Image));
            headerObj.transform.SetParent(cardObj.transform, false);
            var headerRt = headerObj.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0, 1);
            headerRt.anchorMax = Vector2.one;
            headerRt.pivot = new Vector2(0.5f, 1);
            headerRt.sizeDelta = new Vector2(0, 55);
            headerRt.anchoredPosition = Vector2.zero;

            GameObject badgeObj = new GameObject("CategoryBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
            badgeObj.transform.SetParent(headerObj.transform, false);
            var badgeTmp = badgeObj.GetComponent<TextMeshProUGUI>();
            if (jpFont != null) badgeTmp.font = jpFont;
            badgeTmp.fontSize = 20;
            badgeTmp.fontStyle = FontStyles.Bold;
            badgeTmp.alignment = TextAlignmentOptions.Center;
            badgeTmp.color = new Color(0.3f, 0.3f, 0.3f);
            var badgeRt = badgeObj.GetComponent<RectTransform>();
            badgeRt.anchorMin = Vector2.zero;
            badgeRt.anchorMax = Vector2.one;
            badgeRt.offsetMin = Vector2.zero;
            badgeRt.offsetMax = Vector2.zero;

            // Icon Image
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(cardObj.transform, false);
            var iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchoredPosition = new Vector2(0, 160);
            iconRt.sizeDelta = new Vector2(80, 80);

            // Title
            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(cardObj.transform, false);
            var titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
            if (jpFont != null) titleTmp.font = jpFont;
            titleTmp.fontSize = 26;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.black;
            var titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchoredPosition = new Vector2(0, 80);
            titleRt.sizeDelta = new Vector2(400, 45);

            // Description
            GameObject descObj = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
            descObj.transform.SetParent(cardObj.transform, false);
            var descTmp = descObj.GetComponent<TextMeshProUGUI>();
            if (jpFont != null) descTmp.font = jpFont;
            descTmp.fontSize = 18;
            descTmp.alignment = TextAlignmentOptions.Center;
            descTmp.color = new Color(0.2f, 0.2f, 0.2f);
            var descRt = descObj.GetComponent<RectTransform>();
            descRt.anchoredPosition = new Vector2(0, 10);
            descRt.sizeDelta = new Vector2(400, 70);

            // Choices Container
            GameObject choicesContainerObj = new GameObject("ChoicesContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            choicesContainerObj.transform.SetParent(cardObj.transform, false);
            var choicesVlg = choicesContainerObj.GetComponent<VerticalLayoutGroup>();
            choicesVlg.childAlignment = TextAnchor.UpperCenter;
            choicesVlg.spacing = 12;
            choicesVlg.childControlWidth = false;
            choicesVlg.childControlHeight = false;
            var choicesRt = choicesContainerObj.GetComponent<RectTransform>();
            choicesRt.anchoredPosition = new Vector2(0, -140);
            choicesRt.sizeDelta = new Vector2(420, 200);

            var cardUI = cardObj.GetComponent<EventCardUI>();
            SerializedObject soCard = new SerializedObject(cardUI);
            soCard.FindProperty("headerBgImage").objectReferenceValue = headerObj.GetComponent<Image>();
            soCard.FindProperty("categoryBadgeText").objectReferenceValue = badgeTmp;
            soCard.FindProperty("iconImage").objectReferenceValue = iconObj.GetComponent<Image>();
            soCard.FindProperty("titleText").objectReferenceValue = titleTmp;
            soCard.FindProperty("descriptionText").objectReferenceValue = descTmp;
            soCard.FindProperty("choicesContainer").objectReferenceValue = choicesContainerObj.transform;
            soCard.FindProperty("choiceBlockPrefab").objectReferenceValue = blockPrefab.GetComponent<ChoiceBlockUI>();
            soCard.ApplyModifiedProperties();

            string cardPrefabPath = "Assets/Maruyama/Prefabs/Events/EventCard.prefab";
            GameObject cardPrefab = PrefabUtility.SaveAsPrefabAsset(cardObj, cardPrefabPath);
            Object.DestroyImmediate(cardObj);

            // --- 5. EventDialog Prefab ---
            GameObject dialogObj = new GameObject("EventDialog", typeof(RectTransform), typeof(EventDialogUI));
            var dialogRt = dialogObj.GetComponent<RectTransform>();
            dialogRt.anchorMin = Vector2.zero;
            dialogRt.anchorMax = Vector2.one;
            dialogRt.offsetMin = Vector2.zero;
            dialogRt.offsetMax = Vector2.zero;

            // Background Mask
            GameObject bgMaskObj = new GameObject("BackgroundMask", typeof(RectTransform), typeof(Image));
            bgMaskObj.transform.SetParent(dialogObj.transform, false);
            var bgMaskImg = bgMaskObj.GetComponent<Image>();
            bgMaskImg.color = new Color(0, 0, 0, 0.6f);
            var bgMaskRt = bgMaskObj.GetComponent<RectTransform>();
            bgMaskRt.anchorMin = Vector2.zero;
            bgMaskRt.anchorMax = Vector2.one;
            bgMaskRt.offsetMin = Vector2.zero;
            bgMaskRt.offsetMax = Vector2.zero;

            // Cards Panel (Horizontal)
            GameObject cardsPanelObj = new GameObject("CardsPanel", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            cardsPanelObj.transform.SetParent(dialogObj.transform, false);
            var cardsHlg = cardsPanelObj.GetComponent<HorizontalLayoutGroup>();
            cardsHlg.childAlignment = TextAnchor.MiddleCenter;
            cardsHlg.spacing = 40;
            cardsHlg.childControlWidth = false;
            cardsHlg.childControlHeight = false;
            var cardsPanelRt = cardsPanelObj.GetComponent<RectTransform>();
            cardsPanelRt.anchorMin = Vector2.zero;
            cardsPanelRt.anchorMax = Vector2.one;
            cardsPanelRt.offsetMin = Vector2.zero;
            cardsPanelRt.offsetMax = Vector2.zero;

            GameObject demeritCardInstance = PrefabUtility.InstantiatePrefab(cardPrefab, cardsPanelObj.transform) as GameObject;
            demeritCardInstance.name = "DemeritCard";

            GameObject meritCardInstance = PrefabUtility.InstantiatePrefab(cardPrefab, cardsPanelObj.transform) as GameObject;
            meritCardInstance.name = "MeritCard";

            var dialogUI = dialogObj.GetComponent<EventDialogUI>();
            SerializedObject soDialog = new SerializedObject(dialogUI);
            soDialog.FindProperty("dialogPanel").objectReferenceValue = dialogObj;
            soDialog.FindProperty("demeritCardUI").objectReferenceValue = demeritCardInstance.GetComponent<EventCardUI>();
            soDialog.FindProperty("meritCardUI").objectReferenceValue = meritCardInstance.GetComponent<EventCardUI>();
            soDialog.ApplyModifiedProperties();

            string dialogPrefabPath = "Assets/Maruyama/Prefabs/Events/EventDialog.prefab";
            GameObject dialogPrefab = PrefabUtility.SaveAsPrefabAsset(dialogObj, dialogPrefabPath);
            Object.DestroyImmediate(dialogObj);

            // --- 6. Create Test Scene ---
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // EventSystem GameObject (必須：マウスクリックなどの UI 入力を受ける)
            GameObject eventSystemObj = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            // Managers GameObject
            GameObject mgrObj = new GameObject("MaruyamaEventManager");
            mgrObj.AddComponent<MaruyamaPauseManager>();
            mgrObj.AddComponent<MaruyamaChildStatus>();
            var evtMgr = mgrObj.AddComponent<EventManager>();

            // Canvas & EventDialog
            GameObject canvasObj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            GameObject dialogInScene = PrefabUtility.InstantiatePrefab(dialogPrefab, canvasObj.transform) as GameObject;
            dialogInScene.SetActive(false);

            // Assign EventManager properties
            var sickAsset = AssetDatabase.LoadAssetAtPath<EventData>("Assets/Maruyama/Data/Events/Event_Demerit_ChildSick.asset");
            var daycareAsset = AssetDatabase.LoadAssetAtPath<EventData>("Assets/Maruyama/Data/Events/Event_Merit_Daycare.asset");

            SerializedObject soEvtMgr = new SerializedObject(evtMgr);
            soEvtMgr.FindProperty("dialogUI").objectReferenceValue = dialogInScene.GetComponent<EventDialogUI>();
            var demeritListProp = soEvtMgr.FindProperty("demeritEvents");
            demeritListProp.arraySize = 1;
            demeritListProp.GetArrayElementAtIndex(0).objectReferenceValue = sickAsset;
            var meritListProp = soEvtMgr.FindProperty("meritEvents");
            meritListProp.arraySize = 1;
            meritListProp.GetArrayElementAtIndex(0).objectReferenceValue = daycareAsset;
            soEvtMgr.ApplyModifiedProperties();

            // Auto Trigger Script for Test Scene
            var triggerScriptObj = new GameObject("TestTriggerScript");
            var triggerComp = triggerScriptObj.AddComponent<MaruyamaTestTrigger>();

            string scenePath = "Assets/Maruyama/Scenes/EventTestScene.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EventSetupUtility] イベント検証用シーン構築完了: {scenePath}");
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string fullPath = $"{parent}/{folderName}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }

    public class MaruyamaTestTrigger : MonoBehaviour
    {
        private void Start()
        {
            Invoke(nameof(Trigger), 1.0f);
        }

        private void Trigger()
        {
            if (EventManager.Instance != null)
            {
                Debug.Log("[MaruyamaTestTrigger] テストシーン実行開始。1秒後にイベントダイアログをトリガーします。");
                EventManager.Instance.TriggerEventPair();
            }
        }
    }
}
#endif
