using System;
using System.Collections;
using ANest.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class aGuiRubyRegressionTests {
    private GameObject root;
    private aTextMeshProUgui text;

    [SetUp] public void SetUp() {
        root = new GameObject("Ruby regression", typeof(RectTransform), typeof(Canvas));
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(root.transform, false);
        text = go.AddComponent<aTextMeshProUgui>();
        text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        text.fontSize = 30;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 150);
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 500);
    }

    [TearDown] public void TearDown() => Object.DestroyImmediate(root);

    private aGuiRubyMeshTestUtility.Reading Ruby() => aGuiRubyMeshTestUtility.Get(text);

    [TestCase(false)]
    [TestCase(true)]
    public void PreRenderChangesSurviveAndInvalidateRubyLayout(bool enableCycle) {
        text.text = "<ruby=abc>ABC</ruby>";
        text.maxVisibleCharacters = 3;
        bool invoked = false;
        Action<TMP_TextInfo> callback = info => {
            if(invoked) return;
            invoked = true;
            Assert.That(text.maxVisibleCharacters, Is.EqualTo(3));
            Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
            text.fontSize = 60;
            text.maxVisibleCharacters = int.MaxValue;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
        };
        text.OnPreRenderText += callback;
        try {
            if(enableCycle) { text.enabled = false; text.enabled = true; }
            text.ForceMeshUpdate();
            Assert.That(invoked, Is.True);
            Assert.That(text.fontSize, Is.EqualTo(60));
            Assert.That(text.maxVisibleCharacters, Is.EqualTo(int.MaxValue));
            Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
            Assert.That(text.overflowMode, Is.EqualTo(TextOverflowModes.Truncate));
            text.ForceMeshUpdate();
            int first = text.textInfo.linkInfo[0].linkTextfirstCharacterIndex;
            Assert.That(text.textInfo.characterInfo[first].pointSize, Is.EqualTo(60));
            Assert.That(Ruby().bounds.size.x, Is.GreaterThan(0));
        } finally { text.OnPreRenderText -= callback; }
    }

    [UnityTest] public IEnumerator PreRenderChangeUpdatesOnNextFrameWithoutForcedRebuild() {
        text.text = "<ruby=abc>ABC</ruby>";
        bool changed = false;
        Action<TMP_TextInfo> callback = info => {
            if(changed) return;
            changed = true;
            text.fontSize = 40;
        };
        text.OnPreRenderText += callback;
        try {
            text.ForceMeshUpdate();
            yield return null;
            yield return null;
            Assert.That(text.fontSize, Is.EqualTo(40));
            int first = text.textInfo.linkInfo[0].linkTextfirstCharacterIndex;
            Assert.That(text.textInfo.characterInfo[first].pointSize, Is.EqualTo(40));
        } finally { text.OnPreRenderText -= callback; }
    }

    [TestCase(TextAlignmentOptions.TopLeft)]
    [TestCase(TextAlignmentOptions.Center)]
    [TestCase(TextAlignmentOptions.BottomRight)]
    [TestCase(TextAlignmentOptions.Justified)]
    public void RubyMeshPreservesBodyGeometryAndLayoutAcrossAlignmentAndResize(TextAlignmentOptions alignment) {
        var control = CreateControl();
        text.alignment = control.alignment = alignment;
        text.text = "<u>AAAA</u> <ruby=abc><b>BC</b> <color=red>DE</color></ruby> ZZ";
        control.text = "<u>AAAA</u> \u200B<nobr><link=ordinary><b>BC</b> <color=red>DE</color></link></nobr>\u200B ZZ";
        foreach(float width in new[] { 150f, 500f, 170f }) {
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            control.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            text.ForceMeshUpdate();
            control.ForceMeshUpdate();
            AssertBodyMatches(control);
            Assert.That(text.textInfo.meshInfo[0].vertexCount, Is.GreaterThan(control.textInfo.meshInfo[0].vertexCount));
            Assert.That(Ruby().bounds.size.x, Is.GreaterThan(0));
            Assert.That(text.transform.childCount, Is.Zero);
            Assert.That(text.preferredHeight, Is.EqualTo(control.preferredHeight).Within(.01f));
            Assert.That(text.GetPreferredValues(150, 500).y, Is.EqualTo(control.GetPreferredValues(150, 500).y).Within(.01f));
        }
    }

    [TestCase(RubySizeMode.Auto)]
    [TestCase(RubySizeMode.Scale)]
    [TestCase(RubySizeMode.Size)]
    public void RubyModesAndOffsetChangeActualMeshWithoutMovingBody(RubySizeMode mode) {
        text.text = "<ruby=abc>ABC</ruby>";
        text.RubySizeMode = mode;
        text.RubyScale = .4f;
        text.RubySize = 12;
        text.ForceMeshUpdate();
        var before = (Vector3[])text.textInfo.meshInfo[0].vertices.Clone();
        var reading = Ruby();
        Assert.That(reading.fontSize, Is.GreaterThan(0));
        text.RubyOffset += 15;
        for(int i = 0; i < 12; i++) Assert.That(text.textInfo.meshInfo[0].vertices[i], Is.EqualTo(before[i]));
        for(int i = 12; i < 24; i++) Assert.That(Vector3.Distance(text.textInfo.meshInfo[0].vertices[i], before[i] + Vector3.up * 15), Is.LessThan(.001f));
        Assert.That(Ruby().bounds.center.y, Is.EqualTo(reading.bounds.center.y + 15).Within(.001f));
        Assert.That(text.mesh.vertexCount, Is.GreaterThanOrEqualTo(24));
    }

    [Test] public void RubyDoesNotInfluenceAutoSizeAndScaleChangesRefreshSdfData() {
        var control = CreateControl();
        text.enableAutoSizing = control.enableAutoSizing = true;
        text.fontSizeMin = control.fontSizeMin = 8;
        text.fontSizeMax = control.fontSizeMax = 40;
        text.text = "AAAA <ruby=abcdefghijk>BC DE</ruby> ZZ";
        control.text = "AAAA \u200B<nobr><link=ordinary>BC DE</link></nobr>\u200B ZZ";
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 35);
        control.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 35);
        foreach(float scale in new[] { 1f, 2f, .5f }) {
            root.transform.localScale = Vector3.one * scale;
            text.ForceMeshUpdate();
            control.ForceMeshUpdate();
            Assert.That(text.fontSize, Is.EqualTo(control.fontSize).Within(.01f));
            AssertBodyMatches(control);
        }
    }

    [Test] public void MaxVisibleCharactersHidesRubyUntilBodyIsVisibleAndRemovalClearsVertices() {
        text.text = "<ruby=abc>ABC</ruby>";
        text.maxVisibleCharacters = 2;
        text.ForceMeshUpdate();
        Assert.That(Ruby().bounds.size, Is.EqualTo(Vector3.zero));
        text.maxVisibleCharacters = int.MaxValue;
        text.ForceMeshUpdate();
        Assert.That(Ruby().bounds.size.x, Is.GreaterThan(0));
        text.text = "ABC";
        text.ForceMeshUpdate();
        Assert.That(Ruby(), Is.Null);
        Assert.That(text.textInfo.meshInfo[0].vertexCount, Is.EqualTo(12));
        for(int i = 12; i < text.textInfo.meshInfo[0].vertices.Length; i++)
            Assert.That(text.textInfo.meshInfo[0].vertices[i], Is.EqualTo(Vector3.zero));
    }

    [TestCase(TextOverflowModes.Ellipsis)]
    [TestCase(TextOverflowModes.Truncate)]
    [TestCase(TextOverflowModes.Page)]
    public void RubyKeepsBodyOverflowAndPageMetadata(TextOverflowModes mode) {
        var control = CreateControl();
        text.overflowMode = control.overflowMode = mode;
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 35);
        control.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 35);
        text.text = "AAAA <ruby=abc>BC DE</ruby> ZZ";
        control.text = "AAAA \u200B<nobr><link=ordinary>BC DE</link></nobr>\u200B ZZ";
        text.ForceMeshUpdate();
        control.ForceMeshUpdate();
        AssertBodyMatches(control);
        Assert.That(text.isTextTruncated, Is.EqualTo(control.isTextTruncated));
        Assert.That(text.firstOverflowCharacterIndex, Is.EqualTo(control.firstOverflowCharacterIndex));
        Assert.That(text.textInfo.pageCount, Is.EqualTo(control.textInfo.pageCount));
    }

    [Test] public void RubyFallbackUsesTmpMaterialAndDoesNotCreateRubyObjects() {
        var primary = Object.Instantiate(text.font);
        var material = Object.Instantiate(text.font.material);
        primary.material = material;
        primary.characterTable.RemoveAll(character => character.unicode == 'x');
        primary.atlasPopulationMode = AtlasPopulationMode.Static;
        primary.ReadFontAssetDefinition();
        primary.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { text.font };
        try {
            text.font = primary;
            text.text = "<ruby=xxx>ABC</ruby>";
            text.ForceMeshUpdate();
            Assert.That(text.textInfo.materialCount, Is.GreaterThan(1));
            Assert.That(text.textInfo.meshInfo[1].vertexCount, Is.EqualTo(12));
            Assert.That(Ruby().bounds.size.x, Is.GreaterThan(0));
            foreach(Transform child in text.transform) Assert.That(child.GetComponent<TMP_SubMeshUI>(), Is.Not.Null);
            text.text = "ABC";
            text.ForceMeshUpdate();
            Assert.That(Ruby(), Is.Null);
        } finally {
            // 複製したFontAssetが共有アトラスを所有しているわけではない。
            primary.atlasTextures = Array.Empty<Texture2D>();
            Object.DestroyImmediate(primary);
            Object.DestroyImmediate(material);
        }
    }

    private TextMeshProUGUI CreateControl() {
        var go = new GameObject("Control", typeof(RectTransform));
        go.transform.SetParent(root.transform, false);
        var control = go.AddComponent<TextMeshProUGUI>();
        control.font = text.font;
        control.fontSize = 30;
        control.textWrappingMode = TextWrappingModes.Normal;
        control.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 150);
        control.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 500);
        return control;
    }

    [UnityTest] public IEnumerator RubyUsesParentRectMaskWithoutChildGraphics() {
        var mask = new GameObject("Clip", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D));
        mask.transform.SetParent(root.transform, false);
        var rect = mask.GetComponent<RectTransform>();
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 60);
        text.transform.SetParent(mask.transform, false);
        text.alignment = TextAlignmentOptions.Center;
        text.text = "<ruby=abc>ABC</ruby>";
        yield return null;
        Canvas.ForceUpdateCanvases();
        Assert.That(text.canvasRenderer.hasRectClipping, Is.True);
        Assert.That(text.transform.childCount, Is.Zero);
        Assert.That(Ruby().bounds.size.x, Is.GreaterThan(0));
    }

    [UnityTest] public IEnumerator LegacyGeneratedRubyIsRemovedWithoutDeletingAuthoredChildren() {
        text.enabled = false;
        var legacy = new GameObject("Ruby_0", typeof(RectTransform), typeof(TextMeshProUGUI));
        legacy.transform.SetParent(text.transform, false);
        legacy.hideFlags = HideFlags.NotEditable;
        var authored = new GameObject("Ruby_Decoration", typeof(RectTransform), typeof(TextMeshProUGUI));
        authored.transform.SetParent(text.transform, false);
        text.text = "<ruby=abc>ABC</ruby>";
        text.enabled = true;
        yield return null;
        Assert.That(legacy == null, Is.True);
        Assert.That(authored != null, Is.True);
        Assert.That(text.transform.childCount, Is.EqualTo(1));
        Assert.That(Ruby().bounds.size.x, Is.GreaterThan(0));
    }

    private void AssertBodyMatches(TextMeshProUGUI control) {
        Assert.That(text.textInfo.characterCount, Is.EqualTo(control.textInfo.characterCount));
        Assert.That(text.textInfo.lineCount, Is.EqualTo(control.textInfo.lineCount));
        for(int i = 0; i < control.textInfo.characterCount; i++) {
            Assert.That(text.textInfo.characterInfo[i].character, Is.EqualTo(control.textInfo.characterInfo[i].character));
            Assert.That(text.textInfo.characterInfo[i].lineNumber, Is.EqualTo(control.textInfo.characterInfo[i].lineNumber));
        }
        for(int i = 0; i < control.textInfo.meshInfo[0].vertexCount; i++) {
            Assert.That(Vector3.Distance(text.textInfo.meshInfo[0].vertices[i], control.textInfo.meshInfo[0].vertices[i]), Is.LessThan(.001f), "Body vertex " + i);
            Assert.That(text.textInfo.meshInfo[0].uvs0[i].w, Is.EqualTo(control.textInfo.meshInfo[0].uvs0[i].w).Within(.001f));
            if(control.textInfo.meshInfo[0].vertices[i] != Vector3.zero)
                Assert.That(text.textInfo.meshInfo[0].colors32[i], Is.EqualTo(control.textInfo.meshInfo[0].colors32[i]));
        }
    }

    [TestCase("<ruby=\"abc\">ABC</ruby>")]
    [TestCase("<ruby='abc'>ABC</ruby>")]
    [TestCase("<RUBY=abc>ABC</RUBY>")]
    public void CustomRubyRendersWithoutChangingAuthoredText(string source) {
        text.text = source;
        for(var i = 0; i < 3; i++) {
            text.ForceMeshUpdate();
            Assert.That(text.text, Is.EqualTo(source));
            Assert.That(Ruby().text, Is.EqualTo("abc"));
        }
        text.enabled = false;
        text.enabled = true;
        Assert.That(text.text, Is.EqualTo(source));
        Assert.That(Ruby().text, Is.EqualTo("abc"));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void CustomRubyWorksWithEveryTextInput(int input) {
        const string source = "AAAA <ruby=\"abc\">BC DE</ruby> ZZ";
        if(input == 0) text.text = source;
        else if(input == 1) text.SetText(source);
        else if(input == 2) text.SetText(source.ToCharArray());
        else text.SetText("AAAA <ruby=\"abc\">BC {0}</ruby> ZZ", 12f);
        text.ForceMeshUpdate();
        AssertSingleLine(1);
        Assert.That(Ruby().text, Is.EqualTo("abc"));
        Assert.That(text.text, Is.EqualTo(input == 3 ? "AAAA <ruby=\"abc\">BC 12</ruby> ZZ" : source));
    }

    [Test] public void PartiallyTypedCustomRubyNeverDeletesOrRewritesInput() {
        const string source = "<ruby=\"abc\">ABC</ruby>";
        for(var length = 0; length <= source.Length; length++) {
            var partial = source.Substring(0, length);
            text.text = partial;
            Assert.DoesNotThrow(() => text.ForceMeshUpdate());
            Assert.That(text.text, Is.EqualTo(partial));
            if(length < source.Length) Assert.That(Ruby(), Is.Null);
        }
        Assert.That(Ruby().text, Is.EqualTo("abc"));
        text.text = source.Substring(0, source.Length - 1);
        text.ForceMeshUpdate();
        Assert.That(Ruby(), Is.Null);
        Assert.That(text.text, Is.EqualTo(source.Substring(0, source.Length - 1)));
    }

    [Test] public void CustomAndLegacyRubyCoexistAndHonorEmptyBodyAndNoParse() {
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 800);
        text.text = "<ruby=\"empty\"></ruby><ruby=\"abc\"><b>A</b>\nB<br>C</ruby> <link=\"ruby:xyz\">XYZ</link>";
        var source = text.text;
        text.ForceMeshUpdate();
        Assert.That(Ruby().text, Is.EqualTo("abc"));
        Assert.That(aGuiRubyMeshTestUtility.Get(text, 1).text, Is.EqualTo("xyz"));
        Assert.That(text.textInfo.linkInfo[1].linkTextLength, Is.EqualTo(3));
        Assert.That(text.text, Is.EqualTo(source));
        const string literal = "<noparse><ruby=\"abc\">ABC</ruby></noparse>";
        text.text = literal;
        text.ForceMeshUpdate();
        Assert.That(Ruby(), Is.Null);
        Assert.That(text.textPreprocessor.PreprocessText(literal), Is.EqualTo(literal));
        text.richText = false;
        text.text = "<ruby=abc>ABC</ruby>";
        text.ForceMeshUpdate();
        Assert.That(Ruby(), Is.Null);
        Assert.That(text.textPreprocessor.PreprocessText(text.text), Is.EqualTo(text.text));
    }

    [Test] public void LongCustomRubyWarnsAndPreservesOriginalTag() {
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 50);
        const string source = "<ruby=\"abc\">BC DE</ruby>";
        text.text = source;
        LogAssert.Expect(LogType.Warning, "[aTextMeshProUgui] ルビ本文が1行の幅を超えるため、途中で分割せず横にはみ出して表示します。");
        text.ForceMeshUpdate();
        AssertSingleLine(0);
        Assert.That(text.text, Is.EqualTo(source));
        Assert.That(Ruby().fontSize, Is.GreaterThan(0));
    }

