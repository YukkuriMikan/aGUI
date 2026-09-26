using System;
using ANest.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

public class aGuiShrinkWidthTests {
    private GameObject root;
    private aTextMeshProUgui text;

    [SetUp] public void SetUp() {
        root = new GameObject("Shrink Width test", typeof(RectTransform), typeof(Canvas));
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(root.transform, false);
        text = go.AddComponent<aTextMeshProUgui>();
        text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        text.fontSize = 30;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 150);
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 200);
    }

    [TearDown] public void TearDown() => Object.DestroyImmediate(root);

    [TestCase(TextAlignmentOptions.TopLeft)]
    [TestCase(TextAlignmentOptions.Center)]
    [TestCase(TextAlignmentOptions.BottomRight)]
    public void FitsAllVerticesAndPreservesHeightAndContainer(TextAlignmentOptions alignment) {
        text.alignment = alignment;
        text.margin = new Vector4(11, 2, 17, 3);
        text.text = "<u>AAAA BBBB CCCC</u>";
        text.ForceMeshUpdate();
        var before = CopyVertices();
        var rect = text.rectTransform.rect;
        var transformScale = text.transform.localScale;
        text.ShrinkWidth = true;
        text.ForceMeshUpdate();
        AssertFits();
        Assert.That(text.textInfo.lineCount, Is.EqualTo(1));
        Assert.That(text.fontSize, Is.EqualTo(30));
        Assert.That(text.rectTransform.rect, Is.EqualTo(rect));
        Assert.That(text.transform.localScale, Is.EqualTo(transformScale));
        AssertYUnchanged(before);
        Assert.That(text.textInfo.characterInfo[0].aspectRatio, Is.GreaterThan(0));
        Assert.That(Bounds().size.x, Is.EqualTo(122).Within(.02f));
    }

    [Test] public void ShortTextIsNotEnlarged() {
        text.text = "ABC";
        text.alignment = TextAlignmentOptions.Center;
        text.ForceMeshUpdate();
        var before = CopyVertices();
        text.ShrinkWidth = true;
        text.ForceMeshUpdate();
        AssertVerticesEqual(before);
    }

    [TestCase(RubySizeMode.Auto)]
    [TestCase(RubySizeMode.Scale)]
    [TestCase(RubySizeMode.Size)]
    public void RubyFitsWithBodyAndRepeatedUpdatesDoNotAccumulate(RubySizeMode mode) {
        text.RubySizeMode = mode;
        text.RubySize = 25;
        text.text = "<ruby=abcdefghijklmnop>ABC</ruby> DEFGHIJKLMNOP";
        text.ForceMeshUpdate();
        var original = CopyVertices();
        text.ShrinkWidth = true;
        text.ForceMeshUpdate();
        AssertFits();
        AssertYUnchanged(original);
        var first = CopyVertices();
        for(int i = 0; i < 3; i++) { text.ForceMeshUpdate(); AssertVerticesEqual(first); }
        var ruby = aGuiRubyMeshTestUtility.Get(text);
        Assert.That(ruby.bounds.min.x, Is.GreaterThanOrEqualTo(text.rectTransform.rect.xMin - .01f));
        Assert.That(ruby.bounds.max.x, Is.LessThanOrEqualTo(text.rectTransform.rect.xMax + .01f));
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 95);
        text.ForceMeshUpdate();
        AssertFits();
        text.ShrinkWidth = false;
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 150);
        text.ForceMeshUpdate();
        AssertVerticesEqual(original);
    }

    [Test] public void ExplicitNewlinesKeepUniformHorizontalScale() {
        text.text = "ABCDEFGHIJKLMN\nABC";
        text.alignment = TextAlignmentOptions.Center;
        text.ForceMeshUpdate();
        float width0 = CharacterWidth(0), width1 = CharacterWidth(15);
        var before = CopyVertices();
        text.ShrinkWidth = true;
        text.ForceMeshUpdate();
        Assert.That(text.textInfo.lineCount, Is.EqualTo(2));
        Assert.That(CharacterWidth(0) / width0, Is.EqualTo(CharacterWidth(15) / width1).Within(.001f));
        AssertYUnchanged(before);
        AssertFits();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ToggleAndSerializationRestoreOriginalSettings(bool autoSize) {
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.enableAutoSizing = autoSize;
        text.text = "AAAA BBBB CCCC DDDD";
        text.ShrinkWidth = true;
        text.ForceMeshUpdate();
        Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
        Assert.That(text.overflowMode, Is.EqualTo(TextOverflowModes.Overflow));
        Assert.That(text.enableAutoSizing, Is.False);
        Assert.That(text.fontSize, Is.EqualTo(30));
        Assert.That(text.textInfo.lineCount, Is.EqualTo(1));
        text.enabled = false;
        text.enabled = true;
        // このテストはLocalization設定を持たない。複製時のテーブル購読を避けて保存値のみ検証する。
        text.gameObject.SetActive(false);
        var clone = Object.Instantiate(text.gameObject, root.transform).GetComponent<aTextMeshProUgui>();
        clone.ShrinkWidth = false;
        text.ShrinkWidth = false;
        foreach(var item in new[] { clone, text }) {
            Assert.That(item.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
            Assert.That(item.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
            Assert.That(item.enableAutoSizing, Is.EqualTo(autoSize));
        }
    }

    [TestCase(0f)]
    [TestCase(20f)]
    public void EmptyAndNonPositiveAvailableWidthAreFinite(float width) {
        text.ShrinkWidth = true;
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        text.margin = new Vector4(10, 0, 10, 0);
        foreach(string value in new[] { "", "   ", "ABCDEFGHIJK", "<ruby=abc>ABC</ruby>" }) {
            text.text = value;
            text.ForceMeshUpdate();
            foreach(var mesh in CopyVertices()) foreach(var v in mesh) {
                Assert.That(float.IsNaN(v.x) || float.IsInfinity(v.x), Is.False);
            }
        }
    }

    [Test] public void LinkHitTestingUsesCompressedCharacterPositions() {
        text.text = "ABCDEFGHI <link=target>JKLMN</link>";
        text.ShrinkWidth = true;
        text.ForceMeshUpdate();
        var link = text.textInfo.linkInfo[0];
        var first = text.textInfo.characterInfo[link.linkTextfirstCharacterIndex];
        var last = text.textInfo.characterInfo[link.linkTextfirstCharacterIndex + link.linkTextLength - 1];
        var local = (first.bottomLeft + last.topRight) * .5f;
        var screen = RectTransformUtility.WorldToScreenPoint(null, text.transform.TransformPoint(local));
        Assert.That(TMP_TextUtilities.FindIntersectingLink(text, screen, null), Is.EqualTo(0));
        AssertFits();
        Assert.That(last.topRight.x, Is.LessThanOrEqualTo(text.rectTransform.rect.xMax + .01f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DontRenderStillReturnsCompressedGeometry(bool ruby) {
        text.text = ruby ? "<ruby=abc>ABCDEFGHIJKLMN</ruby>" : "ABCDEFGHIJKLMN";
        text.renderMode = TextRenderFlags.DontRender;
        text.ShrinkWidth = true;
        text.ForceMeshUpdate();
        AssertFits();
        Assert.That(Bounds().size.x, Is.EqualTo(150).Within(.02f));
    }

    [Test] public void ShorteningTextAndDisablingShrinkRemoveCompression() {
        text.alignment = TextAlignmentOptions.Center;
        text.ShrinkWidth = true;
        text.text = "ABCDEFGHIJKLMN";
        text.ForceMeshUpdate();
        text.SetText("ABC");
        text.ForceMeshUpdate();
        var shortText = CopyVertices();
        text.ShrinkWidth = false;
        text.ForceMeshUpdate();
        AssertVerticesEqual(shortText);
    }

    private float CharacterWidth(int i) => text.textInfo.characterInfo[i].topRight.x - text.textInfo.characterInfo[i].topLeft.x;

    private Vector3[][] CopyVertices() {
        var result = new Vector3[text.textInfo.materialCount][];
        for(int i = 0; i < result.Length; i++) {
            var mesh = text.textInfo.meshInfo[i];
            result[i] = new Vector3[mesh.vertexCount];
            Array.Copy(mesh.vertices, result[i], mesh.vertexCount);
        }
        return result;
    }

    private Bounds Bounds() {
        Bounds bounds = default;
        bool any = false;
        foreach(var mesh in CopyVertices()) for(int i = 0; i + 3 < mesh.Length; i += 4) {
            if(mesh[i] == mesh[i + 1] && mesh[i] == mesh[i + 2] && mesh[i] == mesh[i + 3]) continue;
            for(int c = 0; c < 4; c++) {
                if(any) bounds.Encapsulate(mesh[i + c]);
                else { bounds = new Bounds(mesh[i + c], Vector3.zero); any = true; }
            }
        }
        return bounds;
    }

    private void AssertFits() {
        var bounds = Bounds();
        Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(text.rectTransform.rect.xMin + text.margin.x - .02f));
        Assert.That(bounds.max.x, Is.LessThanOrEqualTo(text.rectTransform.rect.xMax - text.margin.z + .02f));
    }

    private void AssertYUnchanged(Vector3[][] before) {
        var after = CopyVertices();
        Assert.That(after.Length, Is.EqualTo(before.Length));
        for(int m = 0; m < before.Length; m++) {
            Assert.That(after[m].Length, Is.EqualTo(before[m].Length));
            for(int i = 0; i < before[m].Length; i++) Assert.That(after[m][i].y, Is.EqualTo(before[m][i].y).Within(.001f));
        }
    }

    private void AssertVerticesEqual(Vector3[][] before) {
        var after = CopyVertices();
        Assert.That(after.Length, Is.EqualTo(before.Length));
        for(int m = 0; m < before.Length; m++) {
            Assert.That(after[m].Length, Is.EqualTo(before[m].Length));
            for(int i = 0; i < before[m].Length; i++) Assert.That(Vector3.Distance(after[m][i], before[m][i]), Is.LessThan(.002f));
        }
    }
}
