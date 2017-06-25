namespace netvrk
{
	using System.Collections;
	using System.Collections.Generic;
	using UnityEngine;
	using VRTK;

	public class netvrkSyncedPlayer : netvrkSyncedBase
	{
		private Transform player;
		private Transform head;
		private Transform leftHand;
		private Transform rightHand;

		protected override void Awake()
		{
			base.Awake();
			VRTK_SDKManager.instance.AddBehaviourToToggleOnLoadedSetupChange(this);
			GameObject leftAlias = VRTK_SDKManager.instance.scriptAliasLeftController;
			GameObject rightAlias = VRTK_SDKManager.instance.scriptAliasRightController;
			leftAlias.GetComponent<VRTK_InteractGrab>().ControllerGrabInteractableObject += OnGrab;
			leftAlias.GetComponent<VRTK_InteractGrab>().ControllerUngrabInteractableObject += OnUngrab;
			leftAlias.GetComponent<VRTK_InteractUse>().ControllerUseInteractableObject += OnUse;
			leftAlias.GetComponent<VRTK_InteractUse>().ControllerUnuseInteractableObject += OnUnuse;
			rightAlias.GetComponent<VRTK_InteractGrab>().ControllerGrabInteractableObject += OnGrab;
			rightAlias.GetComponent<VRTK_InteractGrab>().ControllerUngrabInteractableObject += OnUngrab;
			rightAlias.GetComponent<VRTK_InteractUse>().ControllerUseInteractableObject += OnUse;
			rightAlias.GetComponent<VRTK_InteractUse>().ControllerUnuseInteractableObject += OnUnuse;
		}

		protected virtual void OnDestroy()
        {
            VRTK_SDKManager.instance.RemoveBehaviourToToggleOnLoadedSetupChange(this);
        }

		protected override void OnEnable()
		{
			player = VRTK_DeviceFinder.PlayAreaTransform();
			head = VRTK_DeviceFinder.HeadsetTransform();
			leftHand = VRTK_DeviceFinder.GetControllerLeftHand().transform;
			rightHand = VRTK_DeviceFinder.GetControllerRightHand().transform;
			base.OnEnable();
		}

		protected override void OnNetvrkWriteSyncStream(netvrkStream stream)
		{
			stream.Write(player.position);
			stream.Write(player.rotation.eulerAngles.y);
			stream.Write(head.position);
			stream.Write(head.rotation);
			stream.Write(leftHand.position);
			stream.Write(leftHand.rotation);
			stream.Write(rightHand.position);
			stream.Write(rightHand.rotation);
		}

		protected override void OnNetvrkReadSyncStream(netvrkStream stream)
		{
			player.position = (Vector3)stream.Read(typeof(Vector3));
			player.rotation = Quaternion.Euler(0, (float)stream.Read(typeof(float)), 0);
			head.position = (Vector3)stream.Read(typeof(Vector3));
			head.rotation = (Quaternion)stream.Read(typeof(Quaternion));
			leftHand.position = (Vector3)stream.Read(typeof(Vector3));
			leftHand.rotation = (Quaternion)stream.Read(typeof(Quaternion));
			rightHand.position = (Vector3)stream.Read(typeof(Vector3));
			rightHand.rotation = (Quaternion)stream.Read(typeof(Quaternion));
		}

		private void OnGrab(object sender, ObjectInteractEventArgs e)
		{
			netvrkSyncedBase syncScript = e.target.GetComponent<netvrkSyncedBase>();
			if(syncScript != null)
			{
				syncScript.enabled = false;
			}
		}

		private void OnUngrab(object sender, ObjectInteractEventArgs e)
		{
			netvrkSyncedBase syncScript = e.target.GetComponent<netvrkSyncedBase>();
			if(syncScript != null)
			{
				syncScript.enabled = true;
			}
		}

		private void OnUse(object sender, ObjectInteractEventArgs e)
		{
			//Sync use
		}

		private void OnUnuse(object sender, ObjectInteractEventArgs e)
		{
			//Sync unuse
		}
	}
}
