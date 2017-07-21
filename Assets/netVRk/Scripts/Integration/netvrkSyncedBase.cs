namespace netvrk
{
	using System.Collections;
	using UnityEngine;

	[RequireComponent(typeof(netvrkView))]
	public abstract class netvrkSyncedBase : MonoBehaviour
	{
		public int syncPerSec = 10;
		public netvrkSendMethod sendMethod = netvrkSendMethod.Unreliable;

		protected netvrkView netView;

		protected virtual void OnEnable()
		{
			netView = GetComponent<netvrkView>();
			StartCoroutine("SyncLoop");
		}

		protected virtual void OnDisable()
		{
			StopCoroutine("SyncLoop");
		}

		private IEnumerator SyncLoop()
		{
			while(true)
			{
				if(netView.isMine)
				{
					netvrkStream stream = netView.GetStream();
					OnNetvrkWriteSyncStream(stream);
					netView.WriteSyncStream(stream, sendMethod);
				}
				yield return new WaitForSeconds(1 / syncPerSec);
			}
		}

		protected abstract void OnNetvrkWriteSyncStream(netvrkStream stream);
		protected abstract void OnNetvrkReadSyncStream(netvrkStream stream);

	}
}
