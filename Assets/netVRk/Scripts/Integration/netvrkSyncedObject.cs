namespace netvrk
{
	using System.Collections;
	using System.Collections.Generic;
	using UnityEngine;

	public class netvrkSyncedObject : netvrkSyncedBase
	{
		protected override void OnNetvrkWriteSyncStream(netvrkStream stream)
		{
			stream.Write(transform.position);
			stream.Write(transform.rotation);
		}

		protected override void OnNetvrkReadSyncStream(netvrkStream stream)
		{
			transform.position = (Vector3)stream.Read(typeof(Vector3));
			transform.rotation = (Quaternion)stream.Read(typeof(Quaternion));
		}
	}
}
