namespace netvrk
{
	using Steamworks;
	using System;

	public class netvrkPlayer : IEquatable<netvrkPlayer>
	{
		public bool tick = true;

		private string name;
		private CSteamID steamId;
		private bool isLocal;
		private bool isMasterClient;

		public netvrkPlayer(CSteamID playerId, bool isLocal, bool isMasterClient)
		{
			name = SteamFriends.GetFriendPersonaName(playerId);
			steamId = playerId;
			this.isLocal = isLocal;
			this.isMasterClient = isMasterClient;
		}

		public string Name
		{ get{ return name; }}

		public CSteamID SteamId
		{ get{ return steamId; }}

		public bool IsLocal
		{ get{ return isLocal; }}

		public bool IsMasterClient
		{ get{ return isMasterClient; }}

		public bool Equals(netvrkPlayer other)
		{
			if(other == null)
			{
				return false;
			}
			return name == other.name && steamId.m_SteamID == other.steamId.m_SteamID;
		}
	}
}
