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
	public delegate void netVRkEventHandler();

	public enum netvrkTargets
	{
		All,
		Other
	}

	public class netvrkManager : MonoBehaviour
	{
		private ushort maxId = 1;
		private static netvrkManager instance;
		private static Dictionary<ushort, ObjData> objList = new Dictionary<ushort, ObjData>();
		private static List<netvrkPlayer> playerList = new List<netvrkPlayer>();
		private static netvrkPlayer serverPlayer;
		private static bool isServer = false;
		private static bool isConnected = false;
		private Callback<P2PSessionRequest_t> p2PSessionRequestCallback;

		public static event netVRkEventHandler connectSuccess;
		public static event netVRkEventHandler connectFail;
		public static event netVRkEventHandler disconnect;
		public static event netVRkPlayerEventHandler playerJoin;
		public static event netVRkPlayerEventHandler playerDisconnect;

		public enum InternalMethod
		{
			Instantiate,
			ConnectRequest,
			ConnectResponse,
			PlayerJoin,
			PlayerDisconnect
		}

		private struct ObjData
		{
			public netvrkView netObj;
			public List<string> methods;
		}

		private struct InternalData
		{
			public object data;
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
			data.netObj = obj;

			foreach (MonoBehaviour script in scripts)
			{
				MethodInfo[] objectFields = script.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
				for (int i = 0; i < objectFields.Length; i++)
				{
					netvrkRpc attribute = Attribute.GetCustomAttribute(objectFields[i], typeof(netvrkRpc)) as netvrkRpc;
					if(attribute != null)
					{
						char[] splitters = {' ', '('};
						string[] strings = objectFields[i].ToString().Split(splitters);
						data.methods.Add(strings[1]);
					}
				}
			}
			objList.Add(id, data);
		}

		public static void SendRpc(ushort objId, string method, object data, netvrkTargets targets, int channel = 0)
		{
			int methodId = GetObjMethodId(objId, method);
			if(methodId < 0)
			{
				Debug.LogError("netVRk: Rpc method: " + method + " not found!");
				return;
			}

			byte[] bytes = netvrkSerialization.PackData(objId, (byte)methodId, data);
			for(int i = 0; i < playerList.Count; i++)
			{
				SteamNetworking.SendP2PPacket(playerList[i].steamId, bytes, (uint)bytes.Length, EP2PSend.k_EP2PSendReliable, channel);
			}
			if(targets == netvrkTargets.All)
			{
				GameObject go = objList[objId].netObj.gameObject;
				go.SendMessage(method, data, SendMessageOptions.RequireReceiver);
			}
		}

		public static void SendRpc(ushort objId, string method, object data, netvrkPlayer player, int channel = 0)
		{
			int methodId = GetObjMethodId(objId, method);
			if(methodId < 0)
			{
				Debug.LogError("netVRk: Rpc method: " + method + " not found!");
				return;
			}

			byte[] bytes = netvrkSerialization.PackData(objId, (byte)methodId, data);
			SteamNetworking.SendP2PPacket(player.steamId, bytes, (uint)bytes.Length, EP2PSend.k_EP2PSendReliable, channel);
		}

		public static List<netvrkPlayer> GetPlayerList()
		{
			return playerList;
		}

		public static void Instantiate(string prefabName, Vector3 position, Quaternion rotation, int channel = 0, object data = null)
		{
			GameObject go = (GameObject)Resources.Load(prefabName);
			netvrkView view = go.GetComponent<netvrkView>();
			if(view == null)
			{
				Debug.LogError("netVRk: Can not instantiate object '" + prefabName + "' because its missing a netvtkView component!");
				return;
			}

			byte[] tmpBuffer = System.Text.Encoding.UTF8.GetBytes(prefabName);
			byte[] bytes = new byte[tmpBuffer.Length + 28];

			using (MemoryStream memoryStream = new MemoryStream(bytes))
			{
				BinaryWriter bw = new BinaryWriter(memoryStream);
				bw.Write((short)0);
				bw.Write((byte)InternalMethod.Instantiate);
				bw.Write((byte)netvrkType.Internal);
				bw.Write(netvrkSerialization.SerializeVector3(position));
				bw.Write(netvrkSerialization.SerializeVector3(rotation.eulerAngles));
				bw.Write(tmpBuffer);
				// TODO: Handle data
			}

			for(int i = 0; i < playerList.Count; i++)
			{
				SteamNetworking.SendP2PPacket(playerList[i].steamId, bytes, (uint)bytes.Length, EP2PSend.k_EP2PSendReliable, channel);
			}
			Instantiate(go, position, rotation);
		}

		public static bool IsServer()
		{
			return isServer;
		}

		public static bool IsConnected()
		{
			return isConnected;
		}

		public static netvrkPlayer GetServerPlayer()
		{
			return serverPlayer;
		}

		public static void CreateGame()
		{
			isServer = true;
			isConnected = true;
			serverPlayer = new netvrkPlayer(SteamUser.GetSteamID());
		}

		public static void JoinGame(string name)
		{
			int friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
			for (int i = 0; i < friendCount; ++i)
			{
				CSteamID friendSteamId = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
				string friendName = SteamFriends.GetFriendPersonaName(friendSteamId);

				if(friendName == name)
				{
					SendInternalRpc(friendSteamId, InternalMethod.ConnectRequest);
				}
				instance.Invoke("ConnectionFail", 5);
			}
		}

		public static void Disconnect()
		{
			isServer = false;
			isConnected = false;

			for(int i = 0; i < playerList.Count; i++)
			{
				SendInternalRpc(playerList[i].steamId, InternalMethod.PlayerDisconnect, SteamUser.GetSteamID().m_SteamID);
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
			objList.Add(0, data);
		}

		private void Update()
		{
			if (!Application.isPlaying)
			{
				return;
			}

			uint size;
			while (SteamNetworking.IsP2PPacketAvailable(out size))
			{
				byte[] buffer = new byte[size];
				uint bytesRead;
				CSteamID remoteId;
 
				if (SteamNetworking.ReadP2PPacket(buffer, size, out bytesRead, out remoteId))
				{
					UnpackData(buffer, remoteId);
				}
			}
		}

		private void OnP2PSessionRequest(P2PSessionRequest_t request)
		{
			CSteamID clientId = request.m_steamIDRemote;
			if (IsExpectingClient(clientId))
			{
				SteamNetworking.AcceptP2PSessionWithUser(clientId);
			}
			else
			{
				Debug.LogWarning("Unexpected session request from " + clientId);
			}
		}

		private bool IsInPlayerList(CSteamID clientId)
		{
			for(int i = 0; i < playerList.Count; i++)
			{
				if(playerList[i].steamId == clientId)
				{
					return true;
				}
			}
			return false;
		}

		private bool IsExpectingClient(CSteamID clientId)
		{
			return SteamFriends.HasFriend(clientId, EFriendFlags.k_EFriendFlagAll) && isConnected;
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

		private void UnpackData(byte[] buffer, CSteamID remoteId)
		{
			netvrkSerialization.unpackOutput output = netvrkSerialization.UnpackData(buffer, remoteId);

			string methodName = objList[output.objectId].methods[output.methodId];
			if(output.objectId > 0)
			{
				GameObject go = objList[output.objectId].netObj.gameObject;
				go.SendMessage(methodName, output.data, SendMessageOptions.RequireReceiver);
			}
			else
			{
				InternalData intData = new InternalData();
				intData.remoteId = remoteId;
				if(output.dataType == netvrkType.Internal)
				{
					byte[] tmpBuffer3 = new byte[33];
					Buffer.BlockCopy(buffer, 4, tmpBuffer3, 0, buffer.Length - 4);
					intData.data = tmpBuffer3;
				}
				else
				{
					intData.data = output.data;
				}
				
				StartCoroutine(methodName, intData);
			}
		}

		private IEnumerator InstantiatePrefab(InternalData internalData)
		{
			byte[] buffer = (byte[])internalData.data;
			byte[] tmpBuffer = new byte[12];
			string prefabName;

			Buffer.BlockCopy(buffer, 0, tmpBuffer, 0, 12);
			Vector3 position = netvrkSerialization.DeserializeVector3(tmpBuffer);
			Buffer.BlockCopy(buffer, 12, tmpBuffer, 0, 12);
			Quaternion rotation = Quaternion.Euler(netvrkSerialization.DeserializeVector3(tmpBuffer));

			prefabName = System.Text.Encoding.UTF8.GetString(buffer, 24, buffer.Length - 24);
			// TODO: Handle data

			GameObject go = (GameObject)Resources.Load(prefabName);
			go.GetComponent<netvrkView>().isMine = false;
			Instantiate(go, position, rotation);

			yield return null;
		}

		private IEnumerator ConnectionRequest(InternalData internalData)
		{
			CSteamID clientId = internalData.remoteId;

			SendInternalRpc(clientId, InternalMethod.ConnectResponse);

			for(int i = 0; i < playerList.Count; i++)
			{
				SendInternalRpc(playerList[i].steamId, InternalMethod.PlayerJoin, clientId.m_SteamID);
			}
			if(!IsInPlayerList(clientId))
			{
				playerList.Add(new netvrkPlayer(clientId));
			}
			yield return null;
		}

		private IEnumerator ConnectionResponse(InternalData internalData)
		{
			CancelInvoke("ConnectionFail");
			serverPlayer = new netvrkPlayer(internalData.remoteId);
			isConnected = true;

			if(!IsInPlayerList(serverPlayer.steamId))
			{
				playerList.Add(serverPlayer);
			}
			if (connectSuccess != null)
            {
                connectSuccess();
            }
			yield return null;
		}

		private IEnumerator PlayerJoin(InternalData internalData)
		{
			netvrkPlayer newPlayer = new netvrkPlayer(new CSteamID((ulong)internalData.data));
			playerList.Add(newPlayer);
			
			if (playerJoin != null)
            {
                playerJoin(newPlayer);
            }
			yield return null;
		}

		private IEnumerator PlayerDisconnect(InternalData internalData)
		{
			netvrkPlayer newPlayer = new netvrkPlayer(new CSteamID((ulong)internalData.data));
			if (playerDisconnect != null)
            {
                playerDisconnect(newPlayer);
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

		private static void SendInternalRpc(CSteamID friendSteamId, InternalMethod intMethod,  object data = null)
		{
			byte[] bytes = netvrkSerialization.PackData(0, (byte)intMethod, data);
			SteamNetworking.SendP2PPacket(friendSteamId, bytes, (uint)bytes.Length, EP2PSend.k_EP2PSendReliable, 0);
		}
	}
}
