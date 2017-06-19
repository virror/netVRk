namespace netvrk
{
	using UnityEngine;
	using System;
	using System.IO;
	using System.Reflection;
	using System.Collections;
	using System.Collections.Generic;
	using Steamworks;

	public delegate void netVRkPlayerEventHandler(netvrkPlayer player);
	public delegate void netVRkConnectionHandler();
	public delegate void netVRkEventHandler(byte eventCode, object[] data, netvrkPlayer player);

	public enum netvrkTargets
	{
		All,
		Other
	}

	public enum eventCode
	{
		Internal,
		Rpc,
		Sync,
		End
	}

	public class netvrkManager : MonoBehaviour
	{
		private ushort maxId = 1;
		private static netvrkManager instance;
		private static Dictionary<ushort, ObjData> objList = new Dictionary<ushort, ObjData>();
		private static List<netvrkPlayer> playerList = new List<netvrkPlayer>();
		private static netvrkPlayer masterClient;
		private static bool isMasterClient = false;
		private static bool isConnected = false;
		private Callback<P2PSessionRequest_t> p2PSessionRequestCallback;
		private static byte maxPlayersAllowed = 0;

		//Events
		public static event netVRkConnectionHandler connectSuccess;
		public static event netVRkConnectionHandler connectFail;
		public static event netVRkConnectionHandler disconnect;
		public static event netVRkPlayerEventHandler playerJoin;
		public static event netVRkPlayerEventHandler playerDisconnect;
		public static event netVRkEventHandler eventCall;

		public enum InternalMethod
		{
			Instantiate,
			ConnectRequest,
			ConnectResponse,
			PlayerJoin,
			PlayerDisconnect,
			Tick,
			Tock
		}

		private struct ObjData
		{
			public netvrkView netObj;
			public List<string> methods;
			public List<MethodInfo> rpcMethods;
			public List<MonoBehaviour> scripts;
			public MethodInfo syncMethod;
			public MonoBehaviour syncScript;
		}

		private struct InternalData
		{
			public object[] data;
			public CSteamID remoteId;
		}

		public static ushort GetNewId()
		{
			instance.maxId++;
			return instance.maxId;
		}

		public static void AddObj(ushort id, netvrkView obj, GameObject go)
		{
			MonoBehaviour[] scripts = go.GetComponents<MonoBehaviour>();
			ObjData data = new ObjData();
			data.methods = new List<string>();
			data.scripts = new List<MonoBehaviour>();
			data.rpcMethods = new List<MethodInfo>();
			data.netObj = obj;

			foreach (MonoBehaviour script in scripts)
			{
				Type type = script.GetType();
				MethodInfo[] objectFields = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);
				for (int i = 0; i < objectFields.Length; i++)
				{
					netvrkRpc attribute = Attribute.GetCustomAttribute(objectFields[i], typeof(netvrkRpc)) as netvrkRpc;
					if(attribute != null)
					{
						data.methods.Add(objectFields[i].Name);
						data.scripts.Add(script);
						data.rpcMethods.Add(type.GetMethod(objectFields[i].Name));
					}
				}
				MethodInfo info = type.GetMethod("OnNetvrkReadSyncStream");
				if(info != null)
				{
					data.syncMethod = info;
					data.syncScript = script;
				}
			}
			objList.Add(id, data);
		}

		public static void SendRpc(ushort objId, string method, object[] data, netvrkTargets targets, int channel = 0)
		{
			if(!isConnected)
			{
				Debug.LogWarning("netVRk: Can not send RPCs when not connected!");
				return;
			}
			int methodId = GetObjMethodId(objId, method);
			if(methodId < 0)
			{
				Debug.LogError("netVRk: Rpc method: " + method + " not found!");
				return;
			}

			byte[] bytes = netvrkSerialization.SerializeRpc(objId, (byte)methodId, data);
			for(int i = 0; i < playerList.Count; i++)
			{
				SteamNetworking.SendP2PPacket(playerList[i].SteamId, bytes, (uint)bytes.Length, EP2PSend.k_EP2PSendReliable, channel);
			}
			if(targets == netvrkTargets.All)
			{
				GameObject go = objList[objId].netObj.gameObject;
				go.SendMessage(method, data, SendMessageOptions.RequireReceiver);
			}
		}

		public static void SendRpc(ushort objId, string method, object[] data, netvrkPlayer player, int channel = 0)
		{
			if(!isConnected)
			{
				Debug.LogWarning("netVRk: Can not send RPCs when not connected!");
				return;
			}
			int methodId = GetObjMethodId(objId, method);
			if(methodId < 0)
			{
				Debug.LogError("netVRk: Rpc method: " + method + " not found!");
				return;
			}

			byte[] bytes = netvrkSerialization.SerializeRpc(objId, (byte)methodId, data);
			SteamNetworking.SendP2PPacket(player.SteamId, bytes, (uint)bytes.Length, EP2PSend.k_EP2PSendReliable, channel);
		}

		public static List<netvrkPlayer> GetPlayerList()
		{
			return playerList;
		}

		public static void Instantiate(string prefabName, Vector3 position, Quaternion rotation, int channel = 0, params object[] data)
		{
			if(!isConnected)
			{
				Debug.LogWarning("netVRk: Can not instantiate over the network when not connected!");
				return;
			}
			GameObject go = (GameObject)Resources.Load(prefabName);
			netvrkView view = go.GetComponent<netvrkView>();
			if(view == null)
			{
				Debug.LogError("netVRk: Can not instantiate object '" + prefabName + "' because its missing a netvtkView component!");
				return;
			}

			object[] internalData = new object[data.Length + 3];
			internalData[0] = position;
			internalData[1] = rotation.eulerAngles;
			internalData[2] = prefabName;
			Array.Copy(data, 0, internalData, 3, data.Length);
			byte[] bytes = netvrkSerialization.SerializeInternal((byte)InternalMethod.Instantiate, internalData);

			for(int i = 0; i < playerList.Count; i++)
			{
				SteamNetworking.SendP2PPacket(playerList[i].SteamId, bytes, (uint)bytes.Length, EP2PSend.k_EP2PSendReliable, channel);
			}

			GameObject instanceGo = Instantiate(go, position, rotation);
			netvrkView netView = instanceGo.GetComponent<netvrkView>();
			netView.instantiateData = data;
		}

		public static bool IsMasterClient()
		{
			return isMasterClient;
		}

		public static bool IsConnected()
		{
			return isConnected;
		}

		public byte GetMaxPlayers()
		{
			return maxPlayersAllowed;
		}

		public static netvrkPlayer GetMasterClient()
		{
			return masterClient;
		}

		public static void CreateGame(byte maxPlayers)
		{
			if(isConnected)
			{
				Debug.LogWarning("netVRk: Can not create a new game while still connected!");
				return;
			}
			isMasterClient = true;
			isConnected = true;
			masterClient = new netvrkPlayer(SteamUser.GetSteamID(), true, true);
			maxPlayersAllowed = maxPlayers;
			instance.StartCoroutine("TickLoop");
		}

		public static void JoinGame(string steamName)
		{
			if(isConnected)
			{
				Debug.LogWarning("netVRk: Can not join a new game while still connected!");
				return;
			}
			int friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
			for (int i = 0; i < friendCount; ++i)
			{
				CSteamID friendSteamId = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
				string friendName = SteamFriends.GetFriendPersonaName(friendSteamId);

				if(friendName == steamName)
				{
					SendInternalRpc(friendSteamId, InternalMethod.ConnectRequest);
				}
				instance.Invoke("ConnectionFail", 5);
			}
		}

		public static void Disconnect()
		{
			isMasterClient = false;
			isConnected = false;
			object[] data = {SteamUser.GetSteamID().m_SteamID};

			for(int i = 0; i < playerList.Count; i++)
			{
				SendInternalRpc(playerList[i].SteamId, InternalMethod.PlayerDisconnect, data);
			}
			playerList.Clear();
			masterClient = null;
			maxPlayersAllowed = 0;
			instance.StopCoroutine("TickLoop");
		}

		public static void RaiseEvent(byte eventId, netvrkSendMethod method, params object[] data)
		{
			if(!isConnected)
			{
				Debug.LogWarning("netVRk: Can not send events when not connected!");
				return;
			}
			eventId += (byte)eventCode.End;
			byte[] bytes = netvrkSerialization.SerializeEvent(eventId, data);
			for(int i = 0; i < playerList.Count; i++)
			{
				SteamNetworking.SendP2PPacket(playerList[i].SteamId, bytes, (uint)bytes.Length, EP2PSend.k_EP2PSendReliable, 0);
			}
		}

		public static netvrkStream GetStream(ushort objId)
		{
			netvrkStream stream = new netvrkStream(objId);
			return stream;
		}

		public static void WriteSyncStream(netvrkStream stream)
		{
			byte[] buffer = stream.GetStreamData();
			byte[] bytes2 = new byte[buffer.Length + 3];
			byte[] objId = BitConverter.GetBytes(stream.ObjId);
			bytes2[0] = (byte)eventCode.Sync;
			bytes2[1] = objId[0];
			bytes2[2] = objId[1];
			Buffer.BlockCopy(buffer, 0, bytes2, 3, buffer.Length);
			for(int i = 0; i < playerList.Count; i++)
			{
				SteamNetworking.SendP2PPacket(playerList[i].SteamId, bytes2, (uint)bytes2.Length, EP2PSend.k_EP2PSendReliable, 0);
			}
		}

		private void Awake()
		{
			if (instance != null)
			{
				Debug.LogWarning("netVRk: You can only have one netvrkManager object in the scene!");
				return;
			}
			instance = this;

			p2PSessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
			ObjData data = new ObjData();
			data.netObj = null;
			data.methods = new List<string>();
			data.methods.Add("InstantiatePrefab");
			data.methods.Add("ConnectionRequest");
			data.methods.Add("ConnectionResponse");
			data.methods.Add("PlayerJoin");
			data.methods.Add("PlayerDisconnect");
			data.methods.Add("Tick");
			data.methods.Add("Tock");
			objList.Add(0, data);
		}

		private void Update()
		{
			uint size;
			while (SteamNetworking.IsP2PPacketAvailable(out size))
			{
				byte[] buffer = new byte[size];
				uint bytesRead;
				CSteamID remoteId;
 
				if (SteamNetworking.ReadP2PPacket(buffer, size, out bytesRead, out remoteId))
				{
					switch((eventCode)buffer[0])
					{
						case eventCode.Internal:
							UnpackInternal(buffer, remoteId);
							break;
						case eventCode.Rpc:
							UnpackRpc(buffer);
							break;
						case eventCode.Sync:
							UnpackSync(buffer);
							break;
						default:
							UnpackEvent(buffer, remoteId);
							break;
					}
				}
			}
		}

		private void OnP2PSessionRequest(P2PSessionRequest_t request)
		{
			CSteamID clientId = request.m_steamIDRemote;
			Debug.Log("OnP2PSessionRequest");
			if (IsExpectingClient(clientId))
			{
				SteamNetworking.AcceptP2PSessionWithUser(clientId);
			}
			else
			{
				Debug.LogWarning("Unexpected session request from " + clientId);
			}
		}

		private netvrkPlayer IsInPlayerList(CSteamID clientId)
		{
			for(int i = 0; i < playerList.Count; i++)
			{
				if(playerList[i].SteamId == clientId)
				{
					return playerList[i];
				}
			}
			return null;
		}

		private bool IsExpectingClient(CSteamID clientId)
		{
			if(playerList.Count + 1 < maxPlayersAllowed)
			{
				return SteamFriends.HasFriend(clientId, EFriendFlags.k_EFriendFlagAll) && isConnected;
			}
			return false;
		}

		private static EP2PSend netvrkToP2pSend(netvrkSendMethod deliveryMethod)
		{
			switch(deliveryMethod)
			{
				case netvrkSendMethod.Reliable:
					return EP2PSend.k_EP2PSendReliable;
				case netvrkSendMethod.Unreliable:
					return EP2PSend.k_EP2PSendUnreliable;
				default:
					return EP2PSend.k_EP2PSendUnreliable;
			}
		}

		private static int GetObjMethodId(ushort objId, string methodName)
		{
			for(byte i = 0; i < objList[objId].methods.Count; i++)
			{
				if(objList[objId].methods[i] == methodName)
				{
					return i;
				}
			}
			return -1;
		}

		private void UnpackRpc(byte[] buffer)
		{
			netvrkSerialization.unpackOutput output = netvrkSerialization.UnserializeRpc(buffer);
			ObjData data = objList[output.objectId];
			int id = output.methodId;
			data.rpcMethods[id].Invoke(data.scripts[id], output.data);
		}

		private void UnpackInternal(byte[] buffer, CSteamID remoteId)
		{
			netvrkSerialization.unpackOutput output = netvrkSerialization.UnserializeInternal(buffer);
			string methodName = objList[0].methods[output.methodId];

			InternalData intData = new InternalData();
			intData.remoteId = remoteId;
			intData.data = output.data;
			
			StartCoroutine(methodName, intData);
		}

		private void UnpackEvent(byte[] buffer, CSteamID remoteId)
		{
			netvrkSerialization.unpackOutput output = netvrkSerialization.UnserializeEvent(buffer);
			netvrkPlayer player = IsInPlayerList(remoteId);
			if (eventCall != null)
            {
                eventCall(output.eventId, output.data, player);
            }
		}

		private void UnpackSync(byte[] buffer)
		{
			ushort objId = BitConverter.ToUInt16(buffer, 1);
			byte[] tmpBuffer = new byte[buffer.Length - 3];
			Buffer.BlockCopy(buffer, 3, tmpBuffer, 0, tmpBuffer.Length);

			netvrkStream stream = new netvrkStream(tmpBuffer);
			object[] objs = {stream};
			objList[objId].syncMethod.Invoke(objList[objId].syncScript, objs);
		}

		private IEnumerator InstantiatePrefab(InternalData internalData)
		{
			Vector3 position = (Vector3)internalData.data[0];
			Quaternion rotation = Quaternion.Euler((Vector3)internalData.data[1]);
			string prefabName = (string)internalData.data[2];

			GameObject go = (GameObject)Resources.Load(prefabName);
			GameObject instanceGo = Instantiate(go, position, rotation);
			netvrkView netView = instanceGo.GetComponent<netvrkView>();
			netView.isMine = false;
			netView.isSceneView = false;
			int len = internalData.data.Length - 3;
			if(len > 0)
			{
				netView.instantiateData = new object[len];
				Array.Copy(internalData.data, 3, netView.instantiateData, 0, len);
			}
			yield return null;
		}

		private IEnumerator ConnectionRequest(InternalData internalData)
		{
			CSteamID clientId = internalData.remoteId;
			object[] data = {SteamUser.GetSteamID().m_SteamID};
			byte[] bytes = netvrkSerialization.SerializeInternal((byte)InternalMethod.PlayerJoin, data);

			SendInternalRpc(clientId, InternalMethod.ConnectResponse);
			
			for(int i = 0; i < playerList.Count; i++)
			{
				SteamNetworking.SendP2PPacket(playerList[i].SteamId, bytes, (uint)bytes.Length, EP2PSend.k_EP2PSendReliable, 0);
			}
			if(IsInPlayerList(clientId) == null)
			{
				playerList.Add(new netvrkPlayer(clientId, false, false));
			}
			yield return null;
		}

		private IEnumerator ConnectionResponse(InternalData internalData)
		{
			CancelInvoke("ConnectionFail");
			masterClient = new netvrkPlayer(internalData.remoteId, false, true);
			isConnected = true;

			if(IsInPlayerList(masterClient.SteamId) == null)
			{
				playerList.Add(masterClient);
			}
			if (connectSuccess != null)
            {
                connectSuccess();
            }
			StartCoroutine("TickLoop");
			yield return null;
		}

		private IEnumerator PlayerJoin(InternalData internalData)
		{
			netvrkPlayer newPlayer = new netvrkPlayer(new CSteamID((ulong)internalData.data[0]), false, false);
			playerList.Add(newPlayer);
			
			if (playerJoin != null)
            {
                playerJoin(newPlayer);
            }
			yield return null;
		}

		private IEnumerator PlayerDisconnect(InternalData internalData)
		{
			netvrkPlayer player = IsInPlayerList(new CSteamID((ulong)internalData.data[0]));
			if(player != null)
			{
				playerList.Remove(player);
				if (playerDisconnect != null)
				{
					playerDisconnect(player);
				}
			}
			yield return null;
		}

		private IEnumerator Tick(InternalData internalData)
		{
			SendInternalRpc(internalData.remoteId, InternalMethod.Tock);
			yield return null;
		}

		private IEnumerator Tock(InternalData internalData)
		{
			if(isMasterClient)
			{
				for(int i = 0; i < playerList.Count; i++)
				{
					playerList[i].tick = true;
				}
			}
			else
			{
				masterClient.tick = true;
			}
			yield return null;
		}

		private void ConnectionFail()
		{
			if (connectFail != null)
            {
                connectFail();
            }
		}

		private IEnumerator TickLoop()
		{
			while(true)
			{
				if(isMasterClient)
				{
					for(int i = 0; i < playerList.Count; i++)
					{
						if(playerList[i].tick)
						{
							playerList[i].tick = false;
							SendInternalRpc(playerList[i].SteamId, InternalMethod.Tick);
						}
						else
						{
							if(IsInPlayerList(masterClient.SteamId) != null)
							{
								playerList.Remove(masterClient);
							}
							for(int j = 0; j < playerList.Count; j++)
							{
								SendInternalRpc(playerList[j].SteamId, InternalMethod.PlayerDisconnect);
							}
						}
					}
				}
				else
				{
					if(masterClient.tick)
					{
						masterClient.tick = false;
						SendInternalRpc(masterClient.SteamId, InternalMethod.Tick);
					}
					else
					{
						if(disconnect != null)
						{
							disconnect();
						}
						Disconnect();
					}
				}
				yield return new WaitForSeconds(10);
			}
		}

		private static void SendInternalRpc(CSteamID friendSteamId, InternalMethod intMethod,  object[] data = null)
		{
			byte[] bytes = netvrkSerialization.SerializeInternal((byte)intMethod, data);
			SteamNetworking.SendP2PPacket(friendSteamId, bytes, (uint)bytes.Length, EP2PSend.k_EP2PSendReliable, 0);
		}
	}
}
