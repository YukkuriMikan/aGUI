using System.Collections.Generic;
using UnityEngine;

public static class RectUtils {
	/// <summary> 二つのRectを包含する外接Rectを返します。 </summary>
	public static Rect Union(in Rect a, in Rect b) {
		float xMin = Mathf.Min(a.xMin, b.xMin);
		float yMin = Mathf.Min(a.yMin, b.yMin);
		float xMax = Mathf.Max(a.xMax, b.xMax);
		float yMax = Mathf.Max(a.yMax, b.yMax);
		return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
	}

	/// <summary>
	/// 列挙された Rect 群をすべて包含する外接 Rect を返します。
	/// 空の場合は false を返します。
	/// </summary>
	public static bool TryUnion(IEnumerable<Rect> rects, out Rect result) {
		result = default;
		bool hasAny = false;

		float xMin = 0, yMin = 0, xMax = 0, yMax = 0;

		foreach (var r in rects) {
			if(!hasAny) {
				xMin = r.xMin;
				yMin = r.yMin;
				xMax = r.xMax;
				yMax = r.yMax;
				hasAny = true;
				continue;
			}

			xMin = Mathf.Min(xMin, r.xMin);
			yMin = Mathf.Min(yMin, r.yMin);
			xMax = Mathf.Max(xMax, r.xMax);
			yMax = Mathf.Max(yMax, r.yMax);
		}

		if(!hasAny) return false;

		result = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
		return true;
	}
}
