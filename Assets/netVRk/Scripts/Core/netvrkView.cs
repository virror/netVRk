namespace netvrk
{
	using UnityEngine;

	public enum netvrkSendMethod
	{
		Unreliable,
		Reliable,
	}

	public class netvrkView : MonoBehaviour
	{
		[HideInInspector]
		public bool isMine = true;
		public bool isSceneView = true;
		public object[] instantiateData = null;
		public ushort id = 0;
		public netvrkPlayer owner;

		private void Awake ()
		{
			if(id == 0)
			{
				id = netvrkManager.GetNewViewId();
			}

			netvrkManager.AddView(id, this, gameObject);
		}

		public void Rpc(string method, netvrkTargets targets, int channel = 0, params object[] message)
		{
			netvrkManager.SendRpc(id, method, message, targets, channel);
		}

		public void Rpc(string method, netvrkPlayer player, int channel = 0, params object[] message)
		{
			netvrkManager.SendRpc(id, method, message, player, channel);
		}

		public void TransferOwnership(netvrkPlayer player)
		{
			owner = player;
			isMine = false;
			netvrkManager.SendOwnership(id, player);
		}

		public void RequestOwnership()
		{
			netvrkManager.RequestOwnership(this);
		}

		public netvrkStream GetStream()
		{
			return netvrkManager.GetStream(id);
		}

		public void WriteSyncStream(netvrkStream stream)
		{
			netvrkManager.WriteSyncStream(stream);
		}
	}
}
