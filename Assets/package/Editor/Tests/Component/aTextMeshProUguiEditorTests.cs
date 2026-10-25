using System;
using System.Collections;
using System.Collections.Generic;
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
    private Type guiDebugger;
    private bool capturing;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly string[] AlignmentButtons = {
        "Left", "Center", "Right", "Justified", "Flush", "Geometry Center",
        "Top", "Middle", "Bottom", "Baseline", "Midline", "Capline"
    };

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
        StopCapture();
        if(window != null) window.Close();
        if(editor != null) Object.DestroyImmediate(editor);
        Selection.objects = selection;
        EditorSceneManager.ClosePreviewScene(preview);
    }

    [UnityTest]
    public IEnumerator AlignmentButtonsFitAndDoNotOverlapFollowingFields() {
        first.ShrinkWidth = true;
        foreach(int width in new[] { 280, 380, 620 }) {
            foreach(var alignment in new[] { HorizontalAlignmentOptions.Left, HorizontalAlignmentOptions.Flush }) {
                first.horizontalAlignment = alignment;
                editor = UnityEditor.Editor.CreateEditor(first);
                yield return DrawInspector(width, true);
                var controls = GetRecordedControls();
                Assert.That(controls.ContainsKey("Alignment"), Is.True);
                float bottom = 0;
                foreach(string button in AlignmentButtons) {
                    Assert.That(controls.ContainsKey(button), Is.True, "Missing TMP alignment button: " + button);
                    var rect = controls[button];
                    Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(0));
                    Assert.That(rect.xMax, Is.LessThanOrEqualTo(width), "Alignment button exceeds Inspector width.");
                    bottom = Mathf.Max(bottom, rect.yMax);
                    foreach(string other in AlignmentButtons)
                        // 同一グループの連結ボタンは装飾の縁が重なる。水平・垂直のグループ間を検証する。
                        if((Array.IndexOf(AlignmentButtons, button) < 6) != (Array.IndexOf(AlignmentButtons, other) < 6))
                            Assert.That(rect.Overlaps(controls[other]), Is.False, button + " overlaps " + other);
                }
                if(alignment == HorizontalAlignmentOptions.Flush) {
                    Assert.That(controls["Wrap Mix (W <-> C)"].yMin, Is.GreaterThanOrEqualTo(bottom));
                    bottom = controls["Wrap Mix (W <-> C)"].yMax;
                }
                foreach(string label in new[] { "Wrap Mode", "Overflow", "Horizontal Mapping", "Vertical Mapping", "UV Line Offset" }) {
                    Assert.That(controls[label].yMin, Is.GreaterThanOrEqualTo(bottom), label + " overlaps the preceding control at width " + width + ", " + alignment);
                    bottom = controls[label].yMax;
                }
                Assert.That(first.horizontalAlignment, Is.EqualTo(alignment));
                StopCapture();
                window.Close();
                Object.DestroyImmediate(editor);
            }
        }
    }

    [UnityTest]
    public IEnumerator AlignmentButtonsChangeOnlyTheirOwnAxis() {
        first.ShrinkWidth = true;
        first.alignment = TextAlignmentOptions.TopLeft;
        editor = UnityEditor.Editor.CreateEditor(first);
        yield return DrawInspector(380, true);
        var controls = GetRecordedControls();
        Click(controls["Right"].center);
        Assert.That(first.horizontalAlignment, Is.EqualTo(HorizontalAlignmentOptions.Right));
        Assert.That(first.verticalAlignment, Is.EqualTo(VerticalAlignmentOptions.Top));
        Click(controls["Bottom"].center);
        Assert.That(first.horizontalAlignment, Is.EqualTo(HorizontalAlignmentOptions.Right));
        Assert.That(first.verticalAlignment, Is.EqualTo(VerticalAlignmentOptions.Bottom));
        Assert.That(window.Error, Is.Null);
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

    private IEnumerator DrawInspector(int width = 620, bool capture = false) {
        window = ScriptableObject.CreateInstance<aTextMeshProUguiInspectorTestWindow>();
        window.Inspector = editor;
        window.ShowUtility();
        window.position = new Rect(100, 100, width, 1200);
        if(capture) {
            guiDebugger = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIViewDebuggerHelper");
            var parent = typeof(EditorWindow).GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
            guiDebugger.GetMethod("DebugWindow", StaticMembers).Invoke(null, new[] { parent });
            capturing = true;
        }
        double deadline = EditorApplication.timeSinceStartup + 10;
        while(window.Repaints < 2 && window.Error == null && EditorApplication.timeSinceStartup < deadline) {
            window.Repaint();
            yield return null;
        }
        Assert.That(window.Error, Is.Null);
        Assert.That(window.Repaints, Is.GreaterThanOrEqualTo(2), "Inspector did not repaint.");
    }

    private Dictionary<string, Rect> GetRecordedControls() {
        var instructionType = typeof(EditorWindow).Assembly.GetType("UnityEditor.IMGUIDrawInstruction");
        var instructions = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(instructionType));
        guiDebugger.GetMethod("GetDrawInstructions", StaticMembers).Invoke(null, new object[] { instructions, false });
        var controls = new Dictionary<string, Rect>();
        foreach(var instruction in instructions) {
            var content = (GUIContent)instructionType.GetField("usedGUIContent").GetValue(instruction);
            if(content == null) continue;
            string label = content.image != null ? content.tooltip : content.text;
            if(!string.IsNullOrEmpty(label) && !controls.ContainsKey(label)) {
                var rect = (Rect)instructionType.GetField("rect").GetValue(instruction);
                controls.Add(label, rect);
            }
        }
        return controls;
    }

    private void Click(Vector2 position) {
        window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = position });
        window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = position });
    }

    private void StopCapture() {
        if(capturing) guiDebugger.GetMethod("StopDebugging", StaticMembers).Invoke(null, null);
        capturing = false;
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
