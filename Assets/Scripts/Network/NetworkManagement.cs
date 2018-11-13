﻿using Byn.Net;
using Navigation;
using PlayerManagement;
using SlideBall.Networking;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public delegate void NetworkEventHandler(byte[] data, ConnectionId id);
public delegate void ConnectionEventHandler(ConnectionId id);

public static class NetworkingState
{
    public static bool IsServer { get; set; }
}

public class NetworkManagement : ANetworkManagement
{
    public static ConnectionId SERVER_CONNECTION_ID = new ConnectionId(-1);
    public static ConnectionId INVALID_CONNECTION_ID = new ConnectionId(-100);

    private LatencySimulation latencySimulation;

    private enum State
    {
        IDLE,
        CONNECTED,
        SERVER
    }

    private State _stateCurrent;
    private State StateCurrent
    {
        get
        {
            return _stateCurrent;
        }
        set
        {
            _stateCurrent = value;
            switch (value)
            {
                case State.IDLE:
                    bufferedMessages = null;
                    NetworkingState.IsServer = false;
                    break;
                case State.CONNECTED:
                    bufferedMessages = null;
                    NetworkingState.IsServer = false;
                    break;
                case State.SERVER:
                    NetworkingState.IsServer = true;
                    bufferedMessages = new BufferedMessages(this, MyComponents.NetworkViewsManagement);
                    break;
                default:
                    break;
            }
        }
    }

    private bool currentlyPlaying = false;
    public override bool CurrentlyPlaying
    {
        get
        {
            return currentlyPlaying;
        }
        set
        {
            currentlyPlaying = value;
            RefreshRoomData();
        }
    }


    [HideInInspector]
    public string RoomName = null;

    /// <summary>
    /// The network interface.
    /// This can be native webrtc or the browser webrtc version.
    /// (Can also be the old or new unity network but this isn't part of this package)
    /// </summary>
    private IWebRtcNetwork mNetwork = null;

    /// <summary>
    /// True if the user opened an own room allowing incoming connections
    /// </summary>
    private bool IsServer
    {
        get
        {
            return _stateCurrent == State.SERVER;
        }
    }

    internal void LeaveRoom()
    {
        mNetwork.LeaveRoom();
    }

    /// <summary>
    /// Keeps track of all current connections
    /// </summary>
    private List<ConnectionId> mConnections = new List<ConnectionId>();
    public override bool IsConnected
    {
        get
        {
            return mConnections.Count > 0;
        }
    }

    private const int MAX_CODE_LENGTH = 256;
    private const string GET_ROOMS_COMMAND = "GetRooms";
    private const string BLOCK_ROOMS_COMMAND = "BlockRoom";
    private const char ROOM_SEPARATOR_CHAR = '@';
    private BufferedMessages bufferedMessages;

    public event EmptyEventHandler RoomCreated;
    public event EmptyEventHandler ConnectedToRoom;
    public event EmptyEventHandler ServerStartFailed;
    public event EmptyEventHandler ConnectionFailed;
    public event EmptyEventHandler RoomClosed;
    public override event EmptyEventHandler ReceivedAllBufferedMessages;

    private void Start()
    {
        Reset();
    }

    private void OnEnable()
    {
        ConnectedToRoom += ClosePopUp;
        RoomCreated += ClosePopUp;
    }

    private void OnDisable()
    {
        ConnectedToRoom += ClosePopUp;
        RoomCreated += ClosePopUp;
    }

    private void Setup()
    {
        Debug.Log("Setup Network");
        mNetwork = WebRtcNetworkFactory.Instance.CreateDefault(EditorVariables.ServerURL);
        if (mNetwork != null)
        {
            if (EditorVariables.AddLatency)
                latencySimulation = new LatencySimulation(mNetwork, EditorVariables.NumberFramesLatency);
            mNetwork.ConnectToServer();
        }
        else
        {
            Debug.LogError("Failed to access webrtc ");
        }
    }

    public void SendUserCommand(string command, params string[] args)
    {
        string content = command;
        if (args != null)
            for (int i = 0; i < args.Length; i++)
            {
                content += "@" + args[i];
            }
        Debug.Log("Send User Command " + content);
        mNetwork.SendSignalingEvent( MyComponents.Players.MyPlayerId, content, NetEventType.UserCommand);
    }

    public void GetRooms()
    {
        SendUserCommand(GET_ROOMS_COMMAND);
    }

