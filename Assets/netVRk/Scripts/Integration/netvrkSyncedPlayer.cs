namespace netvrk
{
	using System.Collections;
	using System.Collections.Generic;
	using UnityEngine;
	using VRTK;

	public class netvrkSyncedPlayer : netvrkSyncedBase
	{
		public Transform networkHead;
		public Transform networkLeftHand;
		public Transform networkRightHand;

		private Transform player;
		private Transform head;
		private Transform leftHand;
		private Transform rightHand;
		private GameObject leftAlias;
		private GameObject rightAlias;

		private Vector3 playerNewPos;
		private Quaternion playerNewRot;
		private Vector3 headNewPos;
		private Quaternion headNewRot;
		private Vector3 leftHandNewPos;
		private Quaternion leftHandNewRot;
		private Vector3 rightHandNewPos;
		private Quaternion rightHandNewRot;

		private void Awake()
		{
			VRTK_SDKManager.instance.AddBehaviourToToggleOnLoadedSetupChange(this);
			leftAlias = VRTK_SDKManager.instance.scriptAliasLeftController;
			rightAlias = VRTK_SDKManager.instance.scriptAliasRightController;
			leftAlias.GetComponent<VRTK_InteractGrab>().ControllerGrabInteractableObject += OnGrab;
			leftAlias.GetComponent<VRTK_InteractGrab>().ControllerUngrabInteractableObject += OnUngrab;
			leftAlias.GetComponent<VRTK_InteractUse>().ControllerUseInteractableObject += OnUse;
			leftAlias.GetComponent<VRTK_InteractUse>().ControllerUnuseInteractableObject += OnUnuse;
			rightAlias.GetComponent<VRTK_InteractGrab>().ControllerGrabInteractableObject += OnGrab;
			rightAlias.GetComponent<VRTK_InteractGrab>().ControllerUngrabInteractableObject += OnUngrab;
			rightAlias.GetComponent<VRTK_InteractUse>().ControllerUseInteractableObject += OnUse;
			rightAlias.GetComponent<VRTK_InteractUse>().ControllerUnuseInteractableObject += OnUnuse;
		}

		private void Update()
		{
			transform.position = Vector3.Lerp(transform.position, playerNewPos, Time.deltaTime * 5);
			transform.rotation = Quaternion.Lerp(transform.rotation, playerNewRot, Time.deltaTime * 5);
			head.position = Vector3.Lerp(head.position, headNewPos, Time.deltaTime * 5);
			head.rotation = Quaternion.Lerp(head.rotation, headNewRot, Time.deltaTime * 5);
			leftHand.position = Vector3.Lerp(leftHand.position, leftHandNewPos, Time.deltaTime * 5);
			leftHand.rotation = Quaternion.Lerp(leftHand.rotation, leftHandNewRot, Time.deltaTime * 5);
			rightHand.position = Vector3.Lerp(rightHand.position, rightHandNewPos, Time.deltaTime * 5);
			rightHand.rotation = Quaternion.Lerp(rightHand.rotation, rightHandNewRot, Time.deltaTime * 5);
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
			playerNewPos = (Vector3)stream.Read(typeof(Vector3));
			playerNewRot = Quaternion.Euler(0, (float)stream.Read(typeof(float)), 0);
			headNewPos = (Vector3)stream.Read(typeof(Vector3));
			headNewRot = (Quaternion)stream.Read(typeof(Quaternion));
			leftHandNewPos = (Vector3)stream.Read(typeof(Vector3));
			leftHandNewRot = (Quaternion)stream.Read(typeof(Quaternion));
			rightHandNewPos = (Vector3)stream.Read(typeof(Vector3));
			rightHandNewRot = (Quaternion)stream.Read(typeof(Quaternion));
		}

		private void OnGrab(object sender, ObjectInteractEventArgs e)
		{
			netvrkSyncedBase syncScript = e.target.GetComponent<netvrkSyncedBase>();
			if(syncScript != null)
			{
				netvrkView ioView = e.target.GetComponent<netvrkView>();
				ioView.RequestOwnership();
				syncScript.enabled = false;
				netView.Rpc("GrabRpc", netvrkTargets.Other, 0, true, ioView.id);
			}
		}

		private void OnUngrab(object sender, ObjectInteractEventArgs e)
		{
			netvrkSyncedBase syncScript = e.target.GetComponent<netvrkSyncedBase>();
			if(syncScript != null)
			{
				ushort viewId = e.target.GetComponent<netvrkView>().id;
				syncScript.enabled = true;
				netView.Rpc("GrabRpc", netvrkTargets.Other, 0, false, viewId);
			}
		}

		private void OnUse(object sender, ObjectInteractEventArgs e)
		{
			netvrkSyncedBase syncScript = e.target.GetComponent<netvrkSyncedBase>();
			if(syncScript != null)
			{
				ushort viewId = e.target.GetComponent<netvrkView>().id;
				netView.Rpc("UseRpc", netvrkTargets.Other, 0, true, viewId);
			}
		}

		private void OnUnuse(object sender, ObjectInteractEventArgs e)
		{
			netvrkSyncedBase syncScript = e.target.GetComponent<netvrkSyncedBase>();
			if(syncScript != null)
			{
				ushort viewId = e.target.GetComponent<netvrkView>().id;
				netView.Rpc("UseRpc", netvrkTargets.Other, 0, false, viewId);
			}
		}

		[netvrkRpc]
		public void GrabRpc(bool grab, ushort viewId)
		{
			GameObject ioObject = netvrkManager.GetViewById(viewId).gameObject;
			VRTK_InteractableObject io = ioObject.GetComponent<VRTK_InteractableObject>();
			if(grab)
			{
				io.isGrabbable = false;
				SDK_BaseController.ControllerHand hand = VRTK_DeviceFinder.GetControllerHand(io.GetGrabbingObject());
				if(hand == SDK_BaseController.ControllerHand.Left)
				{
					ioObject.transform.SetParent(networkLeftHand);
				}
				else if(hand == SDK_BaseController.ControllerHand.Right)
				{
					ioObject.transform.SetParent(networkRightHand);
				}
			}
			else
			{
				io.isGrabbable = true;
				ioObject.transform.SetParent(null);
			}
		}

		[netvrkRpc]
		public void UseRpc(bool use, ushort viewId)
		{
			GameObject ioObject = netvrkManager.GetViewById(viewId).gameObject;
			VRTK_InteractableObject io = ioObject.GetComponent<VRTK_InteractableObject>();
			if(use)
			{
				io.StartUsing();
			}
			else
			{
				io.StopUsing();
			}
		}
	}
}
