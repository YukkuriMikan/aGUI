using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace ANest.UI {
	/// <summary>
	/// RichUI の頂点データと描画余白を供給する Image。
	/// 外周拡張は Simple (Use Sprite Mesh OFF) / Sliced / Tiled に対応する。
	/// Filled / Use Sprite Mesh ON は標準メッシュを維持し、外側の効果はクリップされる。
	/// </summary>
	/// <remarks>
	/// 非 RichUI の Material.shader を実行中に直接差し替えた場合は SetMaterialDirty() で通知する。
	/// RichUI 未使用時は描画前イベントへ登録せず、マテリアル変更通知で使用状態を更新する。
	/// </remarks>
	[ExecuteAlways]
	public class aImage : Image {
		private static readonly int OutlineEnabled = Shader.PropertyToID("_OutlineEnabled");
		private static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
		private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
		private static readonly int ShadowEnabled = Shader.PropertyToID("_ShadowEnabled");
		private static readonly int ShadowOffsetX = Shader.PropertyToID("_ShadowOffsetX");
		private static readonly int ShadowOffsetY = Shader.PropertyToID("_ShadowOffsetY");
		private static readonly int ShadowSoftness = Shader.PropertyToID("_ShadowSoftness");
		private static readonly int ShadowSpread = Shader.PropertyToID("_ShadowSpread");
		private static readonly int ShadowColor = Shader.PropertyToID("_ShadowColor");
		private static readonly int[] RichUIFloats = {
			Shader.PropertyToID("_Brightness"), Shader.PropertyToID("_RecolorAmount"),
			OutlineEnabled, OutlineWidth, Shader.PropertyToID("_UseOutlineTexture"),
			ShadowEnabled, ShadowOffsetX, ShadowOffsetY, ShadowSoftness, ShadowSpread,
			Shader.PropertyToID("_ShadowSamples")
		};
		private static readonly int[] RichUIColors = {
			Shader.PropertyToID("_Color"), Shader.PropertyToID("_RecolorColor"),
			OutlineColor, ShadowColor
		};
		private static readonly int OutlineTex = Shader.PropertyToID("_OutlineTex");
		private const AdditionalCanvasShaderChannels RichUIChannels =
			AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;

		private Material _observedMaterial;
		private Shader _observedShader;
		private bool _usesRichUI;
		private bool _monitoringRichUI;
		private bool _richUIMaterialChanged;
		private float[] _observedFloats;
		private Color[] _observedColors;
		private Texture _observedOutlineTexture;
		private Vector2 _observedOutlineScale, _observedOutlineOffset;
		// left, bottom, right, top in local UI units.
		private Vector4 _richUIPadding;
		private Rect _richUIDrawRect;
		private bool _hasRichUIDrawRect;

		public override void SetMaterialDirty() {
			// Image's setter, Inspector changes and animation all notify here.
			// In-place shader replacements also use this standard Graphic API.
			UpdateRichUIUsage();
			base.SetMaterialDirty();
		}

		protected override void OnEnable() {
			base.OnEnable();
			RefreshRichUI();
		}

		protected override void OnDisable() {
			SetRichUIMonitoring(false);
			base.OnDisable();
			_observedMaterial = null;
			_observedShader = null;
			_usesRichUI = false;
			_richUIMaterialChanged = false;
			_hasRichUIDrawRect = false;
		}

		protected override void OnCanvasHierarchyChanged() {
			base.OnCanvasHierarchyChanged();
			if(_usesRichUI) EnableRichUIChannels();
		}

		protected override void OnTransformParentChanged() {
			base.OnTransformParentChanged();
			if(_usesRichUI) EnableRichUIChannels();
		}

		private static bool IsRichUI(Material source) => source && source.shader && source.shader.name == "aGUI/RichUI";

		private void EnableRichUIChannels() {
			var owner = canvas;
			if(owner && (owner.additionalShaderChannels & RichUIChannels) != RichUIChannels)
				owner.additionalShaderChannels |= RichUIChannels;
		}

		private void SetRichUIMonitoring(bool monitor) {
			if(_monitoringRichUI == monitor) return;
			_monitoringRichUI = monitor;
			if(monitor) Canvas.preWillRenderCanvases += RefreshRichUI;
			else Canvas.preWillRenderCanvases -= RefreshRichUI;
		}

		private bool UpdateRichUIUsage() {
			var source = material;
			var shader = source ? source.shader : null;
			var materialChanged = source != _observedMaterial || shader != _observedShader;
			var wasRichUI = _usesRichUI;
			// Shader.name crosses into native code and returns a string. Resolve it
			// only when the shader changes, never in ordinary mesh/culling calls.
			if(shader != _observedShader) _usesRichUI = shader && shader.name == "aGUI/RichUI";
			_observedMaterial = source;
			_observedShader = shader;
			SetRichUIMonitoring(_usesRichUI && isActiveAndEnabled);
			if(materialChanged && (wasRichUI || _usesRichUI)) {
				_richUIMaterialChanged = true;
				if(!_usesRichUI) _richUIPadding = Vector4.zero;
				SetVerticesDirty();
			}
			return materialChanged;
		}

		private void RefreshRichUI() {
			var materialChanged = UpdateRichUIUsage();
			if(!_usesRichUI) {
				if(materialChanged) base.SetMaterialDirty();
				return;
			}

			EnableRichUIChannels();
			var source = _observedMaterial;
			if(_observedFloats == null) {
				_observedFloats = new float[RichUIFloats.Length];
				_observedColors = new Color[RichUIColors.Length];
			}
			var changed = _richUIMaterialChanged;
			_richUIMaterialChanged = false;
			for(var i = 0; i < RichUIFloats.Length; i++) {
				var value = source.GetFloat(RichUIFloats[i]);
				changed |= !value.Equals(_observedFloats[i]);
				_observedFloats[i] = value;
			}
			for(var i = 0; i < RichUIColors.Length; i++) {
				var value = source.GetColor(RichUIColors[i]);
				changed |= !value.Equals(_observedColors[i]);
				_observedColors[i] = value;
			}
			var texture = source.GetTexture(OutlineTex);
			var scale = source.GetTextureScale(OutlineTex);
			var offset = source.GetTextureOffset(OutlineTex);
			changed |= texture != _observedOutlineTexture || !scale.Equals(_observedOutlineScale) || !offset.Equals(_observedOutlineOffset);
			_observedOutlineTexture = texture;
			_observedOutlineScale = scale;
			_observedOutlineOffset = offset;

			var padding = CalculateRichUIPadding(source);
			if(!padding.Equals(_richUIPadding)) {
				_richUIPadding = padding;
				_hasRichUIDrawRect = false;
				SetVerticesDirty();
			}
			if(changed) base.SetMaterialDirty();
		}

		private static Vector4 CalculateRichUIPadding(Material source) {
			var outline = source.GetFloat(OutlineEnabled) > 0.5f && source.GetColor(OutlineColor).a > 0
				? Mathf.Max(0, source.GetFloat(OutlineWidth)) : 0;
			var padding = Vector4.one * outline;
			if(source.GetFloat(ShadowEnabled) > 0.5f && source.GetColor(ShadowColor).a > 0) {
				var radius = Mathf.Max(0, source.GetFloat(ShadowSoftness)) + Mathf.Max(0, source.GetFloat(ShadowSpread));
				var x = source.GetFloat(ShadowOffsetX);
				var y = source.GetFloat(ShadowOffsetY);
				padding.x = Mathf.Max(outline, radius - x);
				padding.y = Mathf.Max(outline, radius - y);
				padding.z = Mathf.Max(outline, radius + x);
				padding.w = Mathf.Max(outline, radius + y);
			}
			return padding;
		}

		public override void SetVerticesDirty() {
			// Resizing or replacing a culled Image must invalidate its old bounds
			// before RectMask2D decides whether it can rebuild the new geometry.
			_hasRichUIDrawRect = false;
			base.SetVerticesDirty();
		}

		public override Material GetModifiedMaterial(Material baseMaterial) {
			var modified = base.GetModifiedMaterial(baseMaterial);
			if(_usesRichUI) CopyRichUIProperties(baseMaterial, modified);
			return modified;
		}

		protected override void UpdateMaterial() {
			base.UpdateMaterial();
			// A Mask on this Image is another IMaterialModifier, applied after us.
			// Update its cached stencil and pop materials as well, without owning copies.
			if(!_usesRichUI) return;
			var source = material;
			CopyRichUIProperties(source, canvasRenderer.GetMaterial());
			for(var i = 0; i < canvasRenderer.popMaterialCount; i++)
				CopyRichUIProperties(source, canvasRenderer.GetPopMaterial(i));
		}

		private static void CopyRichUIProperties(Material source, Material destination) {
			if(source == destination || !IsRichUI(source) || !destination || destination.shader != source.shader) return;
			// Copy only effect properties. Stencil, alpha-clip, clip rect and keywords
			// belong to uGUI and must survive parameter changes unchanged.
			foreach(var id in RichUIFloats) destination.SetFloat(id, source.GetFloat(id));
			foreach(var id in RichUIColors) destination.SetColor(id, source.GetColor(id));
			destination.SetTexture(OutlineTex, source.GetTexture(OutlineTex));
			destination.SetTextureScale(OutlineTex, source.GetTextureScale(OutlineTex));
			destination.SetTextureOffset(OutlineTex, source.GetTextureOffset(OutlineTex));
		}

		protected override void OnPopulateMesh(VertexHelper vh) {
			base.OnPopulateMesh(vh);
			_hasRichUIDrawRect = false;
			if(!_usesRichUI || vh.currentVertCount == 0) return;
			EnableRichUIChannels();
			var activeSprite = overrideSprite;
			var uvRect = activeSprite ? DataUtility.GetOuterUV(activeSprite) : new Vector4(0, 0, 1, 1);
			var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
			var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
			var vertex = UIVertex.simpleVert;
			for(var i = 0; i < vh.currentVertCount; i++) {
				vh.PopulateUIVertex(ref vertex, i);
				min = Vector2.Min(min, vertex.position);
				max = Vector2.Max(max, vertex.position);
			}
			var size = max - min;
			if(size.x <= 0 || size.y <= 0) return;

			// These Image modes have independent BL/TL/TR/BR quads. Leave arbitrary
			// sprite triangles and radial fills untouched rather than distorting them.
			var expand = type != Type.Filled && !(type == Type.Simple && useSpriteMesh);
			var padding = expand ? CalculateRichUIPadding(material) : Vector4.zero;
			if(expand) {
				for(var start = 0; start + 3 < vh.currentVertCount; start += 4) {
					var bottomLeft = UIVertex.simpleVert;
					var topLeft = UIVertex.simpleVert;
					var bottomRight = UIVertex.simpleVert;
					vh.PopulateUIVertex(ref bottomLeft, start);
					vh.PopulateUIVertex(ref topLeft, start + 1);
					vh.PopulateUIVertex(ref bottomRight, start + 3);
					var width = bottomRight.position.x - bottomLeft.position.x;
					var height = topLeft.position.y - bottomLeft.position.y;
					if(width <= 0 || height <= 0) continue;
					var dx = (bottomRight.uv0 - bottomLeft.uv0) / width;
					var dy = (topLeft.uv0 - bottomLeft.uv0) / height;
					var bounds = uvRect;
					if(type == Type.Tiled && !hasBorder) {
						// Also covers Image's single-quad Repeat fast path (UVs > 1).
						// A partial last tile must stop at its actual cropped UV edge.
						if(bottomLeft.position.x == min.x) bounds.x = bottomLeft.uv0.x;
						if(bottomLeft.position.y == min.y) bounds.y = bottomLeft.uv0.y;
						if(bottomRight.position.x == max.x) bounds.z = bottomRight.uv0.x;
						if(topLeft.position.y == max.y) bounds.w = topLeft.uv0.y;
					}
					for(var corner = 0; corner < 4; corner++) {
						vh.PopulateUIVertex(ref vertex, start + corner);
						var delta = Vector2.zero;
						if(vertex.position.x == min.x) delta.x -= padding.x;
						if(vertex.position.x == max.x) delta.x += padding.z;
						if(vertex.position.y == min.y) delta.y -= padding.y;
						if(vertex.position.y == max.y) delta.y += padding.w;
						vertex.position += (Vector3)delta;
						vertex.uv0 += dx * delta.x + dy * delta.y;
						SetRichUIVertex(ref vertex, min, size, bounds, new Vector4(dx.x, dx.y, dy.x, dy.y));
						vh.SetUIVertex(vertex, start + corner);
					}
				}
			} else {
				var axes = new Vector4((uvRect.z - uvRect.x) / size.x, 0, 0, (uvRect.w - uvRect.y) / size.y);
				for(var i = 0; i < vh.currentVertCount; i++) {
					vh.PopulateUIVertex(ref vertex, i);
					SetRichUIVertex(ref vertex, min, size, uvRect, axes);
					vh.SetUIVertex(vertex, i);
				}
			}
			_richUIDrawRect = Rect.MinMaxRect(min.x - padding.x, min.y - padding.y, max.x + padding.z, max.y + padding.w);
			_hasRichUIDrawRect = true;
		}

		private static void SetRichUIVertex(ref UIVertex vertex, Vector2 min, Vector2 size, Vector4 bounds, Vector4 axes) {
			vertex.uv0.z = (vertex.position.x - min.x) / size.x;
			vertex.uv0.w = (vertex.position.y - min.y) / size.y;
			vertex.uv1 = bounds;
			vertex.uv2 = axes;
		}

		public override void Cull(Rect clipRect, bool validRect) {
			if(!_usesRichUI || !canvas) {
				base.Cull(clipRect, validRect);
				return;
			}
			// RectMask2D's coarse cull must include decorations that intersect the
			// mask even when the original RectTransform is completely outside it.
			// Clipping runs before geometry rebuilds, including on the first frame.
			var drawRect = _richUIDrawRect;
			if(!_hasRichUIDrawRect) {
				drawRect = GetPixelAdjustedRect();
				if(type != Type.Filled && !(type == Type.Simple && useSpriteMesh)) {
					var padding = CalculateRichUIPadding(material);
					drawRect = Rect.MinMaxRect(drawRect.xMin - padding.x, drawRect.yMin - padding.y,
						drawRect.xMax + padding.z, drawRect.yMax + padding.w);
				}
			}
			var toCanvas = canvas.rootCanvas.transform.worldToLocalMatrix * rectTransform.localToWorldMatrix;
			var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
			var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
			for(var i = 0; i < 4; i++) {
				var point = toCanvas.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? drawRect.xMin : drawRect.xMax,
					(i & 2) == 0 ? drawRect.yMin : drawRect.yMax));
				min = Vector2.Min(min, point);
				max = Vector2.Max(max, point);
			}
			var cull = !validRect || !clipRect.Overlaps(new Rect(min, max - min), true);
			if(canvasRenderer.cull == cull) return;
			canvasRenderer.cull = cull;
			onCullStateChanged.Invoke(cull);
			OnCullingChanged();
		}
	}
}
