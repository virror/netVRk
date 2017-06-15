namespace netvrk
{
	using Steamworks;
	public class netvrkPlayer
	{
		public string name;
		public CSteamID steamId;

		public netvrkPlayer(CSteamID playerId)
		{
			name = SteamFriends.GetFriendPersonaName(playerId);
			steamId = playerId;
		}
	}
}