#if UNITY_EDITOR
    [UnityTest] public IEnumerator InspectorEditsDuringPlayUpdateWithoutForcingMeshOrReplacingSource() {
        const string initial = "<ruby=\"abc\">ABC</ruby>";
        const string changed = "<ruby=\"xyz\">XYZ</ruby>";
        var serialized = new UnityEditor.SerializedObject(text);
        serialized.FindProperty("m_text").stringValue = initial;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        yield return null;
        yield return null;
        serialized.Update();
        Assert.That(serialized.FindProperty("m_text").stringValue, Is.EqualTo(initial));
        Assert.That(Ruby().text, Is.EqualTo("abc"));
        serialized.FindProperty("m_text").stringValue = changed;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        yield return null;
        yield return null;
        serialized.Update();
        Assert.That(serialized.FindProperty("m_text").stringValue, Is.EqualTo(changed));
        Assert.That(Ruby().text, Is.EqualTo("xyz"));
        Assert.That(text.text, Is.EqualTo(changed));
    }

    [Test] public void PrefabSaveReloadAndInstantiationKeepCustomSource() {
        const string source = "<ruby=\"abc\">ABC</ruby>";
        string path = "Assets/aGUI_RubyAudit_" + Guid.NewGuid().ToString("N") + ".prefab";
        GameObject clone = null;
        try {
            text.text = source;
            text.ForceMeshUpdate();
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceUpdate);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var saved = prefab.GetComponentInChildren<aTextMeshProUgui>();
            Assert.That(new UnityEditor.SerializedObject(saved).FindProperty("m_text").stringValue, Is.EqualTo(source));
            clone = Object.Instantiate(prefab);
            var instance = clone.GetComponentInChildren<aTextMeshProUgui>();
            instance.ForceMeshUpdate();
            Assert.That(instance.text, Is.EqualTo(source));
            Assert.That(aGuiRubyMeshTestUtility.Get(instance).text, Is.EqualTo("abc"));
        } finally {
            if(clone != null) Object.DestroyImmediate(clone);
            UnityEditor.AssetDatabase.DeleteAsset(path);
        }
    }
