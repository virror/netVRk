namespace netvrk
{
	using System;
	using System.IO;
	using UnityEngine;
	using Steamworks;

	public enum netvrkType
	{
		None,
		Byte,
		Bool,
		Short,
		UShort,
		Int,
		UInt,
		Long,
		ULong,
		Float,
		Double,
		String,
		Vector2,
		Vector3,
		Vector4,
		ByteArray,
		Internal,
	}

	public class netvrkSerialization : MonoBehaviour
	{
		public struct unpackOutput
		{
			public ushort objectId;
			public byte methodId;
			public netvrkType dataType;
			public object data;
		}
		public static byte[] PackData(ushort objId, byte methodId, object data)
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
					buffer = BitConverter.GetBytes((byte)data);
					break;
				case "Boolean":
					type = netvrkType.Bool;
					buffer = BitConverter.GetBytes((bool)data);
					break;
				case "Int16":
					type = netvrkType.Short;
					buffer = BitConverter.GetBytes((short)data);
					break;
				case "UInt16":
					type = netvrkType.UShort;
					buffer = BitConverter.GetBytes((ushort)data);
					break;
				case "Int32":
					type = netvrkType.Int;
					buffer = BitConverter.GetBytes((int)data);
					break;
				case "UInt32":
					type = netvrkType.UInt;
					buffer = BitConverter.GetBytes((uint)data);
					break;
				case "Int64":
					type = netvrkType.Long;
					buffer = BitConverter.GetBytes((long)data);
					break;
				case "UInt64":
					type = netvrkType.ULong;
					buffer = BitConverter.GetBytes((ulong)data);
					break;
				case "Single":
					type = netvrkType.Float;
					buffer = BitConverter.GetBytes((float)data);
					break;
				case "Double":
					type = netvrkType.Double;
					buffer = BitConverter.GetBytes((double)data);
					break;
				case "String":
					type = netvrkType.String;
					buffer = System.Text.Encoding.UTF8.GetBytes((string)data);
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

		public static unpackOutput UnpackData(byte[] buffer, CSteamID remoteId)
		{
			unpackOutput output = new unpackOutput();
			using (MemoryStream memoryStream = new MemoryStream(buffer))
			{
				BinaryReader br = new BinaryReader(memoryStream);
				output.objectId = br.ReadUInt16();
				output.methodId = br.ReadByte();
				output.dataType = (netvrkType)memoryStream.ReadByte();
				object data = null;

				switch(output.dataType)
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
					case netvrkType.UShort:
						data = br.ReadUInt16();
						break;
					case netvrkType.Int:
						data = br.ReadInt32();
						break;
					case netvrkType.UInt:
						data = br.ReadUInt32();
						break;
					case netvrkType.Long:
						data = br.ReadInt64();
						break;
					case netvrkType.ULong:
						data = br.ReadUInt64();
						break;
					case netvrkType.Float:
						data = br.ReadSingle();
						break;
					case netvrkType.Double:
						data = br.ReadDouble();
						break;
					case netvrkType.String:
						data = System.Text.Encoding.UTF8.GetString(buffer, 4, buffer.Length - 4);
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
				output.data = data;
			}
			return output;
		}

		public static byte[] SerializeVector2(Vector2 vector)
		{
			byte[] buffer = new byte[8];
			
			Buffer.BlockCopy(BitConverter.GetBytes(vector.x), 0, buffer, 0, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(vector.y), 0, buffer, 4, 4);

			return buffer;
		}

		public static byte[] SerializeVector3(Vector3 vector)
		{
			byte[] buffer = new byte[12];
			
			Buffer.BlockCopy(BitConverter.GetBytes(vector.x), 0, buffer, 0, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(vector.y), 0, buffer, 4, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(vector.z), 0, buffer, 8, 4);

			return buffer;
		}

		public static byte[] SerializeVector4(Vector4 vector)
		{
			byte[] buffer = new byte[16];
			
			Buffer.BlockCopy(BitConverter.GetBytes(vector.x), 0, buffer, 0, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(vector.y), 0, buffer, 4, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(vector.z), 0, buffer, 8, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(vector.w), 0, buffer, 12, 4);

			return buffer;
		}

		public static Vector2 DeserializeVector2(byte[] data)
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

		public static Vector3 DeserializeVector3(byte[] data)
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

		public static Vector4 DeserializeVector4(byte[] data)
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