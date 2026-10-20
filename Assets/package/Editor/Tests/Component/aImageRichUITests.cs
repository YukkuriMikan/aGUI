using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ANest.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.U2D;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>標準 Image のジオメトリ・入力と RichUI の描画を一緒に検証する。</summary>
public class aImageRichUITests {
	private readonly List<Object> _objects = new List<Object>();
	private Material _material;
	private Canvas _canvas;
	private Camera _pixelCamera;
	private aImage _image;
	private string _assetFolder;
	private static readonly MethodInfo Populate = typeof(aImage).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
	private static readonly MethodInfo PopulateImage = typeof(UnityEngine.UI.Image).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

	[SetUp]
	public void SetUp() {
		var shader = Shader.Find("aGUI/RichUI");
		Assert.That(shader, Is.Not.Null);
		Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, "RichUI shader compilation");
		_material = Track(new Material(shader));
		_material.SetFloat("_ShadowEnabled", 0);
		_material.SetFloat("_OutlineEnabled", 0);
		_canvas = Track(new GameObject("RichUI test canvas", typeof(RectTransform), typeof(Canvas))).GetComponent<Canvas>();
		_canvas.renderMode = RenderMode.WorldSpace;
		SetSize((RectTransform)_canvas.transform, 256, 256);
		_pixelCamera = Track(new GameObject("One UI unit per pixel", typeof(Camera))).GetComponent<Camera>();
		_pixelCamera.enabled = false;
		_pixelCamera.transform.position = new Vector3(0, 0, -10);
		_pixelCamera.orthographic = true;
		_pixelCamera.orthographicSize = 128;
		_pixelCamera.pixelRect = new Rect(0, 0, 256, 256);
		_pixelCamera.aspect = 1;
		_canvas.worldCamera = _pixelCamera;
		_image = CreateImage("RichUI image", _canvas.transform);
		_image.sprite = CreateSprite(new Rect(0, 0, 32, 32), Vector4.zero);
		_image.material = _material;
	}

	[TearDown]
	public void TearDown() {
		for(var i = _objects.Count - 1; i >= 0; i--) if(_objects[i]) Object.DestroyImmediate(_objects[i]);
		_objects.Clear();
		if(!string.IsNullOrEmpty(_assetFolder)) AssetDatabase.DeleteAsset(_assetFolder);
		_assetFolder = null;
	}

	[Test]
	public void OrdinaryImagesDoNotSubscribeToRenderMonitoringOrAllocateEffectSnapshots() {
		_image.material = null;
		var normal = CreateImage("Ordinary Image", _canvas.transform);
		var channels = _canvas.additionalShaderChannels;
		foreach(var source in new[] { null, Track(new Material(Shader.Find("UI/Default"))) }) {
			normal.material = source;
			Assert.That(RenderCallbackCount(normal), Is.Zero);
			Canvas.ForceUpdateCanvases();
			Canvas.ForceUpdateCanvases();
			Assert.That(RenderCallbackCount(normal), Is.Zero);
			Assert.That(typeof(aImage).GetField("_observedFloats", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(normal), Is.Null);
			Assert.That(typeof(aImage).GetField("_observedColors", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(normal), Is.Null);
			Assert.That(_canvas.additionalShaderChannels, Is.EqualTo(channels));
		}
	}

	[Test]
	public void OrdinaryMaterialReplacementsDoNotRequestGeometryRebuilds() {
		_image.material = null;
		Canvas.ForceUpdateCanvases();
		var dirty = 0;
		_image.RegisterDirtyVerticesCallback(() => dirty++);
		_image.material = Track(new Material(Shader.Find("UI/Default")));
		Canvas.ForceUpdateCanvases();
		_image.material = null;
		Canvas.ForceUpdateCanvases();
		Assert.That(dirty, Is.Zero);
		Assert.That(RenderCallbackCount(_image), Is.Zero);
		using(var actual = Build()) using(var expected = Build(false)) AssertSameVertices(actual, expected);
	}

	[Test]
	public void RenderMonitoringFollowsMaterialChangesAndEnableStateWithoutDuplicates() {
		Assert.That(RenderCallbackCount(_image), Is.EqualTo(1));
		for(var i = 0; i < 3; i++) {
			_image.SetMaterialDirty();
			_image.material = _material;
			Canvas.ForceUpdateCanvases();
			Assert.That(RenderCallbackCount(_image), Is.EqualTo(1));
			_image.enabled = false;
			Assert.That(RenderCallbackCount(_image), Is.Zero);
			_image.SetMaterialDirty();
			Assert.That(RenderCallbackCount(_image), Is.Zero, "A disabled Image must remain unsubscribed");
			_image.enabled = true;
			Assert.That(RenderCallbackCount(_image), Is.EqualTo(1));
			_image.material = null;
			Assert.That(RenderCallbackCount(_image), Is.Zero);
			_image.enabled = false;
			_image.material = _material;
			Assert.That(RenderCallbackCount(_image), Is.Zero);
			_image.enabled = true;
			Assert.That(RenderCallbackCount(_image), Is.EqualTo(1));
		}
		var previous = _image;
		Object.DestroyImmediate(_image.gameObject);
		Assert.That(RenderCallbackCount(previous), Is.Zero, "The static Canvas event must not retain destroyed Images");
	}

	[Test]
	public void SerializedMaterialAssignmentsEnableAndDisableRichUIInEditMode() {
		_image.material = null;
		_material.SetFloat("_OutlineEnabled", 1);
		_material.SetFloat("_OutlineWidth", 8);
		var serialized = new SerializedObject(_image);
		serialized.FindProperty("m_Material").objectReferenceValue = _material;
		serialized.ApplyModifiedPropertiesWithoutUndo();
		Canvas.ForceUpdateCanvases();
		Assert.That(RenderCallbackCount(_image), Is.EqualTo(1));
		using(var expanded = Build()) Assert.That(Bounds(expanded).width, Is.EqualTo(81).Within(0.001));
		serialized.Update();
		serialized.FindProperty("m_Material").objectReferenceValue = null;
		serialized.ApplyModifiedPropertiesWithoutUndo();
		Canvas.ForceUpdateCanvases();
		Assert.That(RenderCallbackCount(_image), Is.Zero);
		using(var actual = Build()) using(var expected = Build(false)) AssertSameVertices(actual, expected);
	}

	[Test]
	public void InPlaceShaderReplacementsUseGraphicMaterialDirtyNotification() {
		var source = Track(new Material(Shader.Find("UI/Default")));
		_image.material = source;
		Assert.That(RenderCallbackCount(_image), Is.Zero);
		source.shader = Shader.Find("aGUI/RichUI");
		source.SetFloat("_OutlineEnabled", 1);
		source.SetFloat("_OutlineWidth", 8);
		source.SetFloat("_ShadowEnabled", 0);
		_image.SetMaterialDirty();
		Assert.That(RenderCallbackCount(_image), Is.EqualTo(1));
		Canvas.ForceUpdateCanvases();
		using(var expanded = Build()) Assert.That(Bounds(expanded).width, Is.EqualTo(81).Within(0.001));
		source.shader = Shader.Find("UI/Default");
		Canvas.ForceUpdateCanvases();
		Assert.That(RenderCallbackCount(_image), Is.Zero, "Leaving RichUI stops its parameter monitor");
		using(var actual = Build()) using(var expected = Build(false)) AssertSameVertices(actual, expected);
	}

	[UnityTest]
	public IEnumerator MaterialInspectorShaderChangesStartRichUIWithoutTogglingImageType() {
		_assetFolder = AssetDatabase.GenerateUniqueAssetPath("Assets/RichUIMaterialTest");
		AssetDatabase.CreateFolder("Assets", Path.GetFileName(_assetFolder));
		var source = new Material(Shader.Find("UI/Default"));
		AssetDatabase.CreateAsset(source, _assetFolder + "/Material.mat");
		try {
			_image.material = source;
			_image.type = UnityEngine.UI.Image.Type.Simple;
			Canvas.ForceUpdateCanvases();
			Assert.That(RenderCallbackCount(_image), Is.Zero);
			var serialized = new SerializedObject(source);
			serialized.FindProperty("m_Shader").objectReferenceValue = Shader.Find("aGUI/RichUI");
			serialized.ApplyModifiedProperties();
			source.SetFloat("_OutlineEnabled", 1);
			source.SetFloat("_OutlineWidth", 8);
			source.SetFloat("_ShadowEnabled", 0);
			Undo.FlushUndoRecordObjects();
			yield return null;
			yield return null;
			Canvas.ForceUpdateCanvases();
			Assert.That(RenderCallbackCount(_image), Is.EqualTo(1), "A material Inspector edit must update the cached shader classification");
			var mesh = _image.canvasRenderer.GetMesh();
			Assert.That(mesh, Is.Not.Null);
			Assert.That(mesh.bounds.size.x, Is.EqualTo(81).Within(0.001), "The rendered mesh must expand without changing ImageType");
			Assert.That(_image.type, Is.EqualTo(UnityEngine.UI.Image.Type.Simple));
			serialized.Update();
			serialized.FindProperty("m_Shader").objectReferenceValue = Shader.Find("UI/Default");
			serialized.ApplyModifiedProperties();
			Undo.FlushUndoRecordObjects();
			yield return null;
			yield return null;
			Canvas.ForceUpdateCanvases();
			Assert.That(RenderCallbackCount(_image), Is.Zero);
			using(var actual = Build()) using(var expected = Build(false)) AssertSameVertices(actual, expected);
		} finally { Undo.ClearUndo(source); }
	}

	[TestCase(UnityEngine.UI.Image.Type.Simple)]
	[TestCase(UnityEngine.UI.Image.Type.Sliced)]
	[TestCase(UnityEngine.UI.Image.Type.Tiled)]
	public void ExpansionKeepsSourceMappingAndOriginalBody(UnityEngine.UI.Image.Type type) {
		_image.type = type;
		_image.sprite = CreateSprite(new Rect(8, 8, 32, 32), type == UnityEngine.UI.Image.Type.Simple ? Vector4.zero : Vector4.one * 6);
		_image.preserveAspect = true;
		SetSize(_image.rectTransform, 93, 71);
		var rect = _image.rectTransform.rect;
		using(var original = Build(false)) {
			_material.SetFloat("_OutlineEnabled", 1);
			_material.SetFloat("_OutlineWidth", 7);
			_material.SetFloat("_ShadowEnabled", 1);
			_material.SetFloat("_ShadowSpread", 2);
			_material.SetFloat("_ShadowSoftness", 5);
			_material.SetFloat("_ShadowOffsetX", -13);
			_material.SetFloat("_ShadowOffsetY", 17);
			using(var expanded = Build()) {
				Assert.That(expanded.currentVertCount, Is.EqualTo(original.currentVertCount));
				var originalBounds = Bounds(original);
				var expandedBounds = Bounds(expanded);
				Assert.That(expandedBounds.xMin, Is.EqualTo(originalBounds.xMin - 20.5f).Within(0.001));
				Assert.That(expandedBounds.xMax, Is.EqualTo(originalBounds.xMax + 7.5f).Within(0.001));
				Assert.That(expandedBounds.yMin, Is.EqualTo(originalBounds.yMin - 7.5f).Within(0.001));
				Assert.That(expandedBounds.yMax, Is.EqualTo(originalBounds.yMax + 24.5f).Within(0.001));
				for(var i = 0; i < original.currentVertCount; i++) {
					var before = Vertex(original, i);
					var after = Vertex(expanded, i);
					var delta = after.position - before.position;
					Assert.That(after.uv0.x, Is.EqualTo(before.uv0.x + after.uv2.x * delta.x + after.uv2.z * delta.y).Within(0.00001));
					Assert.That(after.uv0.y, Is.EqualTo(before.uv0.y + after.uv2.y * delta.x + after.uv2.w * delta.y).Within(0.00001));
					Assert.That(after.uv0.z, Is.EqualTo((after.position.x - originalBounds.xMin) / originalBounds.width).Within(0.00001));
					Assert.That(after.uv0.w, Is.EqualTo((after.position.y - originalBounds.yMin) / originalBounds.height).Within(0.00001));
					Assert.That(after.color, Is.EqualTo(before.color));
				}
				Assert.That(_image.rectTransform.rect, Is.EqualTo(rect));
			}
		}
	}

	[Test]
	public void ResizingACulledImageRecomputesBoundsBeforeGeometryRebuild() {
		_material.SetFloat("_OutlineEnabled", 1);
		_material.SetFloat("_OutlineWidth", 8);
		using(var mesh = Build()) { }
		var clip = new Rect(45, -20, 10, 40);
		_image.Cull(clip, true);
		Assert.That(_image.canvasRenderer.cull, Is.True);
		SetSize(_image.rectTransform, 96, 64);
		_image.Cull(clip, true);
		Assert.That(_image.canvasRenderer.cull, Is.False, "New body and outline intersect the mask");
	}

	[Test]
	public void CanvasChannelsAreAddedWithoutRemovingExistingSettings() {
		_canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
		var cameraMode = _canvas.renderMode;
		Canvas.ForceUpdateCanvases();
		Assert.That(_canvas.additionalShaderChannels, Is.EqualTo(AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent |
			AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2));
		Assert.That(_canvas.renderMode, Is.EqualTo(cameraMode));
		var nested = Track(new GameObject("Nested canvas", typeof(RectTransform), typeof(Canvas))).GetComponent<Canvas>();
		nested.transform.SetParent(_canvas.transform, false);
		_image.transform.SetParent(nested.transform, false);
		Assert.That(nested.additionalShaderChannels & (AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2),
			Is.EqualTo(AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2));
	}

	[Test]
	public void MaterialEditsRebuildOnlyWhenPaddingChangesAndDefaultRestoresImageMesh() {
		Canvas.ForceUpdateCanvases();
		var dirty = 0;
		_image.RegisterDirtyVerticesCallback(() => dirty++);
		Canvas.ForceUpdateCanvases();
		Canvas.ForceUpdateCanvases();
		Assert.That(dirty, Is.Zero, "Unchanged frames must not dirty the mesh");
		_material.SetFloat("_Brightness", 0.4f);
		Canvas.ForceUpdateCanvases();
		Assert.That(dirty, Is.Zero, "Color-only changes must not dirty geometry");
		_material.SetFloat("_OutlineEnabled", 1);
		_material.SetFloat("_OutlineWidth", 11);
		Canvas.ForceUpdateCanvases();
		Assert.That(dirty, Is.EqualTo(1));
		using(var mesh = Build()) Assert.That(Bounds(mesh).width, Is.EqualTo(87).Within(0.001));
		_image.material = null;
		using(var actual = Build()) using(var expected = Build(false)) AssertSameVertices(actual, expected);
	}

	[Test]
	public void StencilCacheReceivesEffectParametersAndKeepsMaskState() {
		var mask = Track(new GameObject("Parent mask", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(Mask)));
		mask.transform.SetParent(_canvas.transform, false);
		_image.transform.SetParent(mask.transform, false);
		var first = _image.GetModifiedMaterial(_material);
		Assert.That(first, Is.Not.SameAs(_material));
		var stencil = first.GetFloat("_Stencil");
		var comparison = first.GetFloat("_StencilComp");
		first.SetVector("_ClipRect", new Vector4(-9, -8, 7, 6));
		first.EnableKeyword("UNITY_UI_CLIP_RECT");
		_material.SetFloat("_Brightness", 0.3f);
		_material.SetFloat("_ShadowOffsetX", 25);
		_material.SetColor("_RecolorColor", Color.cyan);
		_material.SetTextureScale("_OutlineTex", new Vector2(3, 4));
		Canvas.ForceUpdateCanvases();
		var updated = _image.GetModifiedMaterial(_material);
		Assert.That(updated, Is.SameAs(first), "Use uGUI's shared stencil material");
		Assert.That(updated.GetFloat("_Brightness"), Is.EqualTo(0.3f));
		Assert.That(updated.GetFloat("_ShadowOffsetX"), Is.EqualTo(25));
		Assert.That(updated.GetColor("_RecolorColor"), Is.EqualTo(Color.cyan));
		Assert.That(updated.GetTextureScale("_OutlineTex"), Is.EqualTo(new Vector2(3, 4)));
		Assert.That(updated.GetFloat("_Stencil"), Is.EqualTo(stencil));
		Assert.That(updated.GetFloat("_StencilComp"), Is.EqualTo(comparison));
		Assert.That(updated.GetVector("_ClipRect"), Is.EqualTo(new Vector4(-9, -8, 7, 6)));
		Assert.That(updated.IsKeywordEnabled("UNITY_UI_CLIP_RECT"), Is.True);
	}

	[Test]
	public void OwnMaskAndPopMaterialsReceiveUpdatesWithoutLosingStencilOrAlphaClip() {
		_image.gameObject.AddComponent<Mask>();
		Canvas.ForceUpdateCanvases();
		var stencil = _image.canvasRenderer.GetMaterial();
		var pop = _image.canvasRenderer.GetPopMaterial(0);
		Assert.That(stencil, Is.Not.SameAs(_material));
		var operation = stencil.GetFloat("_StencilOp");
		var popOperation = pop.GetFloat("_StencilOp");
		_material.SetFloat("_OutlineWidth", 9);
		_material.SetFloat("_RecolorAmount", 1);
		Canvas.ForceUpdateCanvases();
		Assert.That(_image.canvasRenderer.GetMaterial(), Is.SameAs(stencil));
		Assert.That(_image.canvasRenderer.GetPopMaterial(0), Is.SameAs(pop));
		Assert.That(stencil.GetFloat("_OutlineWidth"), Is.EqualTo(9));
		Assert.That(pop.GetFloat("_RecolorAmount"), Is.EqualTo(1));
		Assert.That(stencil.GetFloat("_StencilOp"), Is.EqualTo(operation));
		Assert.That(pop.GetFloat("_StencilOp"), Is.EqualTo(popOperation));
		Assert.That(stencil.IsKeywordEnabled("UNITY_UI_ALPHACLIP"), Is.True);
	}

	[Test]
	public void RectMaskCullingIncludesOnlyDecorationsIntersectingItsClip() {
		_material.SetFloat("_OutlineEnabled", 1);
		_material.SetFloat("_OutlineWidth", 12);
		// Verify before the first geometry rebuild as well as after it.
		foreach(var built in new[] { false, true }) {
			if(built) using(var mesh = Build()) { }
			_image.Cull(new Rect(35, -20, 10, 40), true);
			Assert.That(_image.canvasRenderer.cull, Is.False, "Outline reaches into the mask");
			_image.Cull(new Rect(45, -20, 10, 40), true);
			Assert.That(_image.canvasRenderer.cull, Is.True);
			_image.Cull(new Rect(35, -20, 10, 40), false);
			Assert.That(_image.canvasRenderer.cull, Is.True);
		}
	}

	[UnityTest]
	public IEnumerator ExpandedGeometryDoesNotExpandRaycastAreaOrModifySprite() {
		_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		var raycaster = _canvas.gameObject.AddComponent<GraphicRaycaster>();
		raycaster.ignoreReversedGraphics = false;
		var events = Track(new GameObject("RichUI event system", typeof(EventSystem))).GetComponent<EventSystem>();
		var originalSprite = _image.sprite;
		var originalRect = _image.rectTransform.rect;
		_material.SetFloat("_OutlineEnabled", 1);
		_material.SetFloat("_OutlineWidth", 16);
		Canvas.ForceUpdateCanvases();
		yield return null; // Native Canvas batching assigns the depth used by GraphicRaycaster.
		// An EditMode overlay may use desktop pixels while Screen reports the
		// Game view size. Place the test image inside both coordinate ranges.
		_image.rectTransform.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0);
		Canvas.ForceUpdateCanvases();
		var results = new List<RaycastResult>();
		var pointer = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(null, _image.transform.TransformPoint(Vector3.zero)) };
		raycaster.Raycast(pointer, results);
		Assert.That(results.Exists(hit => hit.gameObject == _image.gameObject), Is.True, $"depth={_image.depth}, screen={pointer.position}, size={Screen.width}x{Screen.height}");
		results.Clear();
		pointer.position = RectTransformUtility.WorldToScreenPoint(null, _image.transform.TransformPoint(new Vector3(40, 0)));
		raycaster.Raycast(pointer, results);
		Assert.That(results.Exists(hit => hit.gameObject == _image.gameObject), Is.False);
		Assert.That(_image.sprite, Is.SameAs(originalSprite));
		Assert.That(_image.rectTransform.rect, Is.EqualTo(originalRect));
	}

	[TestCase(true)]
	[TestCase(false)]
	public void ParentMasksClipDecorationsAndCanvasGroupFadesAllLayers(bool stencil) {
		var mask = Track(new GameObject("Rendering parent mask", typeof(RectTransform)));
		mask.transform.SetParent(_canvas.transform, false);
		SetSize((RectTransform)mask.transform, 72, 72);
		if(stencil) {
			mask.AddComponent<UnityEngine.UI.Image>();
			mask.AddComponent<Mask>().showMaskGraphic = false;
		} else mask.AddComponent<RectMask2D>();
		_image.transform.SetParent(mask.transform, false);
		_image.color = new Color(1, 1, 1, 0.5f);
		_canvas.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;
		_material.SetFloat("_OutlineEnabled", 1);
		_material.SetFloat("_OutlineWidth", 12);
		_material.SetColor("_OutlineColor", Color.green);
		var pixels = RenderCanvas();
		Assert.That(Pixel(pixels, 0, 0).a, Is.EqualTo(0.25).Within(0.02), "CanvasGroup and Image Color alpha");
		Assert.That(Pixel(pixels, 34, 0).g, Is.EqualTo(0.25).Within(0.02), "Decoration inside parent mask");
		Assert.That(Pixel(pixels, 40, 0).a, Is.LessThan(0.01), "Intentional parent clipping");
		_material.SetColor("_OutlineColor", Color.blue);
		_material.SetFloat("_RecolorAmount", 1);
		_material.SetColor("_RecolorColor", Color.green);
		pixels = RenderCanvas();
		Assert.That(Pixel(pixels, 0, 0).g, Is.EqualTo(0.25).Within(0.02), "Body parameter update");
		Assert.That(Pixel(pixels, 34, 0).b, Is.EqualTo(0.25).Within(0.02), "Stencil/clip material parameter update");
		Assert.That(Pixel(pixels, 40, 0).a, Is.LessThan(0.01));
	}

	[TestCase(32, 0)]
	[TestCase(-32, 0)]
	[TestCase(0, 32)]
	[TestCase(0, -32)]
	public void OpaqueUnpaddedSpriteRendersOutlineAndUnclippedDirectionalShadow(float x, float y) {
		_material.SetFloat("_OutlineEnabled", 1);
		_material.SetFloat("_OutlineWidth", 8);
		_material.SetColor("_OutlineColor", Color.green);
		_material.SetFloat("_ShadowEnabled", 1);
		_material.SetFloat("_ShadowSpread", 3);
		_material.SetFloat("_ShadowSoftness", 0);
		_material.SetFloat("_ShadowOffsetX", x);
		_material.SetFloat("_ShadowOffsetY", y);
		_material.SetColor("_ShadowColor", Color.blue);
		var pixels = RenderMesh();
		Assert.That(Pixel(pixels, -37, 0).g, Is.GreaterThan(0.9));
		Assert.That(Pixel(pixels, 37, 0).g, Is.GreaterThan(0.9));
		Assert.That(Pixel(pixels, 0, -37).g, Is.GreaterThan(0.9));
		Assert.That(Pixel(pixels, 0, 37).g, Is.GreaterThan(0.9));
		Assert.That(Pixel(pixels, (int)(x + Mathf.Sign(x) * 32), (int)(y + Mathf.Sign(y) * 32)).b, Is.GreaterThan(0.9));
		Assert.That(Pixel(pixels, 0, 0).r, Is.GreaterThan(0.9));
	}

	[TestCase(0.25f, 1)]
	[TestCase(1.25f, 1)]
	[TestCase(3.25f, 1)]
	[TestCase(7.25f, 1)]
	[TestCase(15.25f, 1)]
	[TestCase(1f, 0.25f)]
	[TestCase(2.5f, 0.5f)]
	[TestCase(0.625f, 2)]
	public void FractionalOutlineWidthsKeepTheEntireFilteredEdge(float width, float scale) {
		_image.rectTransform.localScale = Vector3.one * scale;
		_material.SetFloat("_OutlineEnabled", 1);
		_material.SetFloat("_OutlineWidth", width);
		_material.SetColor("_OutlineColor", Color.green);
		var reference = RenderMesh(extraPadding: 2 / scale, scale: scale);
		var actual = RenderMesh(scale: scale);
		var edge = Mathf.FloorToInt((32 + width) * scale);
		foreach(var point in new[] { new Vector2Int(edge, 0), new Vector2Int(-edge - 1, 0), new Vector2Int(0, edge), new Vector2Int(0, -edge - 1) }) {
			var expectedAlpha = Pixel(reference, point.x, point.y).a;
			Assert.That(expectedAlpha, Is.GreaterThan(0.1), "The oversized reference contains a visible filtered edge");
			Assert.That(Pixel(actual, point.x, point.y).a, Is.EqualTo(expectedAlpha).Within(0.01), $"Filtered outline clipped at {point}, width={width}");
		}
	}

	[TestCase(30)]
	[TestCase(45)]
	[TestCase(75)]
	public void RotatedOutlineMatchesAnOversizedReference(float rotation) {
		_image.rectTransform.localRotation = Quaternion.Euler(0, 0, rotation);
		_material.SetFloat("_OutlineEnabled", 1);
		_material.SetFloat("_OutlineWidth", 3.25f);
		_material.SetColor("_OutlineColor", Color.green);
		var reference = RenderMesh(extraPadding: 2);
		var actual = RenderMesh();
		for(var y = -54; y < 54; y++) for(var x = -54; x < 54; x++)
			Assert.That(Pixel(actual, x, y).a, Is.EqualTo(Pixel(reference, x, y).a).Within(0.01), $"Rotated fringe at {x},{y}, rotation={rotation}");
	}

	[Test]
	public void FilteringMarginTracksCameraScaleWithoutDirtyingUnchangedFrames() {
		_material.SetFloat("_OutlineEnabled", 1);
		_material.SetFloat("_OutlineWidth", 8);
		Canvas.ForceUpdateCanvases();
		Assert.That(_image.canvasRenderer.GetMesh().bounds.size.x, Is.EqualTo(81).Within(0.001));
		_pixelCamera.orthographicSize = 256;
		Canvas.ForceUpdateCanvases();
		Assert.That(_image.canvasRenderer.GetMesh().bounds.size.x, Is.EqualTo(82).Within(0.001), "Zoomed-out pixels need a larger local fringe");
		var dirty = 0;
		_image.RegisterDirtyVerticesCallback(() => dirty++);
		Canvas.ForceUpdateCanvases();
		Canvas.ForceUpdateCanvases();
		Assert.That(dirty, Is.Zero);
	}

	[TestCase(UnityEngine.UI.Image.Type.Simple)]
	[TestCase(UnityEngine.UI.Image.Type.Sliced)]
	[TestCase(UnityEngine.UI.Image.Type.Tiled)]
	public void AtlasNeighboursDoNotLeakAndBodyPixelsMatchStandardImage(UnityEngine.UI.Image.Type type) {
		_image.type = type;
		_image.sprite = CreateSprite(new Rect(16, 16, 32, 32), type == UnityEngine.UI.Image.Type.Simple ? Vector4.zero : Vector4.one * 5, true);
		SetSize(_image.rectTransform, 79, 67);
		var before = RenderMesh(false);
		_material.SetFloat("_OutlineEnabled", 1);
		_material.SetFloat("_OutlineWidth", 8);
		_material.SetColor("_OutlineColor", Color.green);
		var after = RenderMesh();
		for(var y = -30; y <= 30; y += 3) for(var x = -36; x <= 36; x += 3) {
			var actual = Pixel(after, x, y);
			var expected = Pixel(before, x, y);
			Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.02), $"body {type} at {x},{y}");
			Assert.That(actual.b, Is.LessThan(0.02), "Blue atlas neighbours must never enter the sprite");
		}
		Assert.That(Pixel(after, -44, 0).g, Is.GreaterThan(0.9));
		Assert.That(Pixel(after, 44, 0).b, Is.LessThan(0.02));
	}

	[Test]
	public void PackedSpriteAtlasRemainsIsolatedWhenMinified() {
		var originalMode = EditorSettings.spritePackerMode;
		try {
			EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
			VerifyPackedSpriteAtlas();
		} finally { EditorSettings.spritePackerMode = originalMode; }
	}

	private void VerifyPackedSpriteAtlas() {
		_assetFolder = AssetDatabase.GenerateUniqueAssetPath("Assets/RichUIAtlasTest");
		AssetDatabase.CreateFolder("Assets", Path.GetFileName(_assetFolder));
		var source = CreateSpriteAsset("Source", Color.red);
		var neighbour = CreateSpriteAsset("Neighbour", Color.blue);
		var atlas = new SpriteAtlas();
		atlas.SetPackingSettings(new SpriteAtlasPackingSettings { padding = 2, enableRotation = false, enableTightPacking = false });
		var textureSettings = atlas.GetTextureSettings();
		textureSettings.generateMipMaps = true;
		textureSettings.filterMode = FilterMode.Bilinear;
		atlas.SetTextureSettings(textureSettings);
		atlas.SetPlatformSettings(new TextureImporterPlatformSettings {
			name = "DefaultTexturePlatform", maxTextureSize = 256,
			format = TextureImporterFormat.RGBA32, textureCompression = TextureImporterCompression.Uncompressed
		});
		atlas.Add(new Object[] { source, neighbour });
		var atlasPath = _assetFolder + "/Test.spriteatlas";
		AssetDatabase.CreateAsset(atlas, atlasPath);
		AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
		atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
		SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
		// Use Unity's packed preview texture and UVs to exercise the actual
		// atlas layout and generated mips directly in EditMode.
		var packedTexture = UnityEditor.Sprites.SpriteUtility.GetSpriteTexture(source, true);
		Assert.That(packedTexture, Is.SameAs(UnityEditor.Sprites.SpriteUtility.GetSpriteTexture(neighbour, true)));
		Assert.That(packedTexture.width * packedTexture.height, Is.GreaterThan(32 * 32), "Shared packed atlas texture");
		Assert.That(packedTexture.mipmapCount, Is.GreaterThan(1));
		var uvs = UnityEditor.Sprites.SpriteUtility.GetSpriteUVs(source, true);
		var min = Vector2.one;
		var max = Vector2.zero;
		foreach(var uv in uvs) { min = Vector2.Min(min, uv); max = Vector2.Max(max, uv); }
		_image.sprite = Track(Sprite.Create(packedTexture, Rect.MinMaxRect(min.x * packedTexture.width, min.y * packedTexture.height,
			max.x * packedTexture.width, max.y * packedTexture.height), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect));
		_material.SetFloat("_OutlineEnabled", 1);
		_material.SetFloat("_OutlineWidth", 4);
		_material.SetColor("_OutlineColor", Color.green);
		foreach(var size in new[] { 64f, 4f }) {
			SetSize(_image.rectTransform, size, size);
			var pixels = RenderMesh();
			Assert.That(Pixel(pixels, 0, 0).r, Is.GreaterThan(0.95), "Packed source body");
			for(var y = -40; y < 40; y++) for(var x = -40; x < 40; x++)
				Assert.That(Pixel(pixels, x, y).b, Is.LessThan(0.02), $"Neighbour at {x},{y}, size={size}");
		}
	}

	[TestCase(UnityEngine.UI.Image.Type.Simple)]
	[TestCase(UnityEngine.UI.Image.Type.Sliced)]
	[TestCase(UnityEngine.UI.Image.Type.Tiled)]
	public void OutlineTextureUsesOriginalRectangleCoordinates(UnityEngine.UI.Image.Type type) {
		_image.type = type;
		_image.sprite = CreateSprite(new Rect(16, 16, 32, 32), type == UnityEngine.UI.Image.Type.Simple ? Vector4.zero : Vector4.one * 5);
		var pattern = Track(new Texture2D(2, 1, TextureFormat.RGBA32, false));
		pattern.wrapMode = TextureWrapMode.Clamp;
		pattern.SetPixels(new[] { Color.green, Color.blue });
		pattern.Apply();
		_material.SetTexture("_OutlineTex", pattern);
		_material.SetFloat("_UseOutlineTexture", 1);
		_material.SetFloat("_OutlineEnabled", 1);
		_material.SetFloat("_OutlineWidth", 8);
		_material.SetColor("_OutlineColor", Color.white);
		var pixels = RenderMesh();
		Assert.That(Pixel(pixels, -37, 0).g, Is.GreaterThan(0.95));
		Assert.That(Pixel(pixels, 37, 0).b, Is.GreaterThan(0.95));
		Assert.That(Pixel(pixels, 0, 0).r, Is.GreaterThan(0.95), "Pattern must not recolor the body");
	}

	[TestCase(UnityEngine.UI.Image.Type.Simple)]
	[TestCase(UnityEngine.UI.Image.Type.Sliced)]
	[TestCase(UnityEngine.UI.Image.Type.Tiled)]
	public void ExpandedMeshDoesNotStretchPatternedBody(UnityEngine.UI.Image.Type type) {
		_image.type = type;
		_image.sprite = CreateSprite(new Rect(16, 16, 32, 32), type == UnityEngine.UI.Image.Type.Simple ? Vector4.zero : Vector4.one * 5);
		var texture = _image.sprite.texture;
		for(var y = 16; y < 48; y++) for(var x = 16; x < 48; x++) texture.SetPixel(x, y, new Color((x - 16) / 32f, (y - 16) / 32f, 0, 1));
		texture.Apply();
		SetSize(_image.rectTransform, 79, 67);
		var before = RenderMesh(false);
		_material.SetFloat("_OutlineEnabled", 1);
		_material.SetFloat("_OutlineWidth", 11);
		_material.SetFloat("_ShadowEnabled", 1);
		_material.SetFloat("_ShadowOffsetX", -15);
		var after = RenderMesh();
		for(var y = -30; y <= 30; y += 2) for(var x = -36; x <= 36; x += 2) {
			var actual = Pixel(after, x, y);
			var expected = Pixel(before, x, y);
			Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.02), $"body X mapping at {x},{y}");
			Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.02), $"body Y mapping at {x},{y}");
		}
	}

	[TestCase(TextureWrapMode.Clamp)]
	[TestCase(TextureWrapMode.Repeat)]
	public void TiledPartialLastTileDoesNotContinueTheBodyIntoPadding(TextureWrapMode wrap) {
		_image.type = UnityEngine.UI.Image.Type.Tiled;
		if(wrap == TextureWrapMode.Repeat) _image.sprite = CreateSprite(new Rect(0, 0, 64, 64), Vector4.zero);
		_image.sprite.texture.wrapMode = wrap;
		SetSize(_image.rectTransform, 79, 67);
		_material.SetFloat("_OutlineEnabled", 1);
		_material.SetFloat("_OutlineWidth", 8);
		_material.SetColor("_OutlineColor", Color.green);
		var pixels = RenderMesh();
		Assert.That(Pixel(pixels, 43, 0).r, Is.LessThan(0.02), "Body stops at the cropped last tile");
		Assert.That(Pixel(pixels, 43, 0).g, Is.GreaterThan(0.9));
		Assert.That(Pixel(pixels, 0, 38).g, Is.GreaterThan(0.9));
	}

	[Test]
	public void BodyRecolorDoesNotTintDecorationsAndGraphicAlphaAffectsAllLayers() {
		_material.SetFloat("_OutlineEnabled", 1);
		_material.SetFloat("_OutlineWidth", 8);
		_material.SetColor("_OutlineColor", Color.green);
		_material.SetFloat("_RecolorAmount", 1);
		_material.SetColor("_RecolorColor", Color.blue);
		_image.color = new Color(0.3f, 0.4f, 0.5f, 0.5f);
		var pixels = RenderMesh();
		Assert.That(Pixel(pixels, 0, 0).b, Is.EqualTo(0.5).Within(0.02));
		Assert.That(Pixel(pixels, 37, 0).g, Is.EqualTo(0.5).Within(0.02));
		Assert.That(Pixel(pixels, 37, 0).b, Is.LessThan(0.02));
		Assert.That(Pixel(pixels, 0, 0).a, Is.EqualTo(0.5).Within(0.02));
	}

	[TestCase(true)]
	[TestCase(false)]
	public void UnsupportedGeometryKeepsStandardPositions(bool spriteMesh) {
		_image.useSpriteMesh = spriteMesh;
		_image.type = spriteMesh ? UnityEngine.UI.Image.Type.Simple : UnityEngine.UI.Image.Type.Filled;
		_image.fillAmount = 0.6f;
		_material.SetFloat("_OutlineEnabled", 1);
		_material.SetFloat("_OutlineWidth", 16);
		using(var actual = Build()) using(var expected = Build(false)) {
			Assert.That(actual.currentVertCount, Is.EqualTo(expected.currentVertCount));
			for(var i = 0; i < actual.currentVertCount; i++) Assert.That(Vertex(actual, i).position, Is.EqualTo(Vertex(expected, i).position));
		}
	}

	private T Track<T>(T value) where T : Object { _objects.Add(value); return value; }

	private static int RenderCallbackCount(aImage image) {
		var field = typeof(Canvas).GetField("preWillRenderCanvases", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		Assert.That(field, Is.Not.Null);
		var callback = (System.MulticastDelegate)field.GetValue(null);
		if(callback == null) return 0;
		var count = 0;
		foreach(var entry in callback.GetInvocationList()) if(ReferenceEquals(entry.Target, image)) count++;
		return count;
	}

	private aImage CreateImage(string name, Transform parent) {
		var go = Track(new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)));
		go.transform.SetParent(parent, false);
		var image = go.AddComponent<aImage>();
		SetSize(image.rectTransform, 64, 64);
		return image;
	}

	private Sprite CreateSprite(Rect rect, Vector4 border, bool mipmaps = false) {
		var texture = Track(new Texture2D(64, 64, TextureFormat.RGBA32, mipmaps));
		texture.wrapMode = TextureWrapMode.Clamp;
		texture.filterMode = FilterMode.Bilinear;
		var pixels = new Color[64 * 64];
		for(var y = 0; y < 64; y++) for(var x = 0; x < 64; x++) pixels[y * 64 + x] = rect.Contains(new Vector2(x, y)) ? Color.red : Color.blue;
		texture.SetPixels(pixels);
		texture.Apply(mipmaps);
		return Track(Sprite.Create(texture, rect, Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect, border));
	}

	private Sprite CreateSpriteAsset(string name, Color color) {
		var texture = Track(new Texture2D(32, 32, TextureFormat.RGBA32, false));
		var pixels = new Color[32 * 32];
		for(var i = 0; i < pixels.Length; i++) pixels[i] = color;
		texture.SetPixels(pixels);
		texture.Apply();
		var path = _assetFolder + "/" + name + ".png";
		File.WriteAllBytes(path, texture.EncodeToPNG());
		AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
		var importer = (TextureImporter)AssetImporter.GetAtPath(path);
		importer.textureType = TextureImporterType.Sprite;
		importer.spriteImportMode = SpriteImportMode.Single;
		importer.textureCompression = TextureImporterCompression.Uncompressed;
		var platform = importer.GetDefaultPlatformTextureSettings();
		platform.format = TextureImporterFormat.RGBA32;
		importer.SetPlatformTextureSettings(platform);
		importer.SaveAndReimport();
		return AssetDatabase.LoadAssetAtPath<Sprite>(path);
	}

	private static void SetSize(RectTransform rect, float width, float height) {
		rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
		rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
	}

	private VertexHelper Build(bool rich = true) {
		var vh = new VertexHelper();
		if(rich) Populate.Invoke(_image, new object[] { vh });
		else {
			var go = Track(new GameObject("Standard Image comparison", typeof(RectTransform), typeof(CanvasRenderer)));
			go.SetActive(false);
			go.transform.SetParent(_canvas.transform, false);
			var standard = go.AddComponent<UnityEngine.UI.Image>();
			standard.sprite = _image.sprite;
			standard.overrideSprite = _image.overrideSprite;
			standard.type = _image.type;
			standard.color = _image.color;
			standard.preserveAspect = _image.preserveAspect;
			standard.useSpriteMesh = _image.useSpriteMesh;
			standard.fillCenter = _image.fillCenter;
			standard.fillMethod = _image.fillMethod;
			standard.fillOrigin = _image.fillOrigin;
			standard.fillAmount = _image.fillAmount;
			standard.fillClockwise = _image.fillClockwise;
			standard.pixelsPerUnitMultiplier = _image.pixelsPerUnitMultiplier;
			standard.rectTransform.pivot = _image.rectTransform.pivot;
			SetSize(standard.rectTransform, _image.rectTransform.rect.width, _image.rectTransform.rect.height);
			PopulateImage.Invoke(standard, new object[] { vh });
		}
		return vh;
	}

	private static UIVertex Vertex(VertexHelper vh, int i) {
		var vertex = UIVertex.simpleVert;
		vh.PopulateUIVertex(ref vertex, i);
		return vertex;
	}

	private static Rect Bounds(VertexHelper vh) {
		var min = Vector2.one * float.PositiveInfinity;
		var max = Vector2.one * float.NegativeInfinity;
		for(var i = 0; i < vh.currentVertCount; i++) {
			min = Vector2.Min(min, Vertex(vh, i).position);
			max = Vector2.Max(max, Vertex(vh, i).position);
		}
		return new Rect(min, max - min);
	}

	private static void AssertSameVertices(VertexHelper actual, VertexHelper expected) {
		Assert.That(actual.currentVertCount, Is.EqualTo(expected.currentVertCount));
		for(var i = 0; i < actual.currentVertCount; i++) {
			Assert.That(Vertex(actual, i).position, Is.EqualTo(Vertex(expected, i).position));
			Assert.That(Vertex(actual, i).uv0, Is.EqualTo(Vertex(expected, i).uv0));
			Assert.That(Vertex(actual, i).color, Is.EqualTo(Vertex(expected, i).color));
		}
	}

	private Color[] RenderMesh(bool rich = true, float extraPadding = 0, float scale = 1) {
		Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "GPU rendering is required");
		var mesh = Track(new Mesh());
		using(var vh = Build(rich)) {
			if(extraPadding > 0) {
				var bounds = Bounds(vh);
				for(var i = 0; i < vh.currentVertCount; i++) {
					var vertex = Vertex(vh, i);
					var delta = new Vector2(vertex.position.x == bounds.xMin ? -extraPadding : extraPadding,
						vertex.position.y == bounds.yMin ? -extraPadding : extraPadding);
					vertex.position += (Vector3)delta;
					vertex.uv0.x += vertex.uv2.x * delta.x + vertex.uv2.z * delta.y;
					vertex.uv0.y += vertex.uv2.y * delta.x + vertex.uv2.w * delta.y;
					vertex.uv0.z += delta.x / _image.rectTransform.rect.width;
					vertex.uv0.w += delta.y / _image.rectTransform.rect.height;
					vh.SetUIVertex(vertex, i);
				}
			}
			vh.FillMesh(mesh);
		}
		var renderMaterial = rich ? _material : Track(new Material(Shader.Find("UI/Default")));
		renderMaterial.SetTexture("_MainTex", _image.mainTexture);
		var target = RenderTexture.GetTemporary(256, 256, 24, RenderTextureFormat.ARGB32);
		var previous = RenderTexture.active;
		GL.PushMatrix();
		try {
			RenderTexture.active = target;
			GL.Clear(true, true, Color.clear);
			GL.LoadPixelMatrix(-128, 128, -128, 128);
			renderMaterial.SetPass(0);
			Graphics.DrawMeshNow(mesh, Matrix4x4.TRS(Vector3.zero, _image.rectTransform.localRotation, Vector3.one * scale));
			var readback = Track(new Texture2D(256, 256, TextureFormat.RGBA32, false));
			readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
			readback.Apply();
			return readback.GetPixels();
		} finally {
			GL.PopMatrix();
			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(target);
		}
	}

	private Color[] RenderCanvas() {
		var camera = Track(new GameObject("RichUI verification camera", typeof(Camera))).GetComponent<Camera>();
		camera.transform.position = new Vector3(0, 0, -10);
		camera.orthographic = true;
		camera.orthographicSize = 128;
		camera.aspect = 1;
		camera.clearFlags = CameraClearFlags.SolidColor;
		camera.backgroundColor = Color.clear;
		camera.cullingMask = 1 << 31;
		camera.nearClipPlane = 1;
		camera.farClipPlane = 20;
		foreach(var node in _canvas.GetComponentsInChildren<Transform>()) node.gameObject.layer = 31;
		var target = RenderTexture.GetTemporary(256, 256, 24, RenderTextureFormat.ARGB32);
		var previous = RenderTexture.active;
		try {
			camera.targetTexture = target;
			_canvas.worldCamera = camera;
			Canvas.ForceUpdateCanvases();
			camera.Render();
			RenderTexture.active = target;
			var readback = Track(new Texture2D(256, 256, TextureFormat.RGBA32, false));
			readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
			readback.Apply();
			return readback.GetPixels();
		} finally {
			_canvas.worldCamera = _pixelCamera;
			camera.targetTexture = null;
			RenderTexture.active = previous;
			RenderTexture.ReleaseTemporary(target);
		}
	}

	private static Color Pixel(Color[] pixels, int x, int y) => pixels[(y + 128) * 256 + x + 128];
}
