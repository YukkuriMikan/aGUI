using UnityEngine;

namespace ANest.UI {
	/// <summary> 子要素を水平方向に配置するレイアウトグループ </summary>
	public class aLayoutGroupHorizontal : aLayoutGroupLinear {
		#region Methods
		/// <summary>子要素を横方向に並べ、スケールやパディング、揃え、強制拡張設定を踏まえて重みに応じた幅配分とNavigation再設定を行うレイアウト計算。</summary>
		protected override void CalculateLayout() {
			if(RectTransform == null) return;

			int count = rectChildren.Count;
			if(count == 0) return;

			float availableWidth = RectTransform.rect.width - padding.horizontal; // パディングを除いた幅
			float availableHeight = RectTransform.rect.height - padding.vertical; // パディングを除いた高さ
			float spacingTotal = spacing * (count - 1); // 要素間スペース合計

			float totalWeight = 0f;
			for (int i = 0; i < count; i++) {
				var child = rectChildren[i];
				bool usesSlot = childControlWidth || childForceExpandWidth;
				if(usesSlot) {
					float scaleXWeight = childScaleWidth ? Mathf.Abs(child.localScale.x) : 1f;
					totalWeight += scaleXWeight; // スケールを重みとして加算
				}
			}
			if(totalWeight <= 0f) totalWeight = count;
			float slotWidthPerWeight = (availableWidth - spacingTotal) / totalWeight; // 重み1あたりの割当幅

			float usedMain = spacingTotal; // 主軸で使用する合計長さ（スペース含む）
			float[] allocatedSlots = new float[count];
			float[] allocatedSlotsScaled = new float[count];
			float[] finalWidths = new float[count];
			float[] finalHeights = new float[count];
			float[] crossPositions = new float[count];

			float alignmentX = GetAlignmentOnAxis(0);
			float alignmentY = GetAlignmentOnAxis(1);

			for (int i = 0; i < count; i++) {
				var child = rectChildren[i];
				GetChildSizes(child, 0, childControlWidth, childForceExpandWidth, out var sizeX);
				GetChildSizes(child, 1, childControlHeight, childForceExpandHeight, out var sizeY);
				float scaleX = childScaleWidth ? Mathf.Abs(child.localScale.x) : 1f;
				float scaleY = childScaleHeight ? Mathf.Abs(child.localScale.y) : 1f;

				bool usesSlot = childControlWidth || childForceExpandWidth;
				float weightX = usesSlot ? (childScaleWidth ? Mathf.Abs(child.localScale.x) : 1f) : 0f;
				float allocatedScaled = usesSlot ? slotWidthPerWeight * weightX : sizeX.preferred * scaleX; // スケール込みの割当
				float allocated = usesSlot ? allocatedScaled / Mathf.Max(0.0001f, scaleX) : sizeX.preferred; // 実寸での割当
				float childWidth = childControlWidth ? allocated : sizeX.preferred; // 最終幅

				float minY = sizeY.min * scaleY;
				float prefY = sizeY.preferred * scaleY;
				float flexY = sizeY.flexible * scaleY;
				float requiredSpaceScaled = Mathf.Clamp(availableHeight, minY, flexY > 0f ? RectTransform.rect.height : prefY); // 高さの必要量（スケール込み）
				float requiredSpace = requiredSpaceScaled / Mathf.Max(0.0001f, scaleY); // 実寸高さ
				float startOffsetY = GetStartOffset(1, requiredSpaceScaled); // クロス軸の開始位置
				float childHeight = childControlHeight ? requiredSpace : sizeY.preferred;
				float childHeightScaled = childHeight * scaleY;
				float posY = childControlHeight ? startOffsetY : startOffsetY + (requiredSpaceScaled - childHeightScaled) * alignmentY; // 整列後のY位置

				allocatedSlots[i] = allocated;
				allocatedSlotsScaled[i] = allocatedScaled;
				finalWidths[i] = childWidth;
				finalHeights[i] = childHeight;
				crossPositions[i] = posY;
				usedMain += (childControlWidth || childForceExpandWidth) ? allocatedScaled : childWidth * scaleX; // 主軸使用量を加算
			}

			float startX = GetStartOffset(0, usedMain); // 主軸開始位置
			float posX = startX;

			var order = new System.Collections.Generic.List<RectTransform>(count);
			for (int i = 0; i < count; i++) {
				int idx = reverseArrangement ? (count - 1 - i) : i;
				order.Add(rectChildren[idx]); // 並び順を反転する場合に対応
			}

			for (int i = 0; i < count; i++) {
				var child = order[i];
				float scaleX = childScaleWidth ? child.localScale.x : 1f;
				float scaleY = childScaleHeight ? child.localScale.y : 1f;

				int srcIndex = reverseArrangement ? (count - 1 - i) : i; // 元の配列インデックス
				float alignedPosX = posX + (allocatedSlotsScaled[srcIndex] - finalWidths[srcIndex] * scaleX) * alignmentX; // 整列後のX位置
				SetChildAlongBothAxes(child, alignedPosX, crossPositions[srcIndex], finalWidths[srcIndex], finalHeights[srcIndex], scaleX, scaleY);
				posX += allocatedSlotsScaled[srcIndex] + spacing; // 次の子の配置位置に進める
			}

			ApplyNavigationLinear(order, true);
		}
		#endregion
	}
}
