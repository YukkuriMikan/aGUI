using System;
using System.Collections;
using System.Reflection;
using ANest.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class aTextMeshProUguiEditorTests {
    private Scene preview;
    private GameObject root;
    private aTextMeshProUgui first, second;
    private UnityEditor.Editor editor;
    private aTextMeshProUguiInspectorTestWindow window;
    private Object[] selection;

    [SetUp]
    public void SetUp() {
        preview = EditorSceneManager.NewPreviewScene();
        root = new GameObject("TMP Inspector tests", typeof(RectTransform), typeof(Canvas));
        SceneManager.MoveGameObjectToScene(root, preview);
        first = CreateText("First", "First text", "first-key", Color.red);
        second = CreateText("Second", "Second text", "second-key", Color.blue);
        selection = Selection.objects;
        Selection.objects = new Object[0];
    }

    [TearDown]
    public void TearDown() {
        if(window != null) window.Close();
        if(editor != null) Object.DestroyImmediate(editor);
        Selection.objects = selection;
        EditorSceneManager.ClosePreviewScene(preview);
    }

    [UnityTest]
    public IEnumerator RepaintingMixedSelectionPreservesUneditedValues() {
        Assert.That(Attribute.IsDefined(typeof(aTextMeshProUguiEditor), typeof(CanEditMultipleObjects)), Is.True);
        foreach(var shrink in new[] { (false, false), (false, true), (true, false), (true, true) }) {
            first.ShrinkWidth = shrink.Item1;
            second.ShrinkWidth = shrink.Item2;
            editor = UnityEditor.Editor.CreateEditor(new Object[] { first, second });
            yield return DrawInspector();
            Assert.That(first.text, Is.EqualTo("First text"));
            Assert.That(second.text, Is.EqualTo("Second text"));
            Assert.That(first.LocalizationKey, Is.EqualTo("first-key"));
            Assert.That(second.LocalizationKey, Is.EqualTo("second-key"));
            Assert.That(first.colorGradient.topLeft, Is.EqualTo(Color.red));
            Assert.That(second.colorGradient.topLeft, Is.EqualTo(Color.blue));
            Assert.That(first.ShrinkWidth, Is.EqualTo(shrink.Item1));
            Assert.That(second.ShrinkWidth, Is.EqualTo(shrink.Item2));
            window.Close();
            Object.DestroyImmediate(editor);
        }
    }

    [UnityTest]
    public IEnumerator RepaintingKeyDropdownDoesNotAssignTheFirstAvailableKey() {
        first.LocalizationKey = "";
        editor = UnityEditor.Editor.CreateEditor(first);
        // キー候補が存在しても、選択操作がない再描画では未選択を維持する。
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(aTextMeshProUguiEditor).GetField("m_keys", flags).SetValue(editor, new[] { "available-key" });
        typeof(aTextMeshProUguiEditor).GetMethod("ApplyKeyFilter", flags).Invoke(editor, null);
        yield return DrawInspector();
        Assert.That(first.LocalizationKey, Is.Empty);
    }

    private aTextMeshProUgui CreateText(string name, string body, string key, Color color) {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(root.transform, false);
        var text = go.AddComponent<aTextMeshProUgui>();
        text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        text.text = body;
        text.LocalizationKey = key;
        text.enableVertexGradient = true;
        text.colorGradient = new VertexGradient(color);
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 320);
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 160);
        return text;
    }

    private IEnumerator DrawInspector() {
        window = ScriptableObject.CreateInstance<aTextMeshProUguiInspectorTestWindow>();
        window.Inspector = editor;
        window.position = new Rect(100, 100, 620, 800);
        window.ShowUtility();
        double deadline = EditorApplication.timeSinceStartup + 10;
        while(window.Repaints < 2 && window.Error == null && EditorApplication.timeSinceStartup < deadline) {
            window.Repaint();
            yield return null;
        }
        Assert.That(window.Error, Is.Null);
        Assert.That(window.Repaints, Is.GreaterThanOrEqualTo(2), "Inspector did not repaint.");
    }
}

internal sealed class aTextMeshProUguiInspectorTestWindow : EditorWindow {
    internal UnityEditor.Editor Inspector;
    internal int Repaints;
    internal Exception Error;
    private Vector2 scroll;

    private void OnGUI() {
        if(Inspector == null) return;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try {
            Inspector.OnInspectorGUI();
            if(Event.current.type == EventType.Repaint) Repaints++;
        } catch(Exception exception) {
            Error = exception;
        } finally {
            EditorGUILayout.EndScrollView();
        }
    }
}
