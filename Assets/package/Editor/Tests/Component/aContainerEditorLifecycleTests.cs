using System.Reflection;
using ANest.UI;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class aContainerEditorLifecycleTests {
    [Test]
    public void EditingInactiveContainerDoesNotWarn() {
        Assert.That(Application.isPlaying, Is.False);
        var go = new GameObject("Editor container warning", typeof(RectTransform));
        var warnings = 0;
        Application.LogCallback onLog = (message, stack, type) => {
            if(message.Contains("[aContainerBase]")) warnings++;
        };
        Application.logMessageReceived += onLog;
        try {
            var container = go.AddComponent<aStaticContainer>();
            go.SetActive(false);
            // シーン復元などで呼ばれる警告判定を、編集状態で確認する。
            typeof(aContainerBase).GetMethod("WarnActiveChange", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(container, null);
            Assert.That(warnings, Is.Zero);
        } finally {
            Application.logMessageReceived -= onLog;
            Object.DestroyImmediate(go);
        }
    }

}
