﻿using System;
using UnityEngine;
using static PacketSerializer;

public class NetworkClient : MonoBehaviour, NetworkListener {

    public struct NetworkClientState {
        public CharServerInfo CharServer;
        public AC.ACCEPT_LOGIN LoginInfo;
    }

    public const int DATA_BUFFER_SIZE = 16 * 1024;
    public static int CLIENT_ID = new System.Random().Next();

    private Connection CurrentConnection;
    private InPacket packet;
    public NetworkClientState State;

    public void Start() {
        CurrentConnection = new Connection(this);
        State = new NetworkClientState();
    }

    private void OnApplicationQuit() {
        Disconnect();
    }

    public void ConnectToServer() {
        CurrentConnection.Connect();
    }

    private void Disconnect() {
        CurrentConnection.Disconnect();
    }

    public void OnTcpConnected() {
        HookPacket(AC.ACCEPT_LOGIN.HEADER, (ushort cmd, int size, InPacket packet) => {
            if(packet is AC.ACCEPT_LOGIN) {
                CurrentConnection.Disconnect();

                this.packet = packet;
                var charServerInfo = (this.packet as AC.ACCEPT_LOGIN).Servers[0];
                ConnectToCharServer(charServerInfo);
            }
        });
        if(packet == null) {
            new CA.LOGIN("danilo", "123456", 10, 10).Send(CurrentConnection.GetBinaryWriter());
        } else if(packet is AC.ACCEPT_LOGIN) {
            var acceptLogin = this.packet as AC.ACCEPT_LOGIN;
            State.LoginInfo = acceptLogin;
            new CH.ENTER(acceptLogin.AccountID, acceptLogin.LoginID1, acceptLogin.LoginID2, acceptLogin.Sex).Send(CurrentConnection.GetBinaryWriter());
        } else if (packet is HC.NOTIFY_ZONESVR) {
            new CZ.ENTER(State.LoginInfo.AccountID, State.LoginInfo.LoginID1, State.LoginInfo.LoginID2, State.LoginInfo.Sex).Send(CurrentConnection.GetBinaryWriter());
        }
    }

    private void ConnectToCharServer(CharServerInfo charServerInfo) {
        State.CharServer = charServerInfo;
        CurrentConnection.Connect(charServerInfo.IP.ToString(), charServerInfo.Port);
        HookPacket(HC.ACCEPT_ENTER.HEADER, (ushort cmd, int size, InPacket packet) => {
            if(packet is HC.ACCEPT_ENTER) {
                SelectCharacter();
            }
        });
    }

    private void SelectCharacter() {
        new CH.SELECT_CHAR(0).Send(CurrentConnection.GetBinaryWriter());
        HookPacket(HC.NOTIFY_ZONESVR.HEADER, (ushort cmd, int size, InPacket packet) => {
            if(packet is HC.NOTIFY_ZONESVR) {
                this.packet = packet;
                var pkt = packet as HC.NOTIFY_ZONESVR;
                CurrentConnection.Disconnect();
                CurrentConnection.Connect(pkt.IP.ToString(), pkt.Port);
            }
        });
    }

    public void HookPacket(PacketHeader cmd, OnPacketReceived onPackedReceived) {
        CurrentConnection.Hook((ushort)cmd, onPackedReceived);
    }

    private void Update() {

    }

    public void Ping() {
        var ticks = Time.realtimeSinceStartup;
        if(ticks % 12 < 1f) {
            new Ping((int)Time.realtimeSinceStartup).Send(CurrentConnection.GetBinaryWriter());
        }
    }

    /**
     * Are we gonna try to reconnect?
     */
    public void OnDisconnected(NetworkProtocol protocol) {
        throw new NotImplementedException();
    }
}