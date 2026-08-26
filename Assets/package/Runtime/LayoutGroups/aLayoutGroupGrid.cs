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
		private readonly List<float> m_lineUsedMain = new();              // 各ラインの main 軸使用量
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

			// 計測フェーズ: 全子要素のサイズ・割当をフラットバッファへ格納し、各ラインの main 軸使用量を算出
			EnsureFloatListSize(m_allocatedMainScaled, count);
			EnsureFloatListSize(m_finalMain, count);
			EnsureFloatListSize(m_finalCross, count);
			EnsureFloatListSize(m_scaleMain, count);
			EnsureFloatListSize(m_scaleCross, count);
			EnsureFloatListSize(m_lineUsedMain, lineCount);

			float maxUsedMain = 0f;
			int flatIndex = 0;
			for (int line = 0; line < lineCount; line++) {
				var lineChildren = m_lines[line];
				int lineChildCount = lineChildren.Count;
				if(lineChildCount == 0) {
					m_lineUsedMain[line] = 0f;
					continue;
				}

				// main 軸の割当スロット（scale考慮）を計算
				float spacingSlotCount = useConstraintMainFill ? Mathf.Max(0, Mathf.Max(1, constraintCount) - 1) : Mathf.Max(0, lineChildCount - 1);
				float spacingTotal = mainSpacing * spacingSlotCount;
				float totalWeight = 0f;
				for (int i = 0; i < lineChildCount; i++) {
					var child = lineChildren[i];
					float sMain = scaleMainEnabled ? Mathf.Abs(mainAxis == 0 ? child.localScale.x : child.localScale.y) : 1f;
					m_scaleMain[flatIndex + i] = sMain;
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

					float sMain = m_scaleMain[flatIndex + i];
					float sCross = scaleCrossEnabled ? Mathf.Abs(crossAxis == 0 ? child.localScale.x : child.localScale.y) : 1f;
					m_scaleCross[flatIndex + i] = sCross;

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

					m_allocatedMainScaled[flatIndex + i] = allocatedScaled;
					m_finalMain[flatIndex + i] = childMain;
					m_finalCross[flatIndex + i] = childCross;
					usedMain += (controlMain || forceMain) ? allocatedScaled : childMain * sMain;
				}

				m_lineUsedMain[line] = usedMain;
				if(usedMain > maxUsedMain) maxUsedMain = usedMain;
				flatIndex += lineChildCount;
			}

			// 配置フェーズ: 全ラインで共通の開始位置（最大ライン幅基準）を使い、端数ラインも他ラインの列位置に揃える
			bool alignLineToEnd = startAxis == Axis.Horizontal ? cornerX == 1 : cornerY == 1; // 開始コーナーが終端側なら端数ラインを終端に寄せる
			float commonMainStart = GetStartOffset(mainAxis, maxUsedMain);
			flatIndex = 0;
			for (int line = 0; line < lineCount; line++) {
				var lineChildren = m_lines[line];
				int lineChildCount = lineChildren.Count;
				if(lineChildCount == 0) {
					crossCursor += m_lineCrossSizes[line] + crossSpacing;
					continue;
				}

				// ライン内の最終配置
				float mainCursor = commonMainStart + (alignLineToEnd ? maxUsedMain - m_lineUsedMain[line] : 0f);
				for (int i = 0; i < lineChildCount; i++) {
					var child = lineChildren[i];
					float sMain = m_scaleMain[flatIndex + i];
					float sCross = m_scaleCross[flatIndex + i];

					float childMainScaled = m_finalMain[flatIndex + i] * sMain;
					float childCrossScaled = m_finalCross[flatIndex + i] * sCross;
					float alignedMain = mainCursor + (m_allocatedMainScaled[flatIndex + i] - childMainScaled) * alignMain;
					float alignedCross = crossCursor + (m_lineCrossSizes[line] - childCrossScaled) * alignCross;

					if(startAxis == Axis.Horizontal) {
						SetChildAlongBothAxes(child, alignedMain, alignedCross, m_finalMain[flatIndex + i], m_finalCross[flatIndex + i], sMain, sCross);
					} else {
						SetChildAlongBothAxes(child, alignedCross, alignedMain, m_finalCross[flatIndex + i], m_finalMain[flatIndex + i], sCross, sMain);
					}

					mainCursor += m_allocatedMainScaled[flatIndex + i] + mainSpacing;
				}

				flatIndex += lineChildCount;
				crossCursor += m_lineCrossSizes[line] + crossSpacing;
			}

			ApplyNavigationGrid(lineCount);
		}

		/// <summary>グリッド上の Selectable に明示的な Navigation を設定する。</summary>
		private void ApplyNavigationGrid(int lineCount) {
			if(!setNavigation || lineCount <= 0) return;

			int mainCellCount = 0;
			for (int i = 0; i < lineCount; i++) {
				mainCellCount = Mathf.Max(mainCellCount, m_lines[i].Count);
			}
			if(mainCellCount <= 0) return;

			int columns = startAxis == Axis.Horizontal ? mainCellCount : lineCount;
			int rows = startAxis == Axis.Horizontal ? lineCount : mainCellCount;
			for (int y = 0; y < rows; y++) {
				for (int x = 0; x < columns; x++) {
					var rect = GetGridCell(x, y, columns, rows);
					if(rect == null) continue;

					var selectable = rect.GetComponent<Selectable>();
					if(selectable == null) continue;

					Navigation navigation = selectable.navigation;
					navigation.mode = Navigation.Mode.Explicit;
					navigation.selectOnLeft = FindSelectableInGrid(x, y, -1, 0, columns, rows, rect);
					navigation.selectOnRight = FindSelectableInGrid(x, y, 1, 0, columns, rows, rect);
					navigation.selectOnUp = FindSelectableInGrid(x, y, 0, -1, columns, rows, rect);
					navigation.selectOnDown = FindSelectableInGrid(x, y, 0, 1, columns, rows, rect);
					selectable.navigation = navigation;
				}
			}
		}

		/// <summary>物理的な行列座標から、現在の配置に対応する子要素を取得する。</summary>
		private RectTransform GetGridCell(int x, int y, int columns, int rows) {
			if(x < 0 || x >= columns || y < 0 || y >= rows) return null;

			int lineIndex = startAxis == Axis.Horizontal ? y : x;
			int mainIndex = startAxis == Axis.Horizontal ? x : y;
			if(lineIndex < 0 || lineIndex >= m_lines.Count) return null;

			var line = m_lines[lineIndex];
			int mainCellCount = startAxis == Axis.Horizontal ? columns : rows;
			bool alignLineToEnd = startAxis == Axis.Horizontal
				? startCorner == Corner.UpperRight || startCorner == Corner.LowerRight
				: startCorner == Corner.LowerLeft || startCorner == Corner.LowerRight;
			if(alignLineToEnd) mainIndex -= mainCellCount - line.Count;

			return mainIndex >= 0 && mainIndex < line.Count ? line[mainIndex] : null;
		}

		/// <summary>指定方向にある最寄りの Selectable を探索する。</summary>
		private Selectable FindSelectableInGrid(int startX, int startY, int dx, int dy, int columns, int rows, RectTransform origin) {
			if(dx == 0 && dy == 0) return null;

			int maxSteps = dx != 0 ? columns : rows;
			bool allowCrossLine = !navigationLoop || (dx != 0 ? startAxis == Axis.Vertical : startAxis == Axis.Horizontal);
			bool searchSameLineFirst = !navigationLoop || !allowCrossLine;

			if(searchSameLineFirst) {
				for (int step = 1; step <= maxSteps; step++) {
					int x = startX + dx * step;
					int y = startY + dy * step;
					if(navigationLoop) {
						x = dx != 0 ? PositiveModulo(x, columns) : startX;
						y = dy != 0 ? PositiveModulo(y, rows) : startY;
					} else if(x < 0 || x >= columns || y < 0 || y >= rows) {
						break;
					}

					var candidate = GetSelectableAt(x, y, columns, rows, origin);
					if(candidate != null) return candidate;
				}
			}

			if(!allowCrossLine) return null;

			for (int step = 1; step <= maxSteps; step++) {
				int x = startX + dx * step;
				int y = startY + dy * step;
				if(navigationLoop) {
					x = dx != 0 ? PositiveModulo(x, columns) : startX;
					y = dy != 0 ? PositiveModulo(y, rows) : startY;
				} else if(x < 0 || x >= columns || y < 0 || y >= rows) {
					break;
				}

				Selectable best = null;
				int bestDistance = int.MaxValue;
				if(dx != 0) {
					for (int row = 0; row < rows; row++) {
						var candidate = GetSelectableAt(x, row, columns, rows, origin);
						if(candidate == null) continue;
						int distance = Mathf.Abs(row - startY);
						if(distance >= bestDistance) continue;
						bestDistance = distance;
						best = candidate;
						if(distance == 0) return best;
					}
				} else {
					for (int column = 0; column < columns; column++) {
						var candidate = GetSelectableAt(column, y, columns, rows, origin);
						if(candidate == null) continue;
						int distance = Mathf.Abs(column - startX);
						if(distance >= bestDistance) continue;
						bestDistance = distance;
						best = candidate;
						if(distance == 0) return best;
					}
				}

				if(best != null) return best;
			}

			return null;
		}

		private Selectable GetSelectableAt(int x, int y, int columns, int rows, RectTransform origin) {
			var rect = GetGridCell(x, y, columns, rows);
			return rect != null && rect != origin ? rect.GetComponent<Selectable>() : null;
		}

		private static int PositiveModulo(int value, int divisor)
			=> (value % divisor + divisor) % divisor;
		#endregion
	}
}