    public override void RefreshRoomData()
    {
        RoomData data = new RoomData( MyComponents.Players.players.Count, CurrentlyPlaying, !MatchPanel.Password.Equals(""));
        SendUserCommand(NetEventMessage.REFRESH_ROOM, RoomName, data.ToNetworkEntity());
    }

    public override void Reset()
    {
        Debug.Log("Reset Network");
        MyComponents.Players.NewPlayerCreated -= SendBufferedMessagesOnSceneChange;
        //ReceivedAllBufferedMessages = null;
        StateCurrent = State.IDLE;
        mConnections = new List<ConnectionId>();
        Cleanup();
        Setup();
    }

    private void Cleanup()
    {
        if (mNetwork != null)
        {
            mNetwork.Dispose();
            mNetwork = null;
        }
    }

    private void OnDestroy()
    {
        if (mNetwork != null)
        {
            Cleanup();
        }
    }

    private void Update()
    {
        //check if the network was created
        if (mNetwork != null)
        {
            if (EditorVariables.AddLatency)
                latencySimulation.NewFrame();
            //first update it to read the data from the underlaying network system
            mNetwork.UpdateNetwork();

            ProcessSignalingEvents(mNetwork.SignalingEvents);
            ProcessPeerEvents(mNetwork.PeerEvents);

            //finish this update by flushing the messages out if the network wasn't destroyed during update
            if (mNetwork != null)
                mNetwork.Flush();
        }
    }

