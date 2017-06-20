namespace netvrk.Examples
{
	using System.Collections;
	using System.Collections.Generic;
	using UnityEngine;

	[RequireComponent(typeof(netvrkView))]
	public class PlayerSpawner : MonoBehaviour
	{
		public GameObject localPlayer;
		public string networkPlayerPath;

		private netvrkView netView;
		
		private void Start()
		{
			netView = GetComponent<netvrkView>();
			ushort newId = netvrkManager.GetNewViewId();
			GameObject go = Instantiate(localPlayer);
			go.GetComponent<netvrkView>().id = newId;
			netView.Rpc("SpawnNetworkPlayer", netvrkTargets.Other, 0, networkPlayerPath, transform.position, newId);
		}

		[netvrkRpc]
		private void SpawnNetworkPlayer(string path, Vector3 position, ushort viewId)
		{
			GameObject go = (GameObject)Resources.Load(path);
			GameObject instanceGo = Instantiate(go, position, Quaternion.identity);
			instanceGo.GetComponent<netvrkView>().id = viewId;
		}
	}
}
