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

    private TextMeshProUGUI Ruby() => text.transform.Find("Ruby_0")?.GetComponent<TextMeshProUGUI>();

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

    [UnityTest] public IEnumerator DisableAndEnableWithinFrameDoesNotReuseDestroyedRuby() {
        text.text = "<link=\"ruby:abc\">ABC</link>";
        text.ForceMeshUpdate();
        var first = Ruby();
        text.enabled = false;
        text.enabled = true;
        Assert.That(Ruby(), Is.Not.SameAs(first));
        yield return null;
        Assert.That(first == null, Is.True);
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