    private void ProcessSignalingEvents(Queue<NetworkEvent> signalingEvents)
    {
        //handle all new events that happened since the last updateef
        NetworkEvent evt;

        //check for new messages and keep checking if mNetwork is available. it might get destroyed
        //due to an event
        while (signalingEvents.Count != 0)
        {
            evt = signalingEvents.Dequeue();
            //Debug.LogWarning("Network Management : New event received " + evt);
            switch (evt.Type)
            {
                case NetEventType.RoomCreated:
                    {
                        //server initialized message received
                        Debug.Log("Room Created. Address: " + RoomName + "   " + evt.ConnectionId.id);
                        if (StateCurrent == State.IDLE)
                        {
                            StateCurrent = State.SERVER;
                            MyComponents.Players.NewPlayerCreated += SendBufferedMessagesOnSceneChange;
                            SetConnectionId(ConnectionId.INVALID);
                            RoomCreated.Invoke();
                        }
                    }
                    break;
                //user tried to start the server but it failed
                //maybe the user is offline or signaling server down?
                case NetEventType.SignalingConnectionFailed:
                    {
                        Reset();
                        //MyComponents.PopUp.Show(Language.Instance.texts["Failed_Connect"] + "\n" + Language.Instance.texts["Feedback"]);
                        Debug.LogError("Signaling connection failed " + evt.RawData);
                    }
                    break;
                case NetEventType.RoomCreationFailed:
                    {
                        string rawData = (string)evt.RawData;

                        if (rawData == NetEventMessage.ROOM_ALREADY_EXISTS)
                        {
                            if (ServerStartFailed != null)
                            {
                                Reset();
                                Setup();
                                ServerStartFailed.Invoke();
                            }
                            else
                            {
                                MyComponents.PopUp.Show(Language.Instance.texts["Room_Exists"] + Random_Name_Generator.GetRandomName() + "?");
                            }
                        }
                        else
                        {
                            Reset();
                            MyComponents.PopUp.Show(Language.Instance.texts["Room_Creation_Failed"]);
                        }
                        Debug.LogWarning("Room creation failed " + evt.RawData);
                    }
                    break;
                case NetEventType.RoomJoinFailed:
                    Debug.LogWarning("Room join failed " + evt.RawData);

                    if (evt.RawData != null)
                    {
                        //MyComponents.LobbyManager.RefreshServers();
                        string rawdata = (string)evt.RawData;
                        if (rawdata == NetEventMessage.ROOM_DOESNT_EXIST)
                        {
                            MyComponents.PopUp.Show(Language.Instance.texts["Doesnt_Exist"]);
                        }
                        else if (rawdata == NetEventMessage.GAME_STARTED)
                        {
                            MyComponents.PopUp.Show(Language.Instance.texts["Game_Started"]);
                        }
                        else if (rawdata == NetEventMessage.ROOM_FULL)
                        {
                            MyComponents.PopUp.Show(Language.Instance.texts["Room_Full"]);
                        }
                        else if (rawdata == NetEventMessage.WRONG_PASSWORD)
                        {
                            if (lastPassword.Equals(""))
                                PasswordPanel.InstantiatePanel(RoomName, MyComponents.PopUp.transform);
                            else
                                MyComponents.PopUp.Show(Language.Instance.texts["Wrong_Password"]);
                        }
                        else if (rawdata == NetEventMessage.SERVER_CONNECTION_NOT_1)
                        {
                            MyComponents.PopUp.Show("Server Connection is not 1");
                        }

                    }
                    if (ConnectionFailed != null)
                        ConnectionFailed.Invoke();
                    break;
                //server shut down. reaction to "Shutdown" call or
                //StopServer or the connection broke down
                case NetEventType.RoomClosed:
                    {
                        switch (StateCurrent)
                        {
                            case State.CONNECTED:
                                Debug.LogWarning("Room closed. No incoming connections possible until restart.");
                                break;
                            case State.IDLE:
                                MyComponents.PopUp.Show(Language.Instance.texts["Cant_Create"] + "\n" + Language.Instance.texts["Feedback"]);
                                Debug.LogWarning("Didn't manage to create the server " + RoomName);
                                break;
                            case State.SERVER:
                                Debug.LogWarning("Room closed. Reseting connection");
                                break;
                        }
                        if (RoomClosed != null)
                            RoomClosed.Invoke();
                    }
                    break;
                case NetEventType.UserCommand:

                    if (evt.RawData != null)
                    {
                        string rawData = ((string)evt.RawData);
                        string command = rawData.Split(ROOM_SEPARATOR_CHAR)[0];
                        Debug.LogWarning("Receive User Command " + command);

                        if (command == NetEventMessage.ASK_IF_ALLOWED_TO_ENTER)
                        {
                            Assert.IsTrue(Scenes.IsCurrentScene(Scenes.MainIndex));
                            string password = ((string)evt.RawData).Split(ROOM_SEPARATOR_CHAR)[1];

                            if ( MyComponents.Players.players.Count > MatchPanel.MaxNumberPlayers)
                                SendUserCommand(NetEventMessage.ASK_IF_ALLOWED_TO_ENTER.ToString(), evt.ConnectionId.id.ToString(), NetEventMessage.ROOM_FULL);
                            else if (CurrentlyPlaying)
                                SendUserCommand(NetEventMessage.ASK_IF_ALLOWED_TO_ENTER.ToString(), evt.ConnectionId.id.ToString(), NetEventMessage.GAME_STARTED);
                            else if (password != MatchPanel.Password)
                            {
                                Debug.Log(password);
                                Debug.Log(MatchPanel.Password);
                                SendUserCommand(NetEventMessage.ASK_IF_ALLOWED_TO_ENTER.ToString(), evt.ConnectionId.id.ToString(), NetEventMessage.WRONG_PASSWORD);
                            }
                            else
                                SendUserCommand(NetEventMessage.ASK_IF_ALLOWED_TO_ENTER.ToString(), evt.ConnectionId.id.ToString(), NetEventMessage.ALLOWED_TO_ENTER);

                        }
                        else
                        {
                            string[] rooms = rawData.Split(ROOM_SEPARATOR_CHAR);
                            if (rooms[0] == GET_ROOMS_COMMAND || rawData == GET_ROOMS_COMMAND)
                            {
                                MyComponents.LobbyManager.UpdateRoomList(RoomData.GetRoomData(rooms));
                            }
                        }
                    }
                    break;
                default:
                    Debug.LogWarning("The signaling network shouldn't receive that type of events " + evt.Type + "   " + evt.Info + "    " + evt.MessageData);
                    break;

            }
        }
    }

