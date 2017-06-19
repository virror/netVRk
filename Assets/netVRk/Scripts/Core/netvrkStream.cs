namespace netvrk
{
	using UnityEngine;
	using System.IO;
	using System;

	public class netvrkStream
	{
		private MemoryStream memoryStream;
		private BinaryWriter bw;
		private BinaryReader br;
		private ushort objId;
		
		public netvrkStream(ushort objId)
		{
			memoryStream = new MemoryStream();
			bw = new BinaryWriter(memoryStream);
			this.objId = objId;
		}

		public netvrkStream(byte[] data)
		{
			memoryStream = new MemoryStream(data);
			br = new BinaryReader(memoryStream);
		}

		public ushort ObjId
			{ get{ return objId; }}

		public void Write(object obj)
		{
			byte[] bytes = netvrkSerialization.SerializeSync(obj);
			bw.Write(bytes);
		}

		public object Read(Type type)
		{
			object obj = netvrkSerialization.UnserializeSync(br, type);
			return obj;
		}

		public byte[] GetStreamData()
		{
			return memoryStream.ToArray();
		}
	}
}
