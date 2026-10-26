using System;
using System.Collections.Generic;
using System.Reflection;
using ANest.UI;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class ScriptIconSetterTests {
	private static readonly Type Setter = typeof(ANest.UI.Editor.aGuiEditorUtils).Assembly.GetType("ANest.UI.Editor.ScriptIconSetter");
	private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
	private readonly List<GameObject> _objects = new();
	private static Dictionary<int, Texture2D> Icons => Field<Dictionary<int, Texture2D>>("s_hierarchyIcons");

	[SetUp]
	public void SetUp() {
		Assert.That(Application.isPlaying, Is.False);
		Call("OnHierarchyChanged");
		Call("RefreshCursorTargets");
		Icons.Clear();
	}

	[TearDown]
	public void TearDown() {
		foreach(var go in _objects) if(go) Object.DestroyImmediate(go);
		_objects.Clear();
		Call("OnHierarchyChanged");
	}

	[TestCase(EventType.Layout)]
	[TestCase(EventType.MouseMove)]
	[TestCase(EventType.MouseDown)]
	public void NonRepaintEventsDoNotResolveRowsOrRebuildCursorTargets(EventType eventType) {
		var go = Create("Uncached row");
		Call("MarkCursorTargetsDirty");
		Assert.That(Call("GetHierarchyIcon", go.GetInstanceID(), eventType), Is.Null);
		Assert.That(Icons, Is.Empty);
		Assert.That(Field<bool>("s_cursorTargetsDirty"), Is.True);
	}

	[Test]
	public void CachedRowLookupsDoNotAllocate() {
		var go = Create("Cached ordinary row");
		var instanceId = go.GetInstanceID();
		var lookup = (Func<int, EventType, Texture2D>)Setter.GetMethod("GetHierarchyIcon", StaticPrivate)
			.CreateDelegate(typeof(Func<int, EventType, Texture2D>));
		lookup(instanceId, EventType.Repaint);
		var allocated = GC.GetAllocatedBytesForCurrentThread();
		for(var i = 0; i < 10000; i++) lookup(instanceId, EventType.Repaint);
		Assert.That(GC.GetAllocatedBytesForCurrentThread() - allocated, Is.Zero);
	}

	[Test]
	public void MissingIconsAreCachedUntilTheStructureChanges() {
		var go = Create("Initially ordinary row");
		Assert.That(Icon(go), Is.Null);
		go.AddComponent<aButton>();
		Assert.That(Icon(go), Is.Null, "Negative lookups must not repeat component searches");
		PublishStructureChange(go);
		Assert.That(Icon(go), Is.SameAs(TypeIcon(typeof(aButton))));
		Object.DestroyImmediate(go.GetComponent<aButton>());
		PublishStructureChange(go);
		Assert.That(Icon(go), Is.Null, "Removing a component must discard its cached icon");
	}

	[Test]
	public void MovingAnUnrelatedTransformDoesNotInvalidateEitherCache() {
		var go = Create("Moving ordinary row");
		Icon(go);
		using var builder = new ObjectChangeEventStream.Builder(Allocator.Temp);
		var change = new ChangeGameObjectOrComponentPropertiesEventArgs(go.transform.GetInstanceID(), go.scene);
		builder.PushChangeGameObjectOrComponentPropertiesEvent(ref change);
		using var stream = builder.ToStream(Allocator.Temp);
		Call("OnObjectChanged", stream);
		Assert.That(Icons.ContainsKey(go.GetInstanceID()), Is.True);
		Assert.That(Field<bool>("s_cursorTargetsDirty"), Is.False);
		Assert.That(Field<bool>("s_cursorRefreshQueued"), Is.False);
	}

	[Test]
	public void CursorReferenceChangesRefreshOnlyAffectedRowsOutsideDrawing() {
		var first = Create("First cursor target");
		var second = Create("Second cursor target");
		var unrelated = Create("Unrelated cached row");
		var cursor = Create("Cursor owner").AddComponent<aCursorBase>();
		SetCursorRect(cursor, first);
		Call("OnHierarchyChanged");
		Call("RefreshCursorTargets");
		var cursorIcon = Field<Texture2D>("s_cursorIcon");
		Assert.That(Icon(first), Is.SameAs(cursorIcon));
		Assert.That(Icon(second), Is.Null);
		Icon(unrelated);
		SetCursorRect(cursor, second);
		using var builder = new ObjectChangeEventStream.Builder(Allocator.Temp);
		var change = new ChangeGameObjectOrComponentPropertiesEventArgs(cursor.GetInstanceID(), cursor.gameObject.scene);
		builder.PushChangeGameObjectOrComponentPropertiesEvent(ref change);
		using var stream = builder.ToStream(Allocator.Temp);
		Call("OnObjectChanged", stream);
		Assert.That(Field<bool>("s_cursorTargetsDirty"), Is.True);
		Assert.That(Icon(first), Is.SameAs(cursorIcon), "Row lookup must not trigger a scene-wide search");
		Call("RefreshCursorTargets");
		Assert.That(Icon(first), Is.Null);
		Assert.That(Icon(second), Is.SameAs(cursorIcon));
		Assert.That(Icons.ContainsKey(unrelated.GetInstanceID()), Is.True);
		Assert.That(Field<bool>("s_cursorTargetsDirty"), Is.False);
	}

	[Test]
	public void InactiveCursorsAndSharedTargetsSurviveCacheRefresh() {
		var target = Create("Shared cursor target");
		var first = Create("Inactive cursor").AddComponent<aCursorBase>();
		var second = Create("Other cursor").AddComponent<aCursorBase>();
		SetCursorRect(first, target);
		SetCursorRect(second, target);
		first.gameObject.SetActive(false);
		Call("RebuildCursorTargetCache");
		Assert.That(Icon(target), Is.SameAs(Field<Texture2D>("s_cursorIcon")));
		Object.DestroyImmediate(second);
		Call("RebuildCursorTargetCache");
		Assert.That(Icon(target), Is.SameAs(Field<Texture2D>("s_cursorIcon")));
		Object.DestroyImmediate(first);
		Call("RebuildCursorTargetCache");
		Assert.That(Icon(target), Is.Null);
	}

	[Test]
	public void DescendantsAndGenericScriptNamesKeepTheirIcons() {
		var types = Field<Dictionary<Type, Texture2D>>("s_scriptTypeIcons");
		var names = Field<Dictionary<string, Texture2D>>("s_scriptNameIcons");
		Assert.That(types[typeof(aStaticContainer)], Is.SameAs(TypeIcon(typeof(aContainerBase))));
		Assert.That(types[typeof(aSelectableCursor)], Is.SameAs(TypeIcon(typeof(aCursorBase))));
		Assert.That(names["aSelectableContainerBase"], Is.SameAs(TypeIcon(typeof(aContainerBase))));
		Assert.That(names["aNormalCursorBase"], Is.SameAs(TypeIcon(typeof(aCursorBase))));
	}

	[Test]
	public void DuplicateInvalidationsQueueOnlyOneRefresh() {
		var go = Create("Visible row");
		Call("MarkCursorTargetsDirty");
		Icon(go);
		var queued = EditorApplication.delayCall;
		for(var i = 0; i < 100; i++) {
			Call("MarkCursorTargetsDirty");
			Icon(go);
		}
		Assert.That(EditorApplication.delayCall, Is.EqualTo(queued));
		Call("RefreshCursorTargets");
		Assert.That(Field<bool>("s_cursorRefreshQueued"), Is.False);
	}

	[Test]
	public void DirtyCursorTargetsDoNotQueueSceneSearchUntilARepaint() {
		var go = Create("Hidden hierarchy row");
		Call("MarkCursorTargetsDirty");
		Assert.That(Field<bool>("s_cursorRefreshQueued"), Is.False);
		Call("GetHierarchyIcon", go.GetInstanceID(), EventType.Layout);
		Assert.That(Field<bool>("s_cursorRefreshQueued"), Is.False);
		Icon(go);
		Assert.That(Field<bool>("s_cursorRefreshQueued"), Is.True);
	}

	private GameObject Create(string name) {
		var go = new GameObject(name, typeof(RectTransform));
		_objects.Add(go);
		return go;
	}

	private static void SetCursorRect(aCursorBase cursor, GameObject target) {
		using var serialized = new SerializedObject(cursor);
		serialized.FindProperty("m_cursorRect").objectReferenceValue = target.transform;
		serialized.ApplyModifiedPropertiesWithoutUndo();
	}

	private static void PublishStructureChange(GameObject go) {
		using var builder = new ObjectChangeEventStream.Builder(Allocator.Temp);
		var change = new ChangeGameObjectStructureEventArgs(go.GetInstanceID(), go.scene);
		builder.PushChangeGameObjectStructureEvent(ref change);
		using var stream = builder.ToStream(Allocator.Temp);
		Call("OnObjectChanged", stream);
	}

	private static Texture2D TypeIcon(Type type) => Field<Dictionary<Type, Texture2D>>("s_iconMap")[type];
	private static Texture2D Icon(GameObject go) => (Texture2D)Call("GetHierarchyIcon", go.GetInstanceID(), EventType.Repaint);
	private static T Field<T>(string name) => (T)Setter.GetField(name, StaticPrivate).GetValue(null);
	private static object Call(string name, params object[] args) => Setter.GetMethod(name, StaticPrivate).Invoke(null, args);
}