#endif

    [TestCase("")]
    [TestCase("<b></b>")]
    [TestCase("\n<br>\r\n")]
    public void EmptyRubyBodyIsExcluded(string body) {
        text.text = "<link=\"ruby:abc\">ABC</link>";
        text.ForceMeshUpdate();
        Assert.That(Ruby(), Is.Not.Null);
        text.text = "<link=\"ruby:abc\">" + body + "</link>";
        Assert.DoesNotThrow(() => text.ForceMeshUpdate());
        Assert.That(Ruby(), Is.Null);
        text.text += "<link=\"ruby:xyz\">XYZ</link>";
        text.ForceMeshUpdate();
        Assert.That(Ruby().text, Is.EqualTo("xyz"), "Empty links must not consume a ruby slot.");
    }

    [UnityTest] public IEnumerator DisableAndEnableWithinFrameKeepsRubyWithoutCreatingObjects() {
        text.text = "<link=\"ruby:abc\">ABC</link>";
        text.ForceMeshUpdate();
        Assert.That(text.transform.childCount, Is.Zero);
        text.enabled = false;
        text.enabled = true;
        Assert.That(text.transform.childCount, Is.Zero);
        yield return null;
        Assert.That(text.transform.childCount, Is.Zero);
        Assert.DoesNotThrow(() => text.ForceMeshUpdate());
        Assert.That(Ruby().text, Is.EqualTo("abc"));
        text.gameObject.SetActive(false);
        text.gameObject.SetActive(true);
        yield return null;
        Assert.DoesNotThrow(() => text.ForceMeshUpdate());
        Assert.That(Ruby().isActiveAndEnabled, Is.True);
    }

    [TestCase("\n")]
    [TestCase("\r\n")]
    [TestCase("<br>")]
    [TestCase("\\n")]
    public void ExplicitBreakInsideRubyIsIgnoredButSourceAndOutsideBreakArePreserved(string lineBreak) {
        var source = "A\n<link=\"ruby:abc\">B" + lineBreak + "C</link>";
        text.text = source;
        text.ForceMeshUpdate();
        Assert.That(text.text, Is.EqualTo(source));
        var link = text.textInfo.linkInfo[0];
        Assert.That(link.linkTextLength, Is.EqualTo(2));
        var first = text.textInfo.characterInfo[link.linkTextfirstCharacterIndex];
        var last = text.textInfo.characterInfo[link.linkTextfirstCharacterIndex + 1];
        Assert.That(first.lineNumber, Is.EqualTo(1));
        Assert.That(last.lineNumber, Is.EqualTo(first.lineNumber));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void RubyBodyMovesTogetherToNextLineForEveryTextInput(int input) {
        const string source = "AAAA <link=\"ruby:abc\">BC DE</link> ZZ";
        if(input == 0) text.text = source;
        else if(input == 1) text.SetText(source);
        else if(input == 2) text.SetText(source.ToCharArray());
        else text.SetText("AAAA <link=\"ruby:abc\">BC {0}</link> ZZ", 12f);
        text.ForceMeshUpdate();
        AssertSingleLine(1);
        Assert.That(Ruby().fontSize, Is.GreaterThan(0));
    }

    [Test] public void WiderThanLineRubyOverflowsOnOwnLineAndReturnsWhenResized() {
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 50);
        text.text = "A <link=\"ruby:abc\">BC DE</link> Z";
        LogAssert.Expect(LogType.Warning, "[aTextMeshProUgui] ルビ本文が1行の幅を超えるため、途中で分割せず横にはみ出して表示します。");
        text.ForceMeshUpdate();
        AssertSingleLine();
        Assert.That(Ruby().fontSize, Is.GreaterThan(0));
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 500);
        text.ForceMeshUpdate();
        AssertSingleLine(0);
    }

    [Test] public void RubySupportsFormattingAndLeavesOrdinaryLinksAndNoParseAlone() {
        text.text = "AAAA <link='ruby:abc'><b>BC</b> <color=red>DE</color></link>";
        text.ForceMeshUpdate();
        AssertSingleLine(1);
        text.text = "<link=ordinary>B\nC</link>";
        text.ForceMeshUpdate();
        Assert.That(Ruby(), Is.Null);
        var first = text.textInfo.linkInfo[0].linkTextfirstCharacterIndex;
        Assert.That(text.textInfo.characterInfo[first].lineNumber, Is.Not.EqualTo(text.textInfo.characterInfo[first + 2].lineNumber));
        const string literal = "<noparse><link=\"ruby:abc\">BC</link></noparse>";
        text.text = literal;
        text.ForceMeshUpdate();
        Assert.That(text.textPreprocessor.PreprocessText(literal), Is.EqualTo(literal));
    }

    [TestCase("<link=\"ruby:abc\">BC DE</link>", 1)]
    [TestCase("<link=\"ruby:abc\">BC DE</link><link=\"ruby:xyz\">FG HI</link>", 2)]
    [TestCase("A\n<link=\"ruby:abc\">BC DE</link>\nZ", 3)]
    public void OverflowRubyPreservesParagraphsWithoutExtraEmptyLines(string source, int lines) {
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 50);
        text.text = source;
        int warnings = 0;
        Application.LogCallback onLog = (message, stack, type) => { if(type == LogType.Warning && message.StartsWith("[aTextMeshProUgui]")) warnings++; };
        Application.logMessageReceived += onLog;
        try {
            LogAssert.Expect(LogType.Warning, "[aTextMeshProUgui] ルビ本文が1行の幅を超えるため、途中で分割せず横にはみ出して表示します。");
            text.ForceMeshUpdate();
            Assert.That(text.textInfo.lineCount, Is.EqualTo(lines));
            AssertSingleLine(source.StartsWith("A") ? 1 : 0);
            text.ForceMeshUpdate();
            Assert.That(warnings, Is.EqualTo(1), "Unchanged redraws must not repeat the warning.");
            Assert.That(text.preferredHeight, Is.LessThan(50 * lines));
        } finally { Application.logMessageReceived -= onLog; }
    }

    private sealed class ChangingPreprocessor : ITextPreprocessor {
        public string Ruby = "abc";
        public string PreprocessText(string source) => "AAAA <link=\"ruby:" + Ruby + "\">BC DE</link>";
    }

    [Test] public void ExistingPreprocessorStillRunsAndCanChangeRubyWithoutSourceChange() {
        var processor = new ChangingPreprocessor();
        text.textPreprocessor = processor;
        text.text = "Token";
        text.ForceMeshUpdate();
        AssertSingleLine(1);
        Assert.That(Ruby().text, Is.EqualTo("abc"));
        processor.Ruby = "xyz";
        text.ForceMeshUpdate();
        Assert.That(Ruby().text, Is.EqualTo("xyz"));
    }

    [Test] public void MiddleMouseDoesNotBlockLeftClickOrCancelAcceptedPress() {
        var go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(root.transform, false);
        var button = go.AddComponent<aButton>();
        int clicks = 0;
        button.onClick.AddListener(() => clicks++);
        var middle = new PointerEventData(null) { button = PointerEventData.InputButton.Middle };
        var left = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
        button.OnPointerDown(middle);
        button.OnPointerUp(middle);
        button.OnPointerDown(left);
        button.OnPointerDown(middle);
        button.OnPointerUp(middle);
        button.OnPointerUp(left);
        button.OnPointerClick(left);
        Assert.That(clicks, Is.EqualTo(1));
        button.OnPointerDown(left);
        button.OnPointerUp(left);
        button.OnPointerClick(left);
        Assert.That(clicks, Is.EqualTo(1), "Valid clicks must still apply the input guard.");
    }

    private void AssertSingleLine(int? expectedLine = null) {
        var link = text.textInfo.linkInfo[0];
        int first = link.linkTextfirstCharacterIndex;
        int line = text.textInfo.characterInfo[first].lineNumber;
        if(expectedLine.HasValue) Assert.That(line, Is.EqualTo(expectedLine.Value));
        for(var i = first; i < first + link.linkTextLength; i++)
            Assert.That(text.textInfo.characterInfo[i].lineNumber, Is.EqualTo(line));
    }
}

// 生成物を実際の合成済みメッシュと照合するためのテスト専用アクセス。
internal static class aGuiRubyMeshTestUtility {
    internal sealed class Reading {
        internal string text;
        internal float fontSize;
        internal Bounds bounds;
        internal bool isActiveAndEnabled;
    }
    private const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
    internal static Reading Get(aTextMeshProUgui text, int index = 0) {
        var layout = typeof(aTextMeshProUgui).GetField("m_rubyMesh", Flags).GetValue(text);
        int count = (int)layout.GetType().GetProperty("Count", Flags).GetValue(layout);
        if(index >= count) return null;
        var runs = (System.Collections.IList)layout.GetType().GetField("m_runs", Flags).GetValue(layout);
        var run = runs[index];
        var type = run.GetType();
        return new Reading {
            text = (string)type.GetField("Text", Flags).GetValue(run),
            fontSize = (float)type.GetField("FontSize", Flags).GetValue(run),
            bounds = (Bounds)type.GetField("Bounds", Flags).GetValue(run),
            isActiveAndEnabled = text.isActiveAndEnabled,
        };
    }
}
