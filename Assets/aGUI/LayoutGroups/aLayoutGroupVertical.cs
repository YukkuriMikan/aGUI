using System.Collections.Generic;
using UnityEngine;

namespace ANest.UI {
	/// <summary> 子要素を縦方向に配置するレイアウトグループ </summary>
	public class aLayoutGroupVertical : aLayoutGroupLinear {
		#region Methods
		/// <summary>
		/// 子要素を縦方向に並べ、利用可能な高さを重み・スケール・強制拡張設定に応じて配分するレイアウト計算。
		/// パディング・アライメント・反転配置・Navigation再設定も考慮する。
		/// </summary>
		protected override void CalculateLayout() {
			if(RectTransform == null) return;

			int count = rectChildren.Count;
			if(count == 0) return;

			float availableHeight = RectTransform.rect.height - padding.vertical; // 利用可能な高さ
			float availableWidth = RectTransform.rect.width - padding.horizontal; // 利用可能な幅
			float spacingTotal = spacing * (count - 1); // 要素間スペース合計
			float totalWeight = 0f;
			for (int i = 0; i < count; i++) {
				var child = rectChildren[i];
				bool usesSlot = childControlHeight || childForceExpandHeight;
				if(usesSlot) {
					float scaleYWeight = childScaleHeight ? child.localScale.y : 1f;
					totalWeight += scaleYWeight; // スケールを重みとして加算
				}
			}
			if(totalWeight <= 0f) totalWeight = count;
			float slotHeightPerWeight = (availableHeight - spacingTotal) / totalWeight; // 重み1あたりの割当高さ

			float usedMain = spacingTotal; // 主軸使用量（スペース含む）
			float usedCross = 0f;
			float[] allocatedSlots = new float[count];
			float[] allocatedSlotsScaled = new float[count];
			float[] finalWidths = new float[count];
			float[] finalHeights = new float[count];
			float alignmentY = GetAlignmentOnAxis(1);

			for (int i = 0; i < count; i++) {
				var child = rectChildren[i];
				GetChildSizes(child, 0, childControlWidth, childForceExpandWidth, out var sizeX);
				GetChildSizes(child, 1, childControlHeight, childForceExpandHeight, out var sizeY);
				float scaleX = childScaleWidth ? child.localScale.x : 1f;
				float scaleY = childScaleHeight ? child.localScale.y : 1f;

				bool usesSlot = childControlHeight || childForceExpandHeight;
				float weightY = usesSlot ? (childScaleHeight ? child.localScale.y : 1f) : 0f;
				float allocatedScaled = usesSlot ? slotHeightPerWeight * weightY : sizeY.preferred * scaleY; // スケール込み割当
				float allocated = usesSlot ? allocatedScaled / Mathf.Max(0.0001f, scaleY) : sizeY.preferred; // 実寸割当
				float childHeight = childControlHeight ? allocated : sizeY.preferred;

				float childWidth;
				if(childControlWidth) {
					childWidth = childForceExpandWidth ? availableWidth : sizeX.preferred;
				} else {
					childWidth = sizeX.preferred;
				}

				allocatedSlots[i] = allocated;
				allocatedSlotsScaled[i] = allocatedScaled;
				finalWidths[i] = childWidth;
				finalHeights[i] = childHeight;
				usedMain += (childControlHeight || childForceExpandHeight) ? allocatedScaled : childHeight * scaleY; // 主軸使用量加算
				usedCross = Mathf.Max(usedCross, childWidth * scaleX); // クロス軸で最大幅を記録
			}

			float startX = GetStartOffset(0, usedCross); // クロス軸開始位置
			float startY = GetStartOffset(1, usedMain); // 主軸開始位置
			float posY = startY;

			var order = new List<RectTransform>(count);
			for (int i = 0; i < count; i++) {
				int idx = reverseArrangement ? (count - 1 - i) : i;
				order.Add(rectChildren[idx]); // 並び順反転に対応
			}

			for (int i = 0; i < count; i++) {
				int src = reverseArrangement ? (count - 1 - i) : i; // 元のインデックス
				var child = order[i];
				float scaleX = childScaleWidth ? child.localScale.x : 1f;
				float scaleY = childScaleHeight ? child.localScale.y : 1f;

				float alignedPosY = posY + (allocatedSlotsScaled[src] - finalHeights[src] * scaleY) * alignmentY; // 整列後Y位置
				SetChildAlongBothAxes(child, startX, alignedPosY, finalWidths[src], finalHeights[src], scaleX, scaleY);
				posY += allocatedSlotsScaled[src] + spacing; // 次の要素のY位置に進める
			}

			ApplyNavigationLinear(order, false);
		}
		#endregion
	}
}
