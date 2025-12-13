using UnityEngine;
using UnityEngine.UI;


namespace ANest.UI {
	/// <summary> グリッド状に子要素を配置するカスタムレイアウトグループ </summary>
	public class aLayoutGroupGrid : aLayoutGroupBase {
		/// <summary> グリッド配置の開始コーナー </summary>
		public enum Corner { UpperLeft = 0, UpperRight = 1, LowerLeft = 2, LowerRight = 3 }
		/// <summary> 主軸の方向 </summary>
		public enum Axis { Horizontal = 0, Vertical = 1 }
		/// <summary> グリッドの制約種別 </summary>
		public enum Constraint { Flexible, FixedColumnCount, FixedRowCount }

		#region SerializeField
		[SerializeField] private Corner startCorner = Corner.UpperLeft;          // 配置開始コーナー
		[SerializeField] private Axis startAxis = Axis.Horizontal;                // 主軸方向（横 or 縦）
		[SerializeField] private Vector2 cellSize = new Vector2(100f, 100f);      // セルのサイズ
		[SerializeField] private Vector2 spacingXY = Vector2.zero;                // セル間スペース（X, Y）
		[SerializeField] private Constraint constraint = Constraint.Flexible;      // グリッド制約設定
		[SerializeField] private int constraintCount = 2;                         // 制約値（列・行固定時の数）
		#endregion

