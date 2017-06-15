using UnityEngine;
using netvrk;

public class TestScript : MonoBehaviour
{
	netvrkView netView;

	private void Start()
	{
		netView = GetComponent<netvrkView>();
		byte[] array = {1,2,3,4,5,6,4};
		netView.Rpc("SomeFunction", netvrkTargets.All, array);
		//netvrkManager.TestInstantiate("NetPlayer", new Vector3(2, 1, 3), Quaternion.identity);
	}

	[netvrkRpc]
	public void SomeFunction(byte[] apa)
	{
		foreach (byte item in apa)
		{
			Debug.Log(item);
		}
	}
}
