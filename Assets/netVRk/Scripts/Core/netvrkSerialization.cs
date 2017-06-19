﻿namespace netvrk
{
	using System;
	using System.IO;
	using System.Collections.Generic;
	using System.Text;
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
		Color,
		Color32,
	}

	public class netvrkSerialization : MonoBehaviour
	{
		public struct unpackOutput
		{
			public ushort objectId;
			public byte methodId;
			public netvrkType[] dataType;
			public object[] data;
		}
		public static byte[] PackData(ushort objId, byte methodId, object[] data)
		{
			List<byte[]> buffer = new List<byte[]>();
			netvrkType[] type = null;
			string typeName = "null";
			int byteSize = 0;

			if(data != null)
			{
				type = new netvrkType[data.Length];
				for(int i = 0; i < data.Length; i++)
				{
					type[i] = netvrkType.None;
					byte[] tmpBuffer;

					if(data[i] != null)
					{
						typeName = data[i].GetType().Name;
					}

					switch(typeName)
					{
						case "Byte":
							type[i] = netvrkType.Byte;
							tmpBuffer = BitConverter.GetBytes((byte)data[i]);
							break;
						case "Boolean":
							type[i] = netvrkType.Bool;
							tmpBuffer = BitConverter.GetBytes((bool)data[i]);
							break;
						case "Int16":
							type[i] = netvrkType.Short;
							tmpBuffer = BitConverter.GetBytes((short)data[i]);
							break;
						case "UInt16":
							type[i] = netvrkType.UShort;
							tmpBuffer = BitConverter.GetBytes((ushort)data[i]);
							break;
						case "Int32":
							type[i] = netvrkType.Int;
							tmpBuffer = BitConverter.GetBytes((int)data[i]);
							break;
						case "UInt32":
							type[i] = netvrkType.UInt;
							tmpBuffer = BitConverter.GetBytes((uint)data[i]);
							break;
						case "Int64":
							type[i] = netvrkType.Long;
							tmpBuffer = BitConverter.GetBytes((long)data[i]);
							break;
						case "UInt64":
							type[i] = netvrkType.ULong;
							tmpBuffer = BitConverter.GetBytes((ulong)data[i]);
							break;
						case "Single":
							type[i] = netvrkType.Float;
							tmpBuffer = BitConverter.GetBytes((float)data[i]);
							break;
						case "Double":
							type[i] = netvrkType.Double;
							tmpBuffer = BitConverter.GetBytes((double)data[i]);
							break;
						case "String":
							type[i] = netvrkType.String;
							string myString = (string)(data[i]);
							int len = Encoding.UTF8.GetByteCount(myString);
							tmpBuffer = new byte[len + 2];
							Buffer.BlockCopy(BitConverter.GetBytes((short)len), 0, tmpBuffer, 0, 2);
							Encoding.UTF8.GetBytes(myString, 0, myString.Length, tmpBuffer, 2);
							break;
						case "Vector2":
							type[i] = netvrkType.Vector2;
							tmpBuffer = SerializeVector2((Vector2)data[i]);
							break;
						case "Vector3":
							type[i] = netvrkType.Vector3;
							tmpBuffer = SerializeVector3((Vector3)data[i]);
							break;
						case "Vector4":
							type[i] = netvrkType.Vector4;
							tmpBuffer = SerializeVector4((Vector4)data[i]);
							break;
						case "Byte[]":
							type[i] = netvrkType.ByteArray;
							short len2 = (short)((byte[])data[i]).Length;
							tmpBuffer = new byte[len2 + 2];
							Buffer.BlockCopy(BitConverter.GetBytes(len2), 0, tmpBuffer, 0, 2);
							Buffer.BlockCopy((byte[])data[i], 0, tmpBuffer, 2, len2);
							break;
						case "Color":
							type[i] = netvrkType.Color;
							tmpBuffer = SerializeColor((Color)data[i]);
							break;
						case "Color32":
							type[i] = netvrkType.Color32;
							tmpBuffer = SerializeColor32((Color32)data[i]);
							break;
						default:
							tmpBuffer = new byte[0];
							break;
					}
					buffer.Add(tmpBuffer);
					byteSize += tmpBuffer.Length + 1;
				}
			}

			byte[] bytes = new byte[byteSize + 3];

			using (MemoryStream memoryStream = new MemoryStream(bytes))
			{
				BinaryWriter bw = new BinaryWriter(memoryStream);
				bw.Write((short)objId);
				bw.Write((byte)methodId);
				if(data != null)
				{
					for(int i = 0; i < data.Length; i++)
					{
						bw.Write((byte)type[i]);
						bw.Write(buffer[i]);
					}
				}
			}
			return bytes;
		}

		public static unpackOutput UnpackData(byte[] buffer, CSteamID remoteId)
		{
			List<object> data = new List<object>();
			List<netvrkType> type = new List<netvrkType>();
			unpackOutput output = new unpackOutput();
			using (MemoryStream memoryStream = new MemoryStream(buffer))
			{
				BinaryReader br = new BinaryReader(memoryStream);
				output.objectId = br.ReadUInt16();
				output.methodId = br.ReadByte();
				int i = 0;
				
				while(memoryStream.Position < memoryStream.Length)
				{
					type.Add((netvrkType)br.ReadByte());
					object tmpData = null;
					
					switch(type[i])
					{
						case netvrkType.Byte:
							tmpData = br.ReadByte();
							break;
						case netvrkType.Bool:
							tmpData = br.ReadBoolean();
							break;
						case netvrkType.Short:
							tmpData = br.ReadInt16();
							break;
						case netvrkType.UShort:
							tmpData = br.ReadUInt16();
							break;
						case netvrkType.Int:
							tmpData = br.ReadInt32();
							break;
						case netvrkType.UInt:
							tmpData = br.ReadUInt32();
							break;
						case netvrkType.Long:
							tmpData = br.ReadInt64();
							break;
						case netvrkType.ULong:
							tmpData = br.ReadUInt64();
							break;
						case netvrkType.Float:
							tmpData = br.ReadSingle();
							break;
						case netvrkType.Double:
							tmpData = br.ReadDouble();
							break;
						case netvrkType.String:
							short len = br.ReadInt16();
							byte[] tmpBuffer = new byte[len];
							Buffer.BlockCopy(buffer, (int)memoryStream.Position, tmpBuffer, 0, len);
							tmpData = Encoding.UTF8.GetString(tmpBuffer);
							br.ReadBytes(len);
							break;
						case netvrkType.None:
							break;
						case netvrkType.Vector2:
							tmpData = DeserializeVector2(br.ReadBytes(8));
							break;
						case netvrkType.Vector3:
							tmpData = DeserializeVector3(br.ReadBytes(12));
							break;
						case netvrkType.Vector4:
							tmpData = DeserializeVector4(br.ReadBytes(16));
							break;
						case netvrkType.Color:
							tmpData = DeserializeColor(br.ReadBytes(12));
							break;
						case netvrkType.Color32:
							tmpData = DeserializeColor32(br.ReadBytes(16));
							break;
						case netvrkType.ByteArray:
							short len2 = br.ReadInt16();
							tmpData = br.ReadBytes((int)len2);
							break;
					}
					i++;
					data.Add(tmpData);
				}
				output.data = data.ToArray();
				output.dataType = type.ToArray();
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

		public static byte[] SerializeColor(Color color)
		{
			byte[] buffer = new byte[12];
			
			Buffer.BlockCopy(BitConverter.GetBytes(color.r), 0, buffer, 0, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(color.g), 0, buffer, 4, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(color.b), 0, buffer, 8, 4);

			return buffer;
		}

		public static byte[] SerializeColor32(Color32 color)
		{
			byte[] buffer = new byte[4];
			
			buffer[0] = color.r;
			buffer[1] = color.g;
			buffer[2] = color.b;
			buffer[3] = color.a;

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

		public static Color DeserializeColor(byte[] data)
		{
			Color color = new Color();
			using (MemoryStream memoryStream = new MemoryStream(data))
			{
				BinaryReader br = new BinaryReader(memoryStream);
				color.r = br.ReadSingle();
				color.g = br.ReadSingle();
				color.b = br.ReadSingle();
			}
			return color;
		}

		public static Color32 DeserializeColor32(byte[] data)
		{
			Color32 color = new Color32();
			using (MemoryStream memoryStream = new MemoryStream(data))
			{
				BinaryReader br = new BinaryReader(memoryStream);
				color.r = br.ReadByte();
				color.g = br.ReadByte();
				color.b = br.ReadByte();
				color.a = br.ReadByte();
			}
			return color;
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