using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using netvrk;

public class Menu : MonoBehaviour
{
	private InputField steamInput;
	private Text debugText;
	private netvrkView netView;

	private void Awake()
	{
		steamInput = transform.Find("SteamInput").GetComponent<InputField>();
		debugText = transform.Find("Text").GetComponent<Text>();
		netView = GetComponent<netvrkView>();
		netvrkManager.connectSuccess += new netVRkConnectionHandler(OnConnectSuccess);
		netvrkManager.connectFail += new netVRkConnectionHandler(OnConnectFail);
		netvrkManager.disconnect += new netVRkConnectionHandler(OnDisconnect);
		netvrkManager.playerJoin += new netVRkPlayerEventHandler(OnPlayerJoin);
		netvrkManager.playerDisconnect += new netVRkPlayerEventHandler(OnPlayerDisconnect);
		netvrkManager.eventCall += new netVRkEventHandler(OnEventCall);
	}

	public void CreateGame()
	{
		netvrkManager.CreateGame(4);
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
		netView.Rpc("TestRpc", netvrkTargets.All, 0, new Color32(23, 54, 87, 02));
	}

	public void Sync()
	{
		netvrkStream stream = netView.GetStream();
		stream.Write(467);
		netView.WriteSyncStream(stream);
	}

	public void OnNetvrkReadSyncStream(netvrkStream stream)
	{
		Debug.Log(stream.Read(typeof(int)));
	}

	public void Instantiate()
	{
		netvrkManager.Instantiate("NetPlayer", new Vector3(2, 1, 3), Quaternion.identity, 0, "Apa");
	}

	public void Event()
	{
		netvrkManager.RaiseEvent(0, netvrkSendMethod.Reliable, "TestEvent");
	}

	[netvrkRpc]
	public void TestRpc(Color32 color)
	{
		debugText.text = "Rpc: " + color;
	}

	private void OnConnectSuccess()
	{
		SceneManager.LoadScene("Main");
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
		SceneManager.LoadScene("Main");
	}

	private void OnPlayerDisconnect(netvrkPlayer player)
	{
		debugText.text = "Player disconnected: " + player.Name;
	}

	private void OnEventCall(byte eventCode, object[] data, netvrkPlayer player)
	{
		debugText.text = "Event: " + data[0];
	}
}
