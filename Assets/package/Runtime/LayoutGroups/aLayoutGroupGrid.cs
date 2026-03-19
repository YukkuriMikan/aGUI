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
		private readonly List<Vector2Int> m_positions = new();       // 各子要素の配置セル座標を再利用保持
		private readonly List<int> m_childIndexInLine = new();       // 行/列内での子インデックスを再利用保持
		private readonly List<int> m_rowChildCounts = new();         // 各行の子要素数キャッシュ
		private readonly List<int> m_columnChildCounts = new();      // 各列の子要素数キャッシュ
		private readonly List<Vector2Int> m_filledGridCells = new(); // 前回ナビゲーショングリッドで使用したセル一覧
		private readonly List<RectTransform> m_navigationGrid = new(); // Navigation探索用の再利用グリッド（1次元バッファ）
		private int m_navigationGridCols;
		private int m_navigationGridRows;
		#endregion

		#region Methods
		/// <summary>制約設定・開始コーナー・主軸・セルサイズやスケールを考慮して子要素をグリッド配置する</summary>
		protected override void CalculateLayout() {
			if(RectTransform == null) return;

			bool forceExpandWidth = childForceExpandWidth;
			bool forceExpandHeight = childForceExpandHeight;
			bool controlChildWidth = childControlWidth;
			bool controlChildHeight = childControlHeight;
			bool fillSlotWidth = forceExpandWidth;
			bool fillSlotHeight = forceExpandHeight;

			int count = rectChildren.Count;
			if(count == 0) return;

			float width = RectTransform.rect.width;
			float height = RectTransform.rect.height;
			float availableWidth = Mathf.Max(0f, width - padding.horizontal);
			float availableHeight = Mathf.Max(0f, height - padding.vertical);

			// 制約設定に応じて列・行数の上限を算出
			int cellCountX = 1;
			int cellCountY = 1;
			if(constraint == Constraint.FixedColumnCount) {
				cellCountX = Mathf.Max(1, constraintCount);
				cellCountY = int.MaxValue; // 実際の行数は後でスキャン
			} else if(constraint == Constraint.FixedRowCount) {
				cellCountY = Mathf.Max(1, constraintCount);
				cellCountX = int.MaxValue; // 実際の列数は後でスキャン
			} else {
				if(cellSize.x + spacingXY.x <= 0f) {
					cellCountX = int.MaxValue;
				} else {
					cellCountX = Mathf.Max(1, Mathf.FloorToInt((width - padding.horizontal + spacingXY.x + 0.001f) / (cellSize.x + spacingXY.x)));
				}
				if(cellSize.y + spacingXY.y <= 0f) {
					cellCountY = int.MaxValue;
				} else {
					cellCountY = Mathf.Max(1, Mathf.FloorToInt((height - padding.vertical + spacingXY.y + 0.001f) / (cellSize.y + spacingXY.y)));
				}
			}

			int cornerX = (int)startCorner % 2;
			int cornerY = (int)startCorner / 2;

			// 1st pass: サイズとスケールに応じて必要スロットを算出し、配置候補を決める
			m_positions.Clear();
			m_childIndexInLine.Clear();
			m_rowChildCounts.Clear();
			m_columnChildCounts.Clear();
			if(m_positions.Capacity < count) m_positions.Capacity = count;
			if(m_childIndexInLine.Capacity < count) m_childIndexInLine.Capacity = count;

			int estimatedCellCountX;
			int estimatedCellCountY;
			if(constraint == Constraint.FixedColumnCount) {
				estimatedCellCountX = Mathf.Max(1, constraintCount);
				estimatedCellCountY = Mathf.Max(1, Mathf.CeilToInt((float)count / estimatedCellCountX));
			} else if(constraint == Constraint.FixedRowCount) {
				estimatedCellCountY = Mathf.Max(1, constraintCount);
				estimatedCellCountX = Mathf.Max(1, Mathf.CeilToInt((float)count / estimatedCellCountY));
			} else {
				estimatedCellCountX = cellCountX == int.MaxValue ? count : Mathf.Min(Mathf.Max(1, cellCountX), count);
				estimatedCellCountY = cellCountY == int.MaxValue ? count : Mathf.Min(Mathf.Max(1, cellCountY), count);
			}
			float estimatedSpacingCountWidth = Mathf.Max(0, estimatedCellCountX - 1);
			float estimatedSpacingCountHeight = Mathf.Max(0, estimatedCellCountY - 1);
			float estimatedWidthWithoutSpacing = Mathf.Max(0f, availableWidth - spacingXY.x * estimatedSpacingCountWidth);
			float estimatedHeightWithoutSpacing = Mathf.Max(0f, availableHeight - spacingXY.y * estimatedSpacingCountHeight);
			float estimatedSlotWidth = estimatedWidthWithoutSpacing / Mathf.Max(1, estimatedCellCountX);
			float estimatedSlotHeight = estimatedHeightWithoutSpacing / Mathf.Max(1, estimatedCellCountY);
			float mainSpacing = startAxis == Axis.Horizontal ? spacingXY.x : spacingXY.y;
			int currentX = 0;
			int currentY = 0;
			int maxX = 0;
			int maxY = 0;
			int currentChildInLine = 0;
			int maxChildrenInRow = 0;
			int maxChildrenInColumn = 0;
			for (int i = 0; i < count; i++) {
				int childIndex = reverseArrangement ? (count - 1 - i) : i;
				var child = rectChildren[childIndex];
				GetChildSizes(child, 0, childControlWidth, childForceExpandWidth, out var sizeX);
				GetChildSizes(child, 1, childControlHeight, childForceExpandHeight, out var sizeY);
				float scaleX = childScaleWidth ? Mathf.Abs(child.localScale.x) : 1f;
				float scaleY = childScaleHeight ? Mathf.Abs(child.localScale.y) : 1f;

				bool controlMain = startAxis == Axis.Horizontal ? childControlWidth : childControlHeight;
				bool fillSlotMain = startAxis == Axis.Horizontal ? fillSlotWidth : fillSlotHeight;
				float mainSize = startAxis == Axis.Horizontal ? sizeX.preferred : sizeY.preferred;
				float mainScale = startAxis == Axis.Horizontal ? scaleX : scaleY;
				float scaledMain = mainSize * mainScale;
				int slotNeeded = 1;
				if(fillSlotMain) {
					float estimatedMainSlot = startAxis == Axis.Horizontal ? estimatedSlotWidth : estimatedSlotHeight;
					float step = Mathf.Max(0.0001f, estimatedMainSlot + mainSpacing);
					slotNeeded = Mathf.Max(1, Mathf.CeilToInt((scaledMain + mainSpacing) / step));
				} else if(!controlMain) {
					float baseSize = startAxis == Axis.Horizontal ? cellSize.x : cellSize.y;
					float denom = Mathf.Max(0.0001f, baseSize);
					slotNeeded = Mathf.Max(1, Mathf.CeilToInt(scaledMain / denom));
				}

				// 主軸の空きが足りなければ次の列/行へ送る
				if(startAxis == Axis.Horizontal) {
					if(currentX + slotNeeded > cellCountX) {
						currentX = 0;
						currentY++;
						currentChildInLine = 0;
					}
					m_positions.Add(new Vector2Int(currentX, currentY));
					m_childIndexInLine.Add(currentChildInLine);
					while(m_rowChildCounts.Count <= currentY) m_rowChildCounts.Add(0);
					m_rowChildCounts[currentY]++;
					currentChildInLine++;
					maxChildrenInRow = Mathf.Max(maxChildrenInRow, currentChildInLine);
					maxX = Mathf.Max(maxX, currentX + slotNeeded - 1);
					maxY = Mathf.Max(maxY, currentY);
					currentX += slotNeeded;
				} else {
					if(currentY + slotNeeded > cellCountY) {
						currentY = 0;
						currentX++;
						currentChildInLine = 0;
					}
					m_positions.Add(new Vector2Int(currentX, currentY));
					m_childIndexInLine.Add(currentChildInLine);
					while(m_columnChildCounts.Count <= currentX) m_columnChildCounts.Add(0);
					m_columnChildCounts[currentX]++;
					currentChildInLine++;
					maxChildrenInColumn = Mathf.Max(maxChildrenInColumn, currentChildInLine);
					maxY = Mathf.Max(maxY, currentY + slotNeeded - 1);
					maxX = Mathf.Max(maxX, currentX);
					currentY += slotNeeded;
				}
			}

			// 実際に必要となるセル数を算出
			int actualCellCountX = Mathf.Max(1, maxX + 1);
			int actualCellCountY = Mathf.Max(1, maxY + 1);
			float spacingCountWidth = Mathf.Max(0, actualCellCountX - 1);
			float spacingCountHeight = Mathf.Max(0, actualCellCountY - 1);
			float widthWithoutSpacing = Mathf.Max(0f, availableWidth - spacingXY.x * spacingCountWidth);
			float heightWithoutSpacing = Mathf.Max(0f, availableHeight - spacingXY.y * spacingCountHeight);
			float cellWidth = cellSize.x;
			float cellHeight = cellSize.y;
			float slotWidth = fillSlotWidth ? widthWithoutSpacing / Mathf.Max(1, actualCellCountX) : cellWidth;
			float slotHeight = fillSlotHeight ? heightWithoutSpacing / Mathf.Max(1, actualCellCountY) : cellHeight;

			// Spacing を考慮した必要領域を計算
			int spacingCountX = startAxis == Axis.Horizontal ? Mathf.Max(0, maxChildrenInRow - 1) : Mathf.Max(0, actualCellCountX - 1);
			int spacingCountY = startAxis == Axis.Vertical ? Mathf.Max(0, maxChildrenInColumn - 1) : Mathf.Max(0, actualCellCountY - 1);
			Vector2 requiredSpace = new Vector2(
				fillSlotWidth ? availableWidth : actualCellCountX * cellWidth + spacingCountX * spacingXY.x,
				fillSlotHeight ? availableHeight : actualCellCountY * cellHeight + spacingCountY * spacingXY.y
				);
			Vector2 startOffset = new Vector2(
				GetStartOffset(0, requiredSpace.x),
				GetStartOffset(1, requiredSpace.y)
				);

			EnsureNavigationGridCapacity(actualCellCountX, actualCellCountY);
			for (int i = 0; i < m_filledGridCells.Count; i++) {
				var filled = m_filledGridCells[i];
				int clearIndex = GetGridIndex(filled.x, filled.y, m_navigationGridCols);
				m_navigationGrid[clearIndex] = null;
			}
			m_filledGridCells.Clear();
			float alignX = GetAlignmentOnAxis(0);
			float alignY = GetAlignmentOnAxis(1);

			// 2nd pass: 実際の配置とナビゲーション用グリッドを構築
			for (int i = 0; i < count; i++) {
				int childIndex = reverseArrangement ? (count - 1 - i) : i;
				var child = rectChildren[childIndex];
				var pos = m_positions[i];
				int childLineIndex = m_childIndexInLine[i];
				GetChildSizes(child, 0, childControlWidth, childForceExpandWidth, out var sizeX);
				GetChildSizes(child, 1, childControlHeight, childForceExpandHeight, out var sizeY);

				float scaleX = childScaleWidth ? child.localScale.x : 1f;
				float scaleY = childScaleHeight ? child.localScale.y : 1f;

				float childWidth = controlChildWidth ? (fillSlotWidth ? slotWidth : cellWidth) : sizeX.preferred;
				float childHeight = controlChildHeight ? (fillSlotHeight ? slotHeight : cellHeight) : sizeY.preferred;

				// 主軸方向で必要なスロット数を算出（制御しない場合はサイズに応じて複数スロット消費）
				float currentSlotWidth = slotWidth;
				float currentSlotHeight = slotHeight;

				int px = pos.x;
				int py = pos.y;
				if(cornerX == 1) px = actualCellCountX - 1 - px;
				if(cornerY == 1) py = actualCellCountY - 1 - py;

				int spacingIndexX;
				int spacingIndexY;
				if(startAxis == Axis.Horizontal) {
					// Spacing は子の並び順ベースでカウントするが、右開始の場合は行内のインデックスを反転して距離が正方向に保たれるようにする。
					int rowCount = m_rowChildCounts.Count > pos.y ? m_rowChildCounts[pos.y] : maxChildrenInRow;
					if(cornerX == 1) {
						spacingIndexX = Mathf.Max(0, rowCount - 1 - childLineIndex);
					} else {
						spacingIndexX = childLineIndex;
					}
					spacingIndexY = py;
				} else {
					int columnCount = m_columnChildCounts.Count > pos.x ? m_columnChildCounts[pos.x] : maxChildrenInColumn;
					spacingIndexX = px;
					spacingIndexY = cornerY == 0 ? childLineIndex : (columnCount - 1 - childLineIndex);
				}

				float stepX = fillSlotWidth ? slotWidth : cellWidth;
				float stepY = fillSlotHeight ? slotHeight : cellHeight;
				float baseX = startOffset.x + px * stepX + spacingXY.x * spacingIndexX;
				float baseY = startOffset.y + py * stepY + spacingXY.y * spacingIndexY;

				float alignedX = baseX + (currentSlotWidth - childWidth * scaleX) * alignX;
				float alignedY = baseY + (currentSlotHeight - childHeight * scaleY) * alignY;

				SetChildAlongBothAxes(child, alignedX, alignedY, childWidth, childHeight, scaleX, scaleY);

				if(py >= 0 && py < actualCellCountY && px >= 0 && px < actualCellCountX) {
					int gridIndex = GetGridIndex(px, py, m_navigationGridCols);
					m_navigationGrid[gridIndex] = child;
					m_filledGridCells.Add(new Vector2Int(px, py));
				}
			}

			ApplyNavigationGrid(m_navigationGrid, actualCellCountX, actualCellCountY);
		}

		/// <summary>必要サイズを満たすようにNavigation探索用グリッドを再確保する</summary>
		private void EnsureNavigationGridCapacity(int cols, int rows) {
			if(m_navigationGridCols < cols) m_navigationGridCols = cols;
			if(m_navigationGridRows < rows) m_navigationGridRows = rows;

			int required = m_navigationGridCols * m_navigationGridRows;
			if(m_navigationGrid.Capacity < required) m_navigationGrid.Capacity = required;
			while(m_navigationGrid.Count < required) {
				m_navigationGrid.Add(null);
			}
		}

		/// <summary>1次元グリッドバッファ上のインデックスを計算する</summary>
		private static int GetGridIndex(int x, int y, int cols)
			=> y * cols + x;

		/// <summary>グリッド上のSelectablesにナビゲーションを割り当てる</summary>
		private void ApplyNavigationGrid(List<RectTransform> grid, int cols, int rows) {
			if(!setNavigation) return;
			for (int y = 0; y < rows; y++) {
				for (int x = 0; x < cols; x++) {
					var rect = grid[GetGridIndex(x, y, cols)];
					if(rect == null) continue;
					var selectable = rect.GetComponent<Selectable>();
					if(selectable == null) continue;

					Navigation nav = selectable.navigation;
					nav.mode = Navigation.Mode.Explicit;

					nav.selectOnLeft = FindSelectableInGrid(grid, cols, rows, x, y, -1, 0, navigationLoop, startAxis);
					nav.selectOnRight = FindSelectableInGrid(grid, cols, rows, x, y, 1, 0, navigationLoop, startAxis);
					nav.selectOnUp = FindSelectableInGrid(grid, cols, rows, x, y, 0, -1, navigationLoop, startAxis);
					nav.selectOnDown = FindSelectableInGrid(grid, cols, rows, x, y, 0, 1, navigationLoop, startAxis);

					selectable.navigation = nav;
				}
			}
		}

		/// <summary>グリッド内で指定方向の次のSelectableを探索</summary>
		private Selectable FindSelectableInGrid(List<RectTransform> grid, int cols, int rows, int startX, int startY, int dx, int dy, bool loop, Axis startAxis) {
			if(dx == 0 && dy == 0) return null;

			int maxSteps = dx != 0 ? cols : rows;
			bool allowCrossLine = !loop || (dx != 0 ? startAxis == Axis.Vertical : startAxis == Axis.Horizontal);
			bool useSameLineFirst = !loop || !allowCrossLine;
			// まずは同一行/列で方向が合うものを優先して探す
			if(useSameLineFirst) {
				for (int step = 1; step <= maxSteps; step++) {
					int x = startX + dx * step;
					int y = startY + dy * step;
					if(loop) {
						if(dx != 0) {
							x = (x % cols + cols) % cols;
							y = startY;
						} else {
							y = (y % rows + rows) % rows;
							x = startX;
						}
					} else {
						if(x < 0 || x >= cols || y < 0 || y >= rows) break;
					}

					var rect = grid[GetGridIndex(x, y, cols)];
					if(rect == null) continue;
					var selectable = rect.GetComponent<Selectable>();
					if(selectable != null) return selectable;
				}
			}

			if(!allowCrossLine) return null;

			for (int step = 1; step <= maxSteps; step++) {
				int x = startX + dx * step;
				int y = startY + dy * step;
				if(loop) {
					if(dx != 0) {
						x = (x % cols + cols) % cols;
						y = startY;
					} else {
						y = (y % rows + rows) % rows;
						x = startX;
					}
				} else {
					if(x < 0 || x >= cols || y < 0 || y >= rows) break;
				}

				Selectable best = null;
				int bestDistance = int.MaxValue;
				if(dx != 0) {
					for (int row = 0; row < rows; row++) {
						var rect = grid[GetGridIndex(x, row, cols)];
						if(rect == null) continue;
						var s = rect.GetComponent<Selectable>();
						if(s == null) continue;
						int dist = Mathf.Abs(row - startY);
						if(dist < bestDistance) {
							bestDistance = dist;
							best = s;
							if(bestDistance == 0) return best;
						}
					}
				} else {
					for (int col = 0; col < cols; col++) {
						var rect = grid[GetGridIndex(col, y, cols)];
						if(rect == null) continue;
						var s = rect.GetComponent<Selectable>();
						if(s == null) continue;
						int dist = Mathf.Abs(col - startX);
						if(dist < bestDistance) {
							bestDistance = dist;
							best = s;
							if(bestDistance == 0) return best;
						}
					}
				}

				if(best != null) return best;
			}
			return null;
		}
		#endregion
	}
}
