using System;
using UnityEngine;

namespace ANest.UI {
	/// <summary> SerializeReference フィールドを型選択ドロップダウンで描画するための属性 </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class SerializeReferenceDropdownAttribute : PropertyAttribute {
		/// <summary> 選択対象を絞り込む基底型。null の場合はフィールド型を使用 </summary>
		public Type BaseType { get; }

		/// <param name="baseType">選択肢を制限する基底型（未指定ならフィールド型）</param>
		public SerializeReferenceDropdownAttribute(Type baseType = null) {
			BaseType = baseType;
		}
	}
}