// Assets/Editor/UiRectRepair.cs
// Чинит NaN/∞ и некорректные размеры/якоря у RectTransform в сцене/выделении/префабах.
// Делает Undo, помечает сцены грязными, форсит перестройку лейаута.

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public static class UiRectRepair
{
    private const float MinW = 32f;
    private const float MinH = 16f;

    [MenuItem("Tools/UI/Repair All Rects (Scene)")]
    private static void RepairAllInScene()
    {
        var rts = Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.InstanceID);
        int fixedCnt = RepairBatch(rts, "Repair All Rects (Scene)");
        Canvas.ForceUpdateCanvases();
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"UI Rect Repair: fixed {fixedCnt} of {rts.Length} rects in scene(s).");
    }

    [MenuItem("Tools/UI/Repair Selected")]
    private static void RepairSelected()
    {
        var list = new List<RectTransform>();
        foreach(var go in Selection.gameObjects)
            list.AddRange(go.GetComponentsInChildren<RectTransform>(true));

        int fixedCnt = RepairBatch(list, "Repair Selected");
        Canvas.ForceUpdateCanvases();
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"UI Rect Repair: fixed {fixedCnt} rects in selection.");
    }

    [MenuItem("Tools/UI/Repair All Prefabs (Project)")]
    private static void RepairAllPrefabsInProject()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int fixedTotal = 0, processed = 0;
        try
        {
            foreach(var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EditorUtility.DisplayProgressBar("Repair Prefabs", path, (float)processed / Mathf.Max(1, guids.Length));

                var root = PrefabUtility.LoadPrefabContents(path);
                if(root != null)
                {
                    var rts = root.GetComponentsInChildren<RectTransform>(true);
                    int fixedCnt = RepairBatch(rts, null); // без ProgressBar внутри
                    if(fixedCnt > 0)
                    {
                        fixedTotal += fixedCnt;
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    PrefabUtility.UnloadPrefabContents(root);
                }
                processed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"UI Rect Repair: fixed {fixedTotal} rects across {guids.Length} prefabs.");
    }

    private static int RepairBatch(IEnumerable<RectTransform> rts, string progress)
    {
        int fixedCnt = 0, i = 0;
        var list = (rts is RectTransform[] arr) ? arr : new List<RectTransform>(rts).ToArray();

        foreach(var rt in list)
        {
            if(progress != null)
                EditorUtility.DisplayProgressBar(progress, rt.name, (float)i / Mathf.Max(1, list.Length));

            if(rt == null)
            { i++; continue; }
            Undo.RecordObject(rt, "UI Rect Repair");
            bool changed = RepairOne(rt);
            if(changed)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                EditorUtility.SetDirty(rt);
                fixedCnt++;
            }
            i++;
        }

        if(progress != null)
            EditorUtility.ClearProgressBar();
        return fixedCnt;
    }

    private static bool RepairOne(RectTransform rt)
    {
        bool changed = false;

        // 1) Scale/Rotation/Position: убираем не-финитные и дикие значения
        if(!Finite(rt.localScale))
        { rt.localScale = Vector3.one; changed = true; }
        if(!Finite(rt.localEulerAngles))
        { rt.localEulerAngles = Vector3.zero; changed = true; }
        if(!Finite(rt.anchoredPosition))
        { rt.anchoredPosition = Vector2.zero; changed = true; }
        if(!Finite(rt.pivot))
        { rt.pivot = new Vector2(0.5f, 0.5f); changed = true; }

        // 2) Anchors: в диапазон [0..1] и порядок (min <= max)
        Vector2 aMin = rt.anchorMin, aMax = rt.anchorMax;
        if(!Finite(aMin))
        { aMin = Vector2.zero; changed = true; }
        if(!Finite(aMax))
        { aMax = Vector2.one; changed = true; }
        aMin = Clamp01(aMin);
        aMax = Clamp01(aMax);
        // Если перепутаны, меняем местами по осям
        if(aMin.x > aMax.x)
        { float t = aMin.x; aMin.x = aMax.x; aMax.x = t; changed = true; }
        if(aMin.y > aMax.y)
        { float t = aMin.y; aMin.y = aMax.y; aMax.y = t; changed = true; }
        if(rt.anchorMin != aMin)
        { rt.anchorMin = aMin; changed = true; }
        if(rt.anchorMax != aMax)
        { rt.anchorMax = aMax; changed = true; }

        // 3) Offsets/sizeDelta: чистим не-финитные
        var offMin = rt.offsetMin;
        var offMax = rt.offsetMax;
        var sd = rt.sizeDelta;
        if(!Finite(offMin))
        { offMin = Vector2.zero; changed = true; }
        if(!Finite(offMax))
        { offMax = Vector2.zero; changed = true; }
        if(!Finite(sd))
        { sd = Vector2.zero; changed = true; }

        // Применить очищенные offsets (важно до SetSize…)
        if(rt.offsetMin != offMin)
        { rt.offsetMin = offMin; changed = true; }
        if(rt.offsetMax != offMax)
        { rt.offsetMax = offMax; changed = true; }

        // 4) Гарантируем разумный размер (если сейчас 0/отрицательный/некорректный)
        bool needSizeFix = sd.x <= 0f || sd.y <= 0f || !Finite(sd);

        if(needSizeFix)
        {
            Vector2 pref = GuessPreferredSize(rt);
            float w = Mathf.Max(MinW, pref.x);
            float h = Mathf.Max(MinH, pref.y);

            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
            changed = true;
        }

        // 5) Почистим явные NaN у CanvasGroup/TMP (редко но бывает)
        var cg = rt.GetComponent<CanvasGroup>();
        if(cg != null && float.IsNaN(cg.alpha))
        { cg.alpha = 1f; changed = true; }

        var tmp = rt.GetComponent<TMP_Text>();
        if(tmp != null)
        {
            tmp.ForceMeshUpdate(true, true);
            EditorUtility.SetDirty(tmp);
        }

        return changed;
    }

    // Определяем «разумный» размер:
    // - TMP_Text: GetPreferredValues по текущему тексту (или "W" если пусто)
    // - Text: preferredWidth/Height
    // - Image/RawImage: размер спрайта/тексты
    // - LayoutElement: min/preferred если заданы
    // - иначе — дефолт
    private static Vector2 GuessPreferredSize(RectTransform rt)
    {
        // 1) TMP
        var tmp = rt.GetComponent<TMP_Text>();
        if(tmp != null)
        {
            string s = string.IsNullOrEmpty(tmp.text) ? "W" : tmp.text;
            var pref = tmp.GetPreferredValues(s, float.PositiveInfinity, float.PositiveInfinity);
            if(!Finite(pref))
                pref = new Vector2(64, 22);
            return new Vector2(Mathf.Max(pref.x, MinW), Mathf.Max(pref.y, MinH));
        }

        // 2) UGUI Text
        var uguiText = rt.GetComponent<Text>();
        if(uguiText != null)
        {
            // старый Text имеет PreferredWidth/Height
            return new Vector2(Mathf.Max(uguiText.preferredWidth, MinW),
                               Mathf.Max(uguiText.preferredHeight, MinH));
        }

        // 3) Image / RawImage
        var img = rt.GetComponent<Image>();
        if(img != null && img.sprite != null)
        {
            float ppu = img.pixelsPerUnit > 0 ? img.pixelsPerUnit : 100f;
            var s = img.sprite.rect.size / ppu;
            s.x = Mathf.Max(s.x, MinW);
            s.y = Mathf.Max(s.y, MinH);
            return s;
        }
        var raw = rt.GetComponent<RawImage>();
        if(raw != null && raw.texture != null)
        {
            float w = raw.texture.width;
            float h = raw.texture.height;
            float ppu = 100f;
            return new Vector2(Mathf.Max(w / ppu, MinW), Mathf.Max(h / ppu, MinH));
        }

        // 4) LayoutElement
        var le = rt.GetComponent<LayoutElement>();
        if(le != null)
        {
            float w = le.preferredWidth > 0 ? le.preferredWidth : (le.minWidth > 0 ? le.minWidth : MinW);
            float h = le.preferredHeight > 0 ? le.preferredHeight : (le.minHeight > 0 ? le.minHeight : MinH);
            return new Vector2(w, h);
        }

        // 5) fallback
        return new Vector2(96, 32);
    }

    private static bool Finite(Vector2 v) => float.IsFinite(v.x) && float.IsFinite(v.y);
    private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    private static bool Finite(Vector2 size, bool allowZero) =>
        (allowZero ? size.x >= 0f : size.x > 0f) &&
        (allowZero ? size.y >= 0f : size.y > 0f) &&
        float.IsFinite(size.x) && float.IsFinite(size.y);

    private static Vector2 Clamp01(Vector2 v) => new Vector2(Mathf.Clamp01(v.x), Mathf.Clamp01(v.y));
}
#endif