		#region Methods
		/// <summary>
		/// グリッド状に子要素を配置するレイアウト計算。
		/// 制約設定（列/行固定やフレキシブル）・開始コーナー・主軸方向・セルサイズ/スペーシング・
		/// 反転配置・子のスケールやサイズ制御フラグを踏まえて、必要セル数を算出し位置を決定する。
		/// </summary>
		protected override void CalculateLayout() {
			if(RectTransform == null) return;

			int count = rectChildren.Count;
			if(count == 0) return;

			float width = RectTransform.rect.width;
			float height = RectTransform.rect.height;

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
			var positions = new System.Collections.Generic.List<Vector2Int>(count);
			var childIndexInLine = new System.Collections.Generic.List<int>(count);
			var rowChildCounts = new System.Collections.Generic.List<int>();
			var columnChildCounts = new System.Collections.Generic.List<int>();
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
				float mainSize = startAxis == Axis.Horizontal ? sizeX.preferred : sizeY.preferred;
				float mainScale = startAxis == Axis.Horizontal ? scaleX : scaleY;
				int slotNeeded = 1;
				if(!controlMain) {
					float scaledMain = mainSize * mainScale;
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
					positions.Add(new Vector2Int(currentX, currentY));
					childIndexInLine.Add(currentChildInLine);
					while(rowChildCounts.Count <= currentY) rowChildCounts.Add(0);
					rowChildCounts[currentY]++;
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
					positions.Add(new Vector2Int(currentX, currentY));
					childIndexInLine.Add(currentChildInLine);
					while(columnChildCounts.Count <= currentX) columnChildCounts.Add(0);
					columnChildCounts[currentX]++;
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

			// Spacing を考慮した必要領域を計算
			int spacingCountX = startAxis == Axis.Horizontal ? Mathf.Max(0, maxChildrenInRow - 1) : Mathf.Max(0, actualCellCountX - 1);
			int spacingCountY = startAxis == Axis.Vertical ? Mathf.Max(0, maxChildrenInColumn - 1) : Mathf.Max(0, actualCellCountY - 1);
			Vector2 requiredSpace = new Vector2(
				actualCellCountX * cellSize.x + spacingCountX * spacingXY.x,
				actualCellCountY * cellSize.y + spacingCountY * spacingXY.y
				);
			Vector2 startOffset = new Vector2(
				GetStartOffset(0, requiredSpace.x),
				GetStartOffset(1, requiredSpace.y)
				);

			var grid = new RectTransform[actualCellCountY, actualCellCountX];
			float alignX = GetAlignmentOnAxis(0);
			float alignY = GetAlignmentOnAxis(1);

			// 2nd pass: 実際の配置とナビゲーション用グリッドを構築
			for (int i = 0; i < count; i++) {
				int childIndex = reverseArrangement ? (count - 1 - i) : i;
				var child = rectChildren[childIndex];
				var pos = positions[i];
				int childLineIndex = childIndexInLine[i];

				GetChildSizes(child, 0, childControlWidth, childForceExpandWidth, out var sizeX);
				GetChildSizes(child, 1, childControlHeight, childForceExpandHeight, out var sizeY);
				float scaleX = childScaleWidth ? child.localScale.x : 1f;
				float scaleY = childScaleHeight ? child.localScale.y : 1f;

				bool controlWidth = childControlWidth;
				bool controlHeight = childControlHeight;
				float childWidth = controlWidth ? cellSize.x : sizeX.preferred;
				float childHeight = controlHeight ? cellSize.y : sizeY.preferred;

				// 主軸方向で必要なスロット数を算出（制御しない場合はサイズに応じて複数スロット消費）
				int slotNeededMain = 1;
				float slotWidth = cellSize.x;
				float slotHeight = cellSize.y;
				if(startAxis == Axis.Horizontal) {
					if(!controlWidth) {
						float scaledW = childWidth * (childScaleWidth ? Mathf.Abs(scaleX) : 1f);
						slotNeededMain = Mathf.Max(1, Mathf.CeilToInt(scaledW / Mathf.Max(0.0001f, cellSize.x)));
					}
					slotWidth = cellSize.x * slotNeededMain;
				} else {
					if(!controlHeight) {
						float scaledH = childHeight * (childScaleHeight ? Mathf.Abs(scaleY) : 1f);
						slotNeededMain = Mathf.Max(1, Mathf.CeilToInt(scaledH / Mathf.Max(0.0001f, cellSize.y)));
					}
					slotHeight = cellSize.y * slotNeededMain;
				}

				int px = pos.x;
				int py = pos.y;
				if(cornerX == 1) px = actualCellCountX - 1 - px;
				if(cornerY == 1) py = actualCellCountY - 1 - py;

				int spacingIndexX;
				int spacingIndexY;
				if(startAxis == Axis.Horizontal) {
					// Spacing は子の並び順ベースでカウントするが、右開始の場合は行内のインデックスを反転して距離が正方向に保たれるようにする。
					int rowCount = rowChildCounts.Count > pos.y ? rowChildCounts[pos.y] : maxChildrenInRow;
					if(cornerX == 1) {
						spacingIndexX = Mathf.Max(0, rowCount - 1 - childLineIndex);
					} else {
						spacingIndexX = childLineIndex;
					}
					spacingIndexY = py;
				} else {
					int columnCount = columnChildCounts.Count > pos.x ? columnChildCounts[pos.x] : maxChildrenInColumn;
					spacingIndexX = px;
					spacingIndexY = cornerY == 0 ? childLineIndex : (columnCount - 1 - childLineIndex);
				}

				float baseX = startOffset.x + px * cellSize.x + spacingXY.x * spacingIndexX;
				float baseY = startOffset.y + py * cellSize.y + spacingXY.y * spacingIndexY;

				float alignedX = baseX + (slotWidth - childWidth * scaleX) * alignX;
				float alignedY = baseY + (slotHeight - childHeight * scaleY) * alignY;

				SetChildAlongBothAxes(child, alignedX, alignedY, childWidth, childHeight, scaleX, scaleY);

				if(py >= 0 && py < actualCellCountY && px >= 0 && px < actualCellCountX) {
					grid[py, px] = child;
				}
			}

			ApplyNavigationGrid(grid, actualCellCountX, actualCellCountY);
		}

		/// <summary> グリッド上のSelectablesにナビゲーションを割り当てる </summary>
		private void ApplyNavigationGrid(RectTransform[,] grid, int cols, int rows) {
			if(!setNavigation) return;
			for (int y = 0; y < rows; y++) {
				for (int x = 0; x < cols; x++) {
					var rect = grid[y, x];
					if(rect == null) continue;
					var selectable = rect.GetComponent<Selectable>();
					if(selectable == null) continue;

					Navigation nav = selectable.navigation;
					nav.mode = Navigation.Mode.Explicit;

					nav.selectOnLeft = FindSelectableInGrid(grid, cols, rows, x, y, -1, 0, navigationLoop);
					nav.selectOnRight = FindSelectableInGrid(grid, cols, rows, x, y, 1, 0, navigationLoop);
					nav.selectOnUp = FindSelectableInGrid(grid, cols, rows, x, y, 0, -1, navigationLoop);
					nav.selectOnDown = FindSelectableInGrid(grid, cols, rows, x, y, 0, 1, navigationLoop);

					selectable.navigation = nav;
				}
			}
		}

		/// <summary> グリッド内で指定方向の次のSelectableを探索 </summary>
		private Selectable FindSelectableInGrid(RectTransform[,] grid, int cols, int rows, int startX, int startY, int dx, int dy, bool loop) {
			int x = startX + dx;
			int y = startY + dy;
			if(loop) {
				if(dx != 0) {
					y = startY;
					x = (x % cols + cols) % cols;
					for (int i = 0; i < cols; i++) {
						var rect = grid[y, x];
						if(rect != null) {
							var s = rect.GetComponent<Selectable>();
							if(s != null) return s;
						}
						x = (x + dx + cols) % cols;
					}
					return null;
				}
				if(dy != 0) {
					x = startX;
					y = (y % rows + rows) % rows;
					for (int i = 0; i < rows; i++) {
						var rect = grid[y, x];
						if(rect != null) {
							var s = rect.GetComponent<Selectable>();
							if(s != null) return s;
						}
						y = (y + dy + rows) % rows;
					}
					return null;
				}
			}

			while (x >= 0 && x < cols && y >= 0 && y < rows) {
				var rect = grid[y, x];
				if(rect != null) {
					var s = rect.GetComponent<Selectable>();
					if(s != null) return s;
				}
				x += dx;
				y += dy;
			}
			return null;
		}
		#endregion
	}
}
