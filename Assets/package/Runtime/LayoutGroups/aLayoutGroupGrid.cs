using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace ANest.UI {
	/// <summary>グリッド状に子要素を配置するカスタムレイアウトグループ</summary>
	[Icon("d_GridLayoutGroup Icon")]
	public class aLayoutGroupGrid : aLayoutGroupBase {
		/// <summary>グリッド配置の開始コーナー</summary>
		public enum Corner {
			UpperLeft = 0,  // 左上
			UpperRight = 1, // 右上
			LowerLeft = 2,  // 左下
			LowerRight = 3  // 右下
		}
		/// <summary>主軸の方向</summary>
		public enum Axis {
			Horizontal = 0, // 横方向
			Vertical = 1    // 縦方向
		}
		/// <summary>グリッドの制約種別</summary>
		public enum Constraint {
			Flexible,          // 可変（領域に収まるだけ配置）
			FixedColumnCount,   // 列数固定
			FixedRowCount       // 行数固定
		}

		#region SerializeField
		[Tooltip("配置開始コーナー")]
		[SerializeField] private Corner startCorner = Corner.UpperLeft;          // 配置開始コーナー
		[Tooltip("主軸方向（横 or 縦）")]
		[SerializeField] private Axis startAxis = Axis.Horizontal;                // 主軸方向（横 or 縦）
		[Tooltip("セルのサイズ")]
		[SerializeField] private Vector2 cellSize = new Vector2(100f, 100f);      // セルのサイズ
		[Tooltip("セル間スペース（X, Y）")]
		[SerializeField] private Vector2 spacingXY = Vector2.zero;                // セル間スペース（X, Y）
		[Tooltip("グリッド制約設定")]
		[SerializeField] private Constraint constraint = Constraint.Flexible;      // グリッド制約設定
		[Tooltip("制約値（列・行固定時の数）")]
		[SerializeField] private int constraintCount = 2;                         // 制約値（列・行固定時の数）
		#endregion

		#region Fields
		private readonly List<RectTransform> m_orderedChildren = new();  // reverseArrangement 反映済みの子要素バッファ
		private readonly List<List<RectTransform>> m_lines = new();       // 行（または列）単位の子要素バッファ
		private readonly List<float> m_lineCrossSizes = new();            // 各ラインの cross 軸サイズ
		private readonly List<float> m_allocatedMainScaled = new();       // 各子要素の main 軸割当スロット（scale適用後）
		private readonly List<float> m_finalMain = new();                 // 各子要素の main 軸最終サイズ（scale適用前）
		private readonly List<float> m_finalCross = new();                // 各子要素の cross 軸最終サイズ（scale適用前）
		private readonly List<float> m_scaleMain = new();                 // 各子要素の main 軸スケール
		private readonly List<float> m_scaleCross = new();                // 各子要素の cross 軸スケール
		#endregion

		#region Methods
		/// <summary>指定インデックスのラインを取得し、未作成なら生成して返す</summary>
		private List<RectTransform> GetOrCreateLine(int index) {
			while(m_lines.Count <= index) m_lines.Add(new List<RectTransform>());
			var line = m_lines[index];
			line.Clear();
			return line;
		}

		/// <summary>再利用用 float リストのサイズを指定数に合わせる</summary>
		private static void EnsureFloatListSize(List<float> list, int count) {
			if(list.Count > count) {
				list.RemoveRange(count, list.Count - count);
				return;
			}
			for (int i = list.Count; i < count; i++) list.Add(0f);
		}

		/// <summary>現在設定に基づき、子要素をグリッド状に再配置する</summary>
		protected override void CalculateLayout() {
			if(RectTransform == null) return;

			int count = rectChildren.Count;
			if(count == 0) return;

			float availableWidth = RectTransform.rect.width - padding.horizontal;
			float availableHeight = RectTransform.rect.height - padding.vertical;
			int mainAxis = startAxis == Axis.Horizontal ? 0 : 1;
			int crossAxis = startAxis == Axis.Horizontal ? 1 : 0;
			float mainSpacing = startAxis == Axis.Horizontal ? spacingXY.x : spacingXY.y;
			float crossSpacing = startAxis == Axis.Horizontal ? spacingXY.y : spacingXY.x;
			float availableMain = mainAxis == 0 ? availableWidth : availableHeight;
			float availableCross = crossAxis == 0 ? availableWidth : availableHeight;
			float alignMain = GetAlignmentOnAxis(mainAxis);
			float alignCross = GetAlignmentOnAxis(crossAxis);

			bool controlMain = mainAxis == 0 ? childControlWidth : childControlHeight;
			bool controlCross = crossAxis == 0 ? childControlWidth : childControlHeight;
			bool forceMain = mainAxis == 0 ? childForceExpandWidth : childForceExpandHeight;
			bool forceCross = crossAxis == 0 ? childForceExpandWidth : childForceExpandHeight;
			bool scaleMainEnabled = mainAxis == 0 ? childScaleWidth : childScaleHeight;
			bool scaleCrossEnabled = crossAxis == 0 ? childScaleWidth : childScaleHeight;
			bool isFixedConstraint = constraint == Constraint.FixedColumnCount || constraint == Constraint.FixedRowCount;
			bool isConstraintAxisMatched =
				(constraint == Constraint.FixedColumnCount && startAxis == Axis.Horizontal)
				|| (constraint == Constraint.FixedRowCount && startAxis == Axis.Vertical);
			bool useConstraintMainFill = controlMain && forceMain && isFixedConstraint && isConstraintAxisMatched;

			int cornerX = (int)startCorner % 2;
			int cornerY = (int)startCorner / 2;

			// 既存バッファを使い回すため、前回分のラインデータをクリア
			for (int i = 0; i < m_lines.Count; i++) m_lines[i].Clear();

			// 表示順（reverseArrangement 含む）で子要素バッファを再構築
			m_orderedChildren.Clear();
			if(m_orderedChildren.Capacity < count) m_orderedChildren.Capacity = count;
			for (int i = 0; i < count; i++) {
				int srcIndex = reverseArrangement ? (count - 1 - i) : i;
				var child = rectChildren[srcIndex];
				if(child != null) m_orderedChildren.Add(child);
			}

			if(m_orderedChildren.Count == 0) return;
			count = m_orderedChildren.Count;

			int columns;
			int rows;
			int lineCount;
			int usedLineCount = 0;
				switch (constraint) {
					case Constraint.FixedColumnCount:
						// 列数固定: 行数を算出してラインへ振り分け
						columns = Mathf.Max(1, constraintCount);
						rows = Mathf.CeilToInt(count / (float)columns);
						lineCount = startAxis == Axis.Horizontal ? rows : columns;
						int slotsPerLine = startAxis == Axis.Horizontal ? columns : rows;
						for (int i = 0; i < lineCount; i++) {
							var line = GetOrCreateLine(i);
							for (int j = 0; j < slotsPerLine; j++) line.Add(null);
						}
						usedLineCount = lineCount;
						for (int i = 0; i < count; i++) {
							var child = m_orderedChildren[i];
							int rawCol = i % columns;
							int rawRow = i / columns;
							int col = cornerX == 0 ? rawCol : (columns - 1 - rawCol);
							int row = cornerY == 0 ? rawRow : (rows - 1 - rawRow);
							int lineIndex = startAxis == Axis.Horizontal ? row : col;
							int inLineIndex = startAxis == Axis.Horizontal ? col : row;
							if(lineIndex < 0 || lineIndex >= lineCount) continue;
							if(inLineIndex < 0 || inLineIndex >= slotsPerLine) continue;
							m_lines[lineIndex][inLineIndex] = child;
						}
						for (int i = 0; i < lineCount; i++) {
							var line = m_lines[i];
							for (int j = line.Count - 1; j >= 0; j--) {
								if(line[j] == null) line.RemoveAt(j);
							}
						}
						break;
					case Constraint.FixedRowCount:
						// 行数固定: 列数を算出してラインへ振り分け
						rows = Mathf.Max(1, constraintCount);
						columns = Mathf.CeilToInt(count / (float)rows);
						lineCount = startAxis == Axis.Horizontal ? rows : columns;
						slotsPerLine = startAxis == Axis.Horizontal ? columns : rows;
						for (int i = 0; i < lineCount; i++) {
							var line = GetOrCreateLine(i);
							for (int j = 0; j < slotsPerLine; j++) line.Add(null);
						}
						usedLineCount = lineCount;
						for (int i = 0; i < count; i++) {
							var child = m_orderedChildren[i];
							int rawCol = i / rows;
							int rawRow = i % rows;
							int col = cornerX == 0 ? rawCol : (columns - 1 - rawCol);
							int row = cornerY == 0 ? rawRow : (rows - 1 - rawRow);
							int lineIndex = startAxis == Axis.Horizontal ? row : col;
							int inLineIndex = startAxis == Axis.Horizontal ? col : row;
							if(lineIndex < 0 || lineIndex >= lineCount) continue;
							if(inLineIndex < 0 || inLineIndex >= slotsPerLine) continue;
							m_lines[lineIndex][inLineIndex] = child;
						}
						for (int i = 0; i < lineCount; i++) {
							var line = m_lines[i];
							for (int j = line.Count - 1; j >= 0; j--) {
								if(line[j] == null) line.RemoveAt(j);
							}
						}
						break;
				default:
					// 可変: main 軸の空き幅に収まらなくなったら改行（折返し）
					var currentLine = GetOrCreateLine(0);
					usedLineCount = 1;
					float currentMainUsed = 0f;
					for (int i = 0; i < count; i++) {
						var child = m_orderedChildren[i];
						GetChildSizes(child, mainAxis, controlMain, forceMain, out var sizeMain);
						float sMain = scaleMainEnabled ? Mathf.Abs(mainAxis == 0 ? child.localScale.x : child.localScale.y) : 1f;
						float preferredMain = controlMain ? (mainAxis == 0 ? cellSize.x : cellSize.y) : sizeMain.preferred;
						float childMainScaled = Mathf.Max(0f, preferredMain * sMain);

						float required = currentLine.Count > 0 ? currentMainUsed + mainSpacing + childMainScaled : childMainScaled;
						bool shouldWrap = currentLine.Count > 0 && required > availableMain + 0.001f;
						if(shouldWrap) {
							currentLine = GetOrCreateLine(usedLineCount);
							usedLineCount++;
							currentMainUsed = 0f;
						}

						currentLine.Add(child);
						currentMainUsed = currentLine.Count == 1 ? childMainScaled : currentMainUsed + mainSpacing + childMainScaled;
					}
					if(currentLine.Count == 0 && usedLineCount > 0) usedLineCount--;

					if(startAxis == Axis.Horizontal) {
						if(cornerX == 1) {
							for (int i = 0; i < usedLineCount; i++) m_lines[i].Reverse();
						}
						if(cornerY == 1) m_lines.Reverse(0, usedLineCount);
					} else {
						if(cornerY == 1) {
							for (int i = 0; i < usedLineCount; i++) m_lines[i].Reverse();
						}
						if(cornerX == 1) m_lines.Reverse(0, usedLineCount);
					}
					break;
			}

			lineCount = usedLineCount;
			if(lineCount <= 0) return;

			// 各ラインの cross 軸必要サイズを算出
			EnsureFloatListSize(m_lineCrossSizes, lineCount);
			for (int line = 0; line < lineCount; line++) {
				var lineChildren = m_lines[line];
				float lineCross = 0f;
				for (int i = 0; i < lineChildren.Count; i++) {
					var child = lineChildren[i];
					GetChildSizes(child, crossAxis, controlCross, forceCross, out var sizeCross);
					float scaleCross = scaleCrossEnabled ? Mathf.Abs(crossAxis == 0 ? child.localScale.x : child.localScale.y) : 1f;
					float preferredCross = controlCross ? (crossAxis == 0 ? cellSize.x : cellSize.y) : sizeCross.preferred;
					lineCross = Mathf.Max(lineCross, preferredCross * scaleCross);
				}
				m_lineCrossSizes[line] = lineCross;
			}

			if(forceCross) {
				// forceExpand が有効なら cross 軸を等分配
				float expanded = Mathf.Max(0f, (availableCross - crossSpacing * (lineCount - 1)) / Mathf.Max(1, lineCount));
				for (int i = 0; i < lineCount; i++) m_lineCrossSizes[i] = expanded;
			}

			float requiredCross = crossSpacing * Mathf.Max(0, lineCount - 1);
			for (int i = 0; i < lineCount; i++) requiredCross += m_lineCrossSizes[i];
			float crossCursor = GetStartOffset(crossAxis, requiredCross);

			for (int line = 0; line < lineCount; line++) {
				var lineChildren = m_lines[line];
				int lineChildCount = lineChildren.Count;
				if(lineChildCount == 0) {
					crossCursor += m_lineCrossSizes[line] + crossSpacing;
					continue;
				}

				// ライン内処理用ワークバッファを再利用
				EnsureFloatListSize(m_allocatedMainScaled, lineChildCount);
				EnsureFloatListSize(m_finalMain, lineChildCount);
				EnsureFloatListSize(m_finalCross, lineChildCount);
				EnsureFloatListSize(m_scaleMain, lineChildCount);
				EnsureFloatListSize(m_scaleCross, lineChildCount);

				// main 軸の割当スロット（scale考慮）を計算
				float spacingSlotCount = useConstraintMainFill ? Mathf.Max(0, Mathf.Max(1, constraintCount) - 1) : Mathf.Max(0, lineChildCount - 1);
				float spacingTotal = mainSpacing * spacingSlotCount;
				float totalWeight = 0f;
				for (int i = 0; i < lineChildCount; i++) {
					var child = lineChildren[i];
					float sMain = scaleMainEnabled ? Mathf.Abs(mainAxis == 0 ? child.localScale.x : child.localScale.y) : 1f;
					m_scaleMain[i] = sMain;
					if(controlMain || forceMain) totalWeight += sMain;
				}
				if(totalWeight <= 0f) totalWeight = lineChildCount;
				float slotPerWeight = useConstraintMainFill
					? (availableMain - spacingTotal) / Mathf.Max(1, constraintCount)
					: (availableMain - spacingTotal) / totalWeight;

				float usedMain = spacingTotal;
				float lineCrossSize = m_lineCrossSizes[line];
				for (int i = 0; i < lineChildCount; i++) {
					var child = lineChildren[i];
					GetChildSizes(child, mainAxis, controlMain, forceMain, out var sizeMain);
					GetChildSizes(child, crossAxis, controlCross, forceCross, out var sizeC);

					float sMain = m_scaleMain[i];
					float sCross = scaleCrossEnabled ? Mathf.Abs(crossAxis == 0 ? child.localScale.x : child.localScale.y) : 1f;
					m_scaleCross[i] = sCross;

					float preferredMain = controlMain ? (mainAxis == 0 ? cellSize.x : cellSize.y) : sizeMain.preferred;
					float preferredCross = controlCross ? (crossAxis == 0 ? cellSize.x : cellSize.y) : sizeC.preferred;

					float allocatedScaled = (controlMain || forceMain) ? slotPerWeight * sMain : preferredMain * sMain;
					float childMain;
					if(useConstraintMainFill) {
						allocatedScaled = slotPerWeight;
						childMain = slotPerWeight / Mathf.Max(0.0001f, sMain);
					} else if(controlMain) {
						// ControlChildSize=true の場合は forceExpand の有無に関わらず cellSize を最終サイズとして使用する
						childMain = preferredMain;
						// forceExpand が無効な場合はスロット幅も実サイズに揃える
						if(!forceMain) allocatedScaled = childMain * sMain;
					} else {
						childMain = preferredMain;
					}

					float childCrossScaled = controlCross ? lineCrossSize : preferredCross * sCross;
					float childCross = controlCross ? childCrossScaled / Mathf.Max(0.0001f, sCross) : preferredCross;

					m_allocatedMainScaled[i] = allocatedScaled;
					m_finalMain[i] = childMain;
					m_finalCross[i] = childCross;
					usedMain += (controlMain || forceMain) ? allocatedScaled : childMain * sMain;
				}

				// ライン内の最終配置
				float mainCursor = GetStartOffset(mainAxis, usedMain);
				for (int i = 0; i < lineChildCount; i++) {
					var child = lineChildren[i];
					float sMain = m_scaleMain[i];
					float sCross = m_scaleCross[i];

					float childMainScaled = m_finalMain[i] * sMain;
					float childCrossScaled = m_finalCross[i] * sCross;
					float alignedMain = mainCursor + (m_allocatedMainScaled[i] - childMainScaled) * alignMain;
					float alignedCross = crossCursor + (m_lineCrossSizes[line] - childCrossScaled) * alignCross;

					if(startAxis == Axis.Horizontal) {
						SetChildAlongBothAxes(child, alignedMain, alignedCross, m_finalMain[i], m_finalCross[i], sMain, sCross);
					} else {
						SetChildAlongBothAxes(child, alignedCross, alignedMain, m_finalCross[i], m_finalMain[i], sCross, sMain);
					}

					mainCursor += m_allocatedMainScaled[i] + mainSpacing;
				}

				crossCursor += m_lineCrossSizes[line] + crossSpacing;
			}
		}
		#endregion
	}
}
