using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ANest.UI.Editor {
	/// <summary> SerializeReference フィールドをドロップダウンで型選択できるようにするプロパティドロワー </summary>
	[CustomPropertyDrawer(typeof(SerializeReferenceDropdownAttribute))]
	public class SerializeReferenceDropdownDrawer : PropertyDrawer {
		#region Fields
		private const string NullEntryName = "(None)"; // 「なし」選択時に表示する名称
		#endregion

		#region Methods
		/// <summary> ドロップダウン＋折りたたみ付きの描画処理 </summary>
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			EditorGUI.BeginProperty(position, label, property);

			var dropdownAttribute = (SerializeReferenceDropdownAttribute)attribute;
			Type baseType = GetBaseType(dropdownAttribute, fieldInfo);
			if(baseType == null) {
				EditorGUI.PropertyField(position, property, label, true);
				EditorGUI.EndProperty();
				return;
			}

			var choices = GetTypeChoices(baseType);

			using (new EditorGUI.PropertyScope(position, label, property)) {
				Rect lineRect = position;
				lineRect.height = EditorGUIUtility.singleLineHeight;

				Rect dropdownRect = EditorGUI.PrefixLabel(lineRect, label);

				Rect foldoutRect = dropdownRect;
				foldoutRect.width = EditorGUIUtility.singleLineHeight;
				dropdownRect.xMin += foldoutRect.width + 2f;

				// 型未選択時は中身を展開できないよう無効化する
				using (new EditorGUI.DisabledScope(property.managedReferenceValue == null)) {
					property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none);
				}

				int currentIndex = GetCurrentIndex(property, choices);
				string[] displayedOptions = choices.Select(t => t?.Name ?? NullEntryName).ToArray();
				int selectedIndex = EditorGUI.Popup(dropdownRect, currentIndex, displayedOptions);

				if(selectedIndex != currentIndex) {
					Type selectedType = choices[selectedIndex];
					SetManagedReferenceValue(property, selectedType);
					property.isExpanded = property.managedReferenceValue != null && property.isExpanded; // 選択切替後も折りたたみ状態を維持
				}

				// 型が選択され展開中の場合のみ中身を描画
				if(property.isExpanded && property.managedReferenceValue != null) {
					Rect propertyRect = position;
					propertyRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
					propertyRect.height = EditorGUI.GetPropertyHeight(property, GUIContent.none, true);
					EditorGUI.PropertyField(propertyRect, property, GUIContent.none, true);
				}
			}

			EditorGUI.EndProperty();
		}

		/// <summary> プロパティの高さをドロップダウン＋展開時の本体高さで算出 </summary>
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			float dropdownHeight = EditorGUIUtility.singleLineHeight;
			if(property.managedReferenceValue == null || !property.isExpanded) return dropdownHeight;

			float bodyHeight = EditorGUI.GetPropertyHeight(property, GUIContent.none, true);
			return dropdownHeight + EditorGUIUtility.standardVerticalSpacing + bodyHeight;
		}

		/// <summary> 対象フィールドからドロップダウンの基底型を取得 </summary>
		private static Type GetBaseType(SerializeReferenceDropdownAttribute dropdownAttribute, System.Reflection.FieldInfo info) {
			if(dropdownAttribute.BaseType != null) return dropdownAttribute.BaseType;

			Type fieldType = info.FieldType;
			if(fieldType.IsArray) {
				return fieldType.GetElementType();
			}
			if(fieldType.IsGenericType) {
				return fieldType.GetGenericArguments().FirstOrDefault();
			}
			return fieldType;
		}

		/// <summary> 選択肢に使用する型一覧を生成 </summary>
		private static List<Type> GetTypeChoices(Type baseType) {
			var list = new List<Type> {
				null
			}; // null は「なし」を表す

			foreach (Type type in TypeCache.GetTypesDerivedFrom(baseType)) {
				if(type.IsAbstract || type.IsInterface) continue;
				if(type.IsGenericType) continue;
				if(type.GetConstructor(Type.EmptyTypes) == null) continue;
				list.Add(type);
			}

			return list;
		}

		/// <summary> 現在の型が選択肢中の何番目かを取得 </summary>
		private static int GetCurrentIndex(SerializedProperty property, IList<Type> choices) {
			Type currentType = GetManagedReferenceType(property);
			int index = currentType != null ? choices.IndexOf(currentType) : -1;
			return index >= 0 ? index : 0;
		}

		/// <summary> 選択された型で SerializeReference の値を生成・設定 </summary>
		private static void SetManagedReferenceValue(SerializedProperty property, Type type) {
			if(type == null) {
				property.managedReferenceValue = null;
				property.serializedObject.ApplyModifiedProperties();
				return;
			}

			try {
				object instance = Activator.CreateInstance(type);
				property.managedReferenceValue = instance;
				property.serializedObject.ApplyModifiedProperties();
			} catch (Exception ex) {
				Debug.LogError($"Failed to create instance of {type}: {ex.Message}");
			}
		}

		/// <summary> managedReferenceFullTypename から実際の型を解決 </summary>
		private static Type GetManagedReferenceType(SerializedProperty property) {
			string fullTypeName = property.managedReferenceFullTypename;
			if(string.IsNullOrEmpty(fullTypeName)) return null;

			int spaceIndex = fullTypeName.IndexOf(' ');
			if(spaceIndex < 0 || spaceIndex >= fullTypeName.Length - 1) return null;

			string assemblyName = fullTypeName.Substring(0, spaceIndex);
			string typeName = fullTypeName.Substring(spaceIndex + 1);

			return Type.GetType($"{typeName}, {assemblyName}");
		}
		#endregion
	}
}
