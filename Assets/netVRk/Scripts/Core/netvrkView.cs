namespace netvrk
{
	using UnityEngine;

	public enum netvrkSendMethod
	{
		Unreliable,
		Reliable,
		ReliableBuffered
	}

	public class netvrkView : MonoBehaviour
	{
		[HideInInspector]
		public bool isMine = true;

		private ushort id = 0;

		private void Start ()
		{
			if(id == 0)
			{
				id = netvrkManager.GetNewId();
			}

			netvrkManager.AddObj(id, this, gameObject);
		}

		public void Rpc(string method, netvrkTargets targets, object message = null, int channel = 0)
		{
			netvrkManager.SendRpc(id, method, message, targets, channel);
		}

		public void Rpc(string method, netvrkPlayer player, object message = null, int channel = 0)
		{
			netvrkManager.SendRpc(id, method, message, player, channel);
		}
	}
}