    private void ProcessPeerEvents(Queue<NetworkEvent> peerEvents)
    {
        NetworkEvent evt;
        while (peerEvents.Count != 0)
        {
            evt = peerEvents.Dequeue();
            switch (evt.Type)
            {
                //either user runs a client and connected to a server or the
                //user runs the server and a new client connected
                case NetEventType.NewConnection:
                    {
                        Debug.Log("NewConnection " + evt.Info + "  " + evt.ConnectionId.id);

                        mConnections.Add(evt.ConnectionId);

                        if (IsServer)
                        {
                            MyComponents.NetworkViewsManagement.SendClientInstanciationInterval(evt.ConnectionId);
                            MyComponents.Players.SendPlayersData(evt.ConnectionId);
                            View.RPC("SetConnectionId", evt.ConnectionId, evt.ConnectionId);
                        }
                        else if (ConnectedToRoom != null)
                        {
                            StateCurrent = State.CONNECTED;
                            ConnectedToRoom.Invoke();
                        }
                    }
                    break;
                case NetEventType.ConnectionFailed:
                    {
                        Debug.LogWarning("Connection to peer failed " + StateCurrent + "    " + (string)evt.RawData);
                        //MyComponents.PopUp.Show(Language.Instance.texts["Connection_Failed"]);
                        if (ConnectionFailed != null)
                            ConnectionFailed.Invoke();
                    }
                    break;
                case NetEventType.Disconnected:
                    {
                        mConnections.Remove(evt.ConnectionId);
                        //A connection was disconnected
                        //If this was the client then he was disconnected from the server
                        //if it was the server this just means that one of the clients left
                        Debug.Log("Local Connection ID " + evt.ConnectionId + " disconnected");
                        if (IsServer == false)
                        {
                            Debug.Log("Host disconnected ");
                            if (StateCurrent == State.CONNECTED)
                            {
                                MyComponents.PopUp.Show(Language.Instance.texts["Host_Disconnected"]);
                                if (RoomClosed != null)
                                    RoomClosed.Invoke();
                            }
                        }
                        else
                        {
                            mConnections.Remove(evt.ConnectionId);
                            MyComponents.Players.RemovePlayer(evt.ConnectionId);
                            Debug.Log("User " + evt.ConnectionId + " left the room.");
                        }
                    }
                    break;
                case NetEventType.ReliableMessageReceived:
                case NetEventType.UnreliableMessageReceived:
                    {
                        HandleIncomingEvent(ref evt);
                    }
                    break;
                default:
                    Debug.LogError("The peer network shouldn't receive that type of events " + evt.Type + "   " + evt.Info + "    " + evt.MessageData);
                    break;
            }
        }
    }

    private void SendBufferedMessagesOnSceneChange(ConnectionId id)
    {
        MyComponents.Players.players[id].eventNotifier.ListenToEvents(bufferedMessages.SendBufferedMessages, PlayerFlags.SCENEID);
    }

    private void HandleIncomingEvent(ref NetworkEvent evt)
    {
        MessageDataBuffer buffer = evt.MessageData;
        NetworkMessage message = NetworkMessage.Deserialize(buffer.Buffer.SubArray(0, buffer.ContentLength));
        HandleIncomingMessage(evt.ConnectionId, message);
    }

    private void HandleIncomingMessage(ConnectionId senderId, NetworkMessage message)
    {
        if (message.traceMessage)
        {
            Debug.LogError("HandleTracedMessage " + message + "   RPCID :" + BitConverter.ToInt16(message.data, 0));
        }
        if (IsServer && message.isDistributed())
        {
            List<ConnectionId> receivers;
            if (message.isSentToTeam())
            {
                receivers = new List<ConnectionId>();
                List<Player> teamMates = MyComponents.Players.GetPlayersInTeam( MyComponents.Players.players[senderId].Team);
                teamMates.Map(player => { if (player.id != MyComponents.Players.MyPlayerId && player.id != senderId) receivers.Add(player.id); });
            }
            else
            {
                receivers = new List<ConnectionId>(mConnections);
                receivers.Remove(senderId);
            }
            SendNetworkMessage(message, receivers.ToArray());
        }
        if (IsServer)
            bufferedMessages.TryAddBuffered(senderId, message);

        if (!IsServer || !message.isSentToTeam() || MyComponents.Players.players[senderId].Team == MyComponents.MyPlayer.Team)
            MyComponents.NetworkViewsManagement.DistributeMessage(senderId, message);

        //This line of code is causing random crashes. 
        //It seems the crash occur when the time between creating and disposing is too short
        // buffer.Dispose();
    }

    public override void SendData(short viewId, MessageType type, byte[] data)
    {
        NetworkMessage message = new NetworkMessage(viewId, 0, type, data);
        SendNetworkMessage(message, mConnections.ToArray());
    }

    public override void SendData(short viewId, MessageType type, byte[] data, ConnectionId id)
    {
        NetworkMessage message = new NetworkMessage(viewId, 0, type, data);
        message.flags |= MessageFlags.NotDistributed;
        SendNetworkMessage(message, id);
    }

