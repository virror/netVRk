using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using netvrk;

public class Menu : MonoBehaviour
{
	private InputField steamInput;
	private Text debugText;
	netvrkView netView;

	private void Awake()
	{
		steamInput = transform.Find("SteamInput").GetComponent<InputField>();
		debugText = transform.Find("Text").GetComponent<Text>();
		netView = GetComponent<netvrkView>();
		netvrkManager.connectSuccess += new netVRkEventHandler(OnConnectSuccess);
		netvrkManager.connectFail += new netVRkEventHandler(OnConnectFail);
		netvrkManager.disconnect += new netVRkEventHandler(OnDisconnect);
		netvrkManager.playerJoin += new netVRkPlayerEventHandler(OnPlayerJoin);
		netvrkManager.playerDisconnect += new netVRkPlayerEventHandler(OnPlayerDisconnect);
	}

	public void CreateGame()
	{
		netvrkManager.CreateGame();
	}

	public void JoinGame()
	{
		netvrkManager.JoinGame(steamInput.text);
	}

	public void Quit()
	{
		Application.Quit();
	}

	public void Disconnect()
	{
		netvrkManager.Disconnect();
	}

	public void Rpc()
	{
		netView.Rpc("TestRpc", netvrkTargets.All, "Rpc test!");
	}

	public void Instantiate()
	{
		netvrkManager.Instantiate("NetPlayer", new Vector3(2, 1, 3), Quaternion.identity);
	}

	[netvrkRpc]
	public void TestRpc(string apa)
	{
		debugText.text = "Rpc: " + apa;
	}

	private void OnConnectSuccess()
	{
		debugText.text = "Connection successful.";
	}

	private void OnConnectFail()
	{
		debugText.text = "Connection failed.";
	}

	private void OnDisconnect()
	{
		debugText.text = "Disconnected.";
	}

	private void OnPlayerJoin(netvrkPlayer player)
	{
		debugText.text = "Player joined: " + player.name;
	}

	private void OnPlayerDisconnect(netvrkPlayer player)
	{
		debugText.text = "Player disconnected: " + player.name;
	}
}
