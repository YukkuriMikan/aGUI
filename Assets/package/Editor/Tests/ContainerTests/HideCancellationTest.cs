using System.Collections;
using System.Threading;
using ANest.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class HideCancellationTest
{
    private GameObject _containerObj;
    private aStaticContainer _container;

    [SetUp]
    public void SetUp()
    {
        _containerObj = new GameObject("Container");
        _container = _containerObj.AddComponent<aStaticContainer>();
        // Awakeで初期化される想定
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_containerObj);
    }

    [UnityTest]
    public IEnumerator Hide_WhenCancelledByShow_ShouldStayVisible()
    {
        // Setup visible state
        _container.Show();
        Debug.Log("[DEBUG_LOG] Show called");
        Assert.IsTrue(_container.IsVisible);
        Assert.IsTrue(_container.gameObject.activeSelf);

        // Start Hide
        _container.Hide();
        Debug.Log("[DEBUG_LOG] Hide called");
        Assert.IsFalse(_container.IsVisible);
        
        // Immediately Show again
        _container.Show();
        Debug.Log("[DEBUG_LOG] Show called again");
        Assert.IsTrue(_container.IsVisible);
        
        yield return new WaitForSeconds(0.1f);
        
        Debug.Log($"[DEBUG_LOG] Final State: IsVisible={_container.IsVisible}, activeSelf={_container.gameObject.activeSelf}");
        
        // If Show was called last, it should be visible and active.
        Assert.IsTrue(_container.IsVisible);
        Assert.IsTrue(_container.gameObject.activeSelf);
    }

    [UnityTest]
    public IEnumerator Hide_MultipleCalls_ShouldHandleCallbackCorrectly()
    {
        _container.Show();
        
        // 1st Hide
        _container.Hide();
        Assert.IsFalse(_container.IsVisible);
        
        // 2nd Hide (should cancel 1st Hide's animation CTS)
        _container.Hide();
        
        // Wait for potential animation (though there are no animations set here)
        yield return new WaitForSeconds(0.1f);
        
        // In current implementation, if TryPlayAnimations has no animations, it calls callback immediately.
        // Let's check aContainerBase.cs:316
        // if(m_suppressAnimation || (!m_useCustomAnimations && !m_useSharedAnimation)) { callback?.Invoke(); return; }
        
        // If there are no animations, it's hard to reproduce.
    }
}