    public override void SendData(short viewId, short subId, MessageType type, byte[] data)
    {
        NetworkMessage message = new NetworkMessage(viewId, subId, type, data);
        SendNetworkMessage(message, mConnections.ToArray());
    }

    public override void SendData(short viewId, short subId, MessageType type, byte[] data, ConnectionId id)
    {
        NetworkMessage message = new NetworkMessage(viewId, subId, type, data);
        message.flags |= MessageFlags.NotDistributed;
        SendNetworkMessage(message, id);

    }

    public override void SendNetworkMessage(NetworkMessage message)
    {
        if (message.isSentToTeam() && IsServer)
        {
            List<ConnectionId> connectionIds = new List<ConnectionId>();
            List<Player> teamMates = MyComponents.Players.GetPlayersInTeam( MyComponents.MyPlayer.Team);
            teamMates.Map(player => { if (player.id != MyComponents.Players.MyPlayerId) connectionIds.Add(player.id); });
            SendNetworkMessage(message, connectionIds.ToArray());
        }
        else
            SendNetworkMessage(message, mConnections.ToArray());
    }

    public override void SendNetworkMessage(NetworkMessage message, params ConnectionId[] connectionIds)
    {
        byte[] dataToSend = message.Serialize();
        if (message.traceMessage)
        {
            Debug.LogError("SendData " + message);
        }
        for (int i = 0; i < connectionIds.Length; i++)
        {
            SendToNetwork(dataToSend, connectionIds[i], message.isReliable());
        }
        if (IsServer)
            bufferedMessages.TryAddBuffered(SERVER_CONNECTION_ID, message);
    }

    private void SendToNetwork(byte[] data, ConnectionId id, bool reliable)
    {
        if (EditorVariables.AddLatency)
            latencySimulation.AddMessage(data, id, reliable);
        else
            mNetwork.SendPeerEvent(id, data, 0, data.Length, reliable);
        if (!mConnections.Contains(id))
            Debug.LogError("This isn't a valid connectionId " + id + "    " + mConnections.Count + "   " + mConnections.PrintContent());
    }

    private string lastPassword = "";

    public void ConnectToRoom(string roomName, string password = "")
    {
        RoomName = roomName;
        lastPassword = password;
        mNetwork.ConnectToRoom(roomName, password);
        MyComponents.PopUp.Show(Language.Instance.texts["Connecting"]);
        Debug.Log("Connecting to room : " + roomName + " using password $" + password + "$");
    }

    private void EnsureLength(string roomName)
    {
        if (roomName.Length > MAX_CODE_LENGTH)
        {
            roomName = roomName.Substring(0, MAX_CODE_LENGTH);
        }
    }

    public void CreateRoom(string roomName)
    {
        EnsureLength(roomName);
        mNetwork.CreateRoom(roomName);
        RoomName = roomName;
        MyComponents.PopUp.Show(Language.Instance.texts["Connecting"]);

        Debug.Log("CreateRoom " + roomName);
    }

    private void ClosePopUp()
    {
        MyComponents.PopUp.ClosePopUp();
    }

    #region host
    public override int GetNumberPlayers()
    {
        Assert.IsTrue(IsServer);
        return mConnections.Count + 1;
    }

    #endregion

    #region client
    [MyRPC]
    private void ReceivedAllBuffered()
    {
        if (ReceivedAllBufferedMessages != null)
        {
            ReceivedAllBufferedMessages.Invoke();
        }
    }

    [MyRPC]
    private void SetConnectionId(ConnectionId id)
    {
        SceneChangeEventHandler handler;
        if (!EditorVariables.HeadlessServer)
        {
            MyComponents.Players.CreatePlayer(id);
            MyComponents.Players.MyPlayerId = id;
            MyComponents.MyPlayer.Nickname = UserSettings.Nickname;
            MyComponents.MyPlayer.SceneId = Scenes.currentSceneId;
            handler = (previousSceneId, currentSceneId) => { MyComponents.MyPlayer.SceneId = currentSceneId; };
        }
        else
        {
            handler = (previousSceneId, currentSceneId) => { bufferedMessages.SendBufferedMessages(currentSceneId, ConnectionId.INVALID); };
        }
        NavigationManager.FinishedLoadingScene += handler;
    }

    #endregion
}
