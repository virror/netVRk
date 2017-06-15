namespace netvrk
{
	using UnityEngine;
	using System;
	using System.IO;
	using System.Reflection;
	using System.Collections;
	using System.Collections.Generic;
	using Steamworks;

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
		public static event netVRkEventHandler playerJoin;
		public static event netVRkEventHandler playerDisconnect;

		private enum netvrkType
		{
			None,
			Byte,
			Bool,
			Short,
			Int,
			Long,
			Float,
			Double,
			String,
			Vector2,
			Vector3,
			Vector4,
			ByteArray,
			Internal,
		}

		private enum InternalMethod
		{
			Instantiate,
			ConnectRequest,
			ConnectResponse,
			NewPlayer
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

			byte[] bytes = PackData(objId, (byte)methodId, data);
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

			byte[] bytes = PackData(objId, (byte)methodId, data);
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
			byte[] bytes = new byte[tmpBuffer.Length + 24];

			using (MemoryStream memoryStream = new MemoryStream(bytes))
			{
				BinaryWriter bw = new BinaryWriter(memoryStream);
				bw.Write((short)0);
				bw.Write((byte)InternalMethod.Instantiate);
				bw.Write((byte)netvrkType.Internal);
				bw.Write(SerializeVector3(position));
				bw.Write(SerializeVector3(rotation.eulerAngles));
				bw.Write(tmpBuffer);
				// TODO: Handle data
				//bw.Write(data);
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
			}
		}

		public static void Disconnect()
		{
			isServer = false;
			isConnected = false;
			// TODO: Send disconnect messages to other clients
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
			data.methods.Add("NewPlayer");
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
					UnpackData(buffer, bytesRead, remoteId);
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
					return false;
				}
			}
			return true;
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
				case netvrkSendMethod.ReliableBuffered:
					return EP2PSend.k_EP2PSendReliableWithBuffering;
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

		private static byte[] PackData(ushort objId, byte methodId, object data)
		{
			netvrkType type = netvrkType.None;
			byte[] buffer;
			string typeName = "null";

			if(data != null)
			{
				typeName = data.GetType().Name;
			}

			switch(typeName)
			{
				case "Byte":
					type = netvrkType.Byte;
					buffer = new byte[1];
					buffer = BitConverter.GetBytes((byte)data);
					break;
				case "Boolean":
					type = netvrkType.Bool;
					buffer = new byte[1];
					buffer = BitConverter.GetBytes((bool)data);
					break;
				case "Int16":
					type = netvrkType.Short;
					buffer = new byte[2];
					buffer = BitConverter.GetBytes((short)data);
					break;
				case "Int32":
					type = netvrkType.Int;
					buffer = new byte[4];
					buffer = BitConverter.GetBytes((int)data);
					break;
				case "Int64":
					type = netvrkType.Long;
					buffer = new byte[8];
					buffer = BitConverter.GetBytes((long)data);
					break;
				case "Single":
					type = netvrkType.Float;
					buffer = new byte[4];
					buffer = BitConverter.GetBytes((float)data);
					break;
				case "Double":
					type = netvrkType.Double;
					buffer = new byte[8];
					buffer = BitConverter.GetBytes((double)data);
					break;
				case "String":
					type = netvrkType.String;
					byte[] tmpBuffer = System.Text.Encoding.UTF8.GetBytes((string)data);
					buffer = new byte[tmpBuffer.Length];
					Buffer.BlockCopy(tmpBuffer, 0, buffer, 0, tmpBuffer.Length);
					break;
				case "Vector2":
					type = netvrkType.Vector2;
					buffer = SerializeVector2((Vector2)data);
					break;
				case "Vector3":
					type = netvrkType.Vector3;
					buffer = SerializeVector3((Vector3)data);
					break;
				case "Vector4":
					type = netvrkType.Vector4;
					buffer = SerializeVector4((Vector4)data);
					break;
				case "Byte[]":
					type = netvrkType.ByteArray;
					buffer = (byte[])data;
					break;
				default:
					buffer = new byte[0];
					break;
			}

			int byteSize = buffer.Length;
			byte[] bytes = new byte[byteSize + 4];

			using (MemoryStream memoryStream = new MemoryStream(bytes))
			{
				BinaryWriter bw = new BinaryWriter(memoryStream);
				bw.Write((short)objId);
				bw.Write((byte)methodId);
				bw.Write((byte)type);
				bw.Write(buffer);
			}
			return bytes;
		}

		private void UnpackData(byte[] buffer, uint size, CSteamID remoteId)
		{
			using (MemoryStream memoryStream = new MemoryStream(buffer))
			{
				BinaryReader br = new BinaryReader(memoryStream);
				ushort objId = br.ReadUInt16();
				int methodId = br.ReadByte();
				netvrkType dataType = (netvrkType)memoryStream.ReadByte();
				string methodName = objList[objId].methods[methodId];
				object data = null;

				switch(dataType)
				{
					case netvrkType.Byte:
						data = br.ReadByte();
						break;
					case netvrkType.Bool:
						data = br.ReadBoolean();
						break;
					case netvrkType.Short:
						data = br.ReadInt16();
						break;
					case netvrkType.Int:
						data = br.ReadInt32();
						break;
					case netvrkType.Long:
						data = br.ReadInt64();
						break;
					case netvrkType.Float:
						data = br.ReadSingle();
						break;
					case netvrkType.Double:
						data = br.ReadDouble();
						break;
					case netvrkType.String:
						data = System.Text.Encoding.UTF8.GetString(buffer, 4, (int)size - 4);
						break;
					case netvrkType.None:
						break;
					case netvrkType.Vector2:
						byte[] tmpBuffer = new byte[8];
						Buffer.BlockCopy(buffer, 4, tmpBuffer, 0, 8);
						data = DeserializeVector2(tmpBuffer);
						break;
					case netvrkType.Vector3:
						byte[] tmpBuffer2 = new byte[12];
						Buffer.BlockCopy(buffer, 4, tmpBuffer2, 0, 12);
						data = DeserializeVector3(tmpBuffer2);
						break;
					case netvrkType.Vector4:
						byte[] tmpBuffer4 = new byte[16];
						Buffer.BlockCopy(buffer, 4, tmpBuffer4, 0, 16);
						data = DeserializeVector4(tmpBuffer4);
						break;
					case netvrkType.ByteArray:
						data = br.ReadBytes((int)memoryStream.Length);
						break;
				}
				if(objId > 0)
				{
					GameObject go = objList[objId].netObj.gameObject;
					go.SendMessage(methodName, data, SendMessageOptions.RequireReceiver);
				}
				else
				{
					InternalData intData = new InternalData();
					intData.remoteId = remoteId;
					if(dataType == netvrkType.Internal)
					{
						byte[] tmpBuffer3 = new byte[33];
						Buffer.BlockCopy(buffer, 4, tmpBuffer3, 0, (int)size - 4);
						intData.data = tmpBuffer3;
					}
					else
					{
						intData.data = data;
					}
					
					StartCoroutine(methodName, intData);
				}
			}
		}

		private IEnumerator InstantiatePrefab(InternalData internalData)
		{
			object data = internalData.data;
			byte[] buffer = (byte[])data;
			byte[] tmpBuffer = new byte[12];
			string prefabName;

			Buffer.BlockCopy(buffer, 0, tmpBuffer, 0, 12);
			Vector3 position = DeserializeVector3(tmpBuffer);
			Buffer.BlockCopy(buffer, 12, tmpBuffer, 0, 12);
			Quaternion rotation = Quaternion.Euler(DeserializeVector3(tmpBuffer));

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
			byte[] buffer = BitConverter.GetBytes(clientId.m_SteamID);
			for(int i = 0; i < playerList.Count; i++)
			{
				SendInternalRpc(playerList[i].steamId, InternalMethod.NewPlayer, netvrkType.Long, buffer);
			}
			if(!IsInPlayerList(clientId))
			{
				playerList.Add(new netvrkPlayer(clientId));
			}
			yield return null;
		}

		private IEnumerator ConnectionResponse(InternalData internalData)
		{
			serverPlayer = new netvrkPlayer(internalData.remoteId);
			if (connectSuccess != null)
            {
                connectSuccess();
            }
			yield return null;
		}

		private IEnumerator NewPlayer(InternalData internalData)
		{
			byte[] clientId = (byte[])internalData.data;
			CSteamID newPlayer = new CSteamID(BitConverter.ToUInt64(clientId, 0));
			if (playerJoin != null)
            {
                playerJoin();
            }
			yield return null;
		}

		private static void SendInternalRpc(CSteamID friendSteamId, InternalMethod intMethod, 
										netvrkType dataType = netvrkType.None, byte[] data = null)
		{
			byte[] bytes = new byte[4];

			using (MemoryStream memoryStream = new MemoryStream(bytes))
			{
				BinaryWriter bw = new BinaryWriter(memoryStream);
				bw.Write((short)0);
				bw.Write((byte)intMethod);
				bw.Write((byte)dataType);
				bw.Write(data);
				SteamNetworking.SendP2PPacket(friendSteamId, bytes, (uint)bytes.Length, EP2PSend.k_EP2PSendReliable, 0);
			}
		}

		private static byte[] SerializeVector2(Vector2 vector)
		{
			byte[] buffer = new byte[8];
			
			Buffer.BlockCopy(BitConverter.GetBytes(vector.x), 0, buffer, 0, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(vector.y), 0, buffer, 4, 4);

			return buffer;
		}

		private static byte[] SerializeVector3(Vector3 vector)
		{
			byte[] buffer = new byte[12];
			
			Buffer.BlockCopy(BitConverter.GetBytes(vector.x), 0, buffer, 0, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(vector.y), 0, buffer, 4, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(vector.z), 0, buffer, 8, 4);

			return buffer;
		}

		private static byte[] SerializeVector4(Vector4 vector)
		{
			byte[] buffer = new byte[16];
			
			Buffer.BlockCopy(BitConverter.GetBytes(vector.x), 0, buffer, 0, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(vector.y), 0, buffer, 4, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(vector.z), 0, buffer, 8, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(vector.w), 0, buffer, 12, 4);

			return buffer;
		}

		private static Vector2 DeserializeVector2(byte[] data)
		{
			Vector2 vector;
			using (MemoryStream memoryStream = new MemoryStream(data))
			{
				BinaryReader br = new BinaryReader(memoryStream);
				vector.x = br.ReadSingle();
				vector.y = br.ReadSingle();
			}
			return vector;
		}

		private static Vector3 DeserializeVector3(byte[] data)
		{
			Vector3 vector;
			using (MemoryStream memoryStream = new MemoryStream(data))
			{
				BinaryReader br = new BinaryReader(memoryStream);
				vector.x = br.ReadSingle();
				vector.y = br.ReadSingle();
				vector.z = br.ReadSingle();
			}
			return vector;
		}

		private static Vector4 DeserializeVector4(byte[] data)
		{
			Vector4 vector;
			using (MemoryStream memoryStream = new MemoryStream(data))
			{
				BinaryReader br = new BinaryReader(memoryStream);
				vector.x = br.ReadSingle();
				vector.y = br.ReadSingle();
				vector.z = br.ReadSingle();
				vector.w = br.ReadSingle();
			}
			return vector;
		}
	}
}

//9 obj array
//11 array T
//12 Hashtable
//13 Dictionary<Object,Object>
//14 Dictionary<Object,V> 
//15 Dictionary<K,Object>
//16 Dictionary<K,V> 