using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary> aLayoutGroup 系の基本動作を確認するためのテストクラス </summary>
public class aLayoutGroupTests {
	#region Methods
	/// <summary> シンプルな同期待ちなしテストの雛形 </summary>
	[Test]
	public void aLayoutGroupTestsSimplePasses() {
		// 条件を追加して検証する際のテンプレート
	}

	/// <summary> コルーチンを用いた非同期テストの雛形 </summary>
	[UnityTest]
	public IEnumerator aLayoutGroupTestsWithEnumeratorPasses() {
		// フレームをまたぐ検証を行う際のテンプレート
		yield return null;
	}
	#endregion
}
