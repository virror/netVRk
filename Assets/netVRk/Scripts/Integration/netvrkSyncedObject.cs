namespace netvrk
{
	using System.Collections;
	using System.Collections.Generic;
	using UnityEngine;

	public class netvrkSyncedObject : netvrkSyncedBase
	{
		private Vector3 newPos;
		private Quaternion newRot;

		private void Update()
		{
			transform.position = Vector3.Lerp(transform.position, newPos, Time.deltaTime * 5);
			transform.rotation = Quaternion.Lerp(transform.rotation, newRot, Time.deltaTime * 5);
		}
		protected override void OnNetvrkWriteSyncStream(netvrkStream stream)
		{
			stream.Write(transform.position);
			stream.Write(transform.rotation);
		}

		protected override void OnNetvrkReadSyncStream(netvrkStream stream)
		{
			newPos = (Vector3)stream.Read(typeof(Vector3));
			newRot = (Quaternion)stream.Read(typeof(Quaternion));
		}
	}
}
