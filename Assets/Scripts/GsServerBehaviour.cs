using HexHeroes.Lobbies;
using HexHeroes.Messaging;
using HexHeroes.Messaging.Messages;
using HexHeroes.Streamables;
using HexHeroes.Users;
using HexHeroes.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;

public class GsServerBehaviour : MonoBehaviour
{
    public static GsServerBehaviour instance;
    public NetworkDriver m_ServerDriver;
    public NetworkConnection m_ServerConnection;
    private StreamReader streamReader;
    private NativeHashMap<int, NetworkConnection> m_ServerConnections;
    public NativeHashMap<int, FixedString64Bytes> serverSuppliedTokens;
    public NativeHashMap<int, FixedString64Bytes> clientSuppliedTokens;
    private NativeList<int> connsToTerminate;
    private int maxConnections;
    private ServerUserManager serverUserManager;
    private bool running;
    private GameSettings gameSettings;

    public void Awake()
    {
        if(instance == null)
        {
            running = false;
            instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    private void DisposeAllNatives()
    {
        m_ServerConnections.Dispose();
        serverSuppliedTokens.Dispose();
        clientSuppliedTokens.Dispose();
        connsToTerminate.Dispose();
        ServerUserManager.DisposeAllNatives();
    }

    public void OnDestroy()
    {
        DisposeAllNatives();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (running)
        {
            m_ServerDriver.ScheduleUpdate().Complete();
            CleanUpConnections();
            AcceptConnections();
            CheckForEvents();
        }
        
    }

    public void SendMessageToAll(Message m)
    {
        foreach(int userID in ServerUserManager.UserIDs)
        {
            ServerUserInstance sui = ServerUserManager.GetUserInstance(userID);
            SendMessage(m, sui.connID);
        }
    }

    public void SendMessage(Message m, int connID)
    {
        m_ServerDriver.BeginSend(m_ServerConnections[connID], out DataStreamWriter stream);
        stream.WriteInt(m.MessageType);
        m.SerializeToStream(ref stream, connID);
        if (m is ISecureMessage)
        {
            ((ISecureMessage)m).SerializeSecurityToStream(ref stream, connID);
        }
        stream.WriteInt(-1);
        Debug.Log(string.Format("Wrote {0} bytes to stream", stream.Length));
        m_ServerDriver.EndSend(stream);
    }

    private bool VerifySecureMessage(SecureMessage sm, int connID)
    {
        string origin = sm.timestamp.ToString() + clientSuppliedTokens[connID] + serverSuppliedTokens[connID];
        print(string.Format("Origin: {0}", origin));
        string verificationString = SecurityUtility.Sha512FromString(origin);
        print(string.Format("Verification string: {0}", verificationString));
        return verificationString == sm.key;
    }

    public void ForciblyCloseConnection(int connID, byte reason)
    {
        if (connsToTerminate.Contains(connID))
        {
            return;
        }
        ServerUserManager.OnConnectionForciblyEnded(connID);
        ForciblyCloseConnectionMessage fcc = new ForciblyCloseConnectionMessage();
        fcc.reason = reason;
        SendMessage(fcc, connID);
        connsToTerminate.Add(connID);
    }

    private void CheckForEvents()
    {
        DataStreamReader stream;
        NativeArray<int> connIDs = m_ServerConnections.GetKeyArray(Allocator.Temp);
        for (int i = 0; i < connIDs.Length; i++)
        {
            int connID = connIDs[i];
            NetworkConnection connection = m_ServerConnections[connID];
            if (!m_ServerConnections[connID].IsCreated)
                continue;
            NetworkEvent.Type cmd;
            while ((cmd = m_ServerDriver.PopEventForConnection(connection, out stream)) != NetworkEvent.Type.Empty)
            {
                Debug.Log(string.Format("Received {0} bytes of data", stream.Length));
                if (cmd == NetworkEvent.Type.Data)
                {

                    List<Message> messages = streamReader.ProcessMessagesFromConnection(ref stream, connID);
                    foreach (Message message in messages)
                    {

                        if (message is ISecureMessage)
                        {
                            if (!VerifySecureMessage((SecureMessage)message, connID))
                            {
                                ForciblyCloseConnection(connID, 0);
                                break;
                            }
                        }
                        switch (message.MessageType)
                        {
                            default:
                                throw new NotImplementedException("No logic has been defined to deal with a net message of that type");
                            case MessageTypes.ChallengeResponse:
                                OnChallengeResponseMessage ocrm = new OnChallengeResponseMessage();
                                ChallengeResponseMessage crm = (ChallengeResponseMessage)message;
                                Dictionary<FixedString64Bytes, int> challengeUserMapping = ServerUserManager.GetUserChallengeMapping();
                                int userIdFound = -1;
                                foreach (FixedString64Bytes challenge in challengeUserMapping.Keys)
                                {
                                    if(SecurityUtility.GenerateGameServerChallengeIdentifier(challenge.ToString(), serverSuppliedTokens[connID].ToString()) == crm.challengeIdentifier)
                                    {
                                        userIdFound = challengeUserMapping[challenge];
                                        break;
                                    }
                                }
                                if(userIdFound == -1)
                                {
                                    ocrm.success = 1;
                                    SendMessage(ocrm, connID);
                                    return;
                                }
                                ServerUserInstance userInstance = ServerUserManager.GetUserInstance(userIdFound);
                                FixedString512Bytes verificationString = SecurityUtility.GenerateGameServerChallengeString(userInstance.gameServerChallenge.ToString(), userInstance.mainServerSuppliedToken.ToString(), serverSuppliedTokens[connID].ToString(), crm.clientSuppliedToken.ToString());
                                if(verificationString != crm.verificationString)
                                {
                                    ocrm.success = 2;
                                    SendMessage(ocrm, connID);
                                    return;
                                }
                                clientSuppliedTokens.Add(connID, crm.clientSuppliedToken);
                                ocrm.success = 0;
                                SendMessage(ocrm, connID);
                                Debug.Log("Accepted the users challenge.");
                                userInstance.connID = connID;
                                userInstance.connected = true;
                                if(ServerUserManager.CountConnectedUsers() == ServerUserManager.Count)
                                {
                                    InitiateGameLoadMessage iglm = new InitiateGameLoadMessage();
                                    iglm.gameSettings = gameSettings;
                                    SendMessageToAll(iglm);
                                }
                                break;
                        }
                    }
                    if (connsToTerminate.Contains(connID))
                    {
                        break;
                    }
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client disconnected from server");
                    m_ServerConnections.Remove(connID);
                    serverSuppliedTokens.Remove(connID);
                }
            }

        }
        foreach (int connID in connsToTerminate)
        {
            m_ServerConnections[connID].Disconnect(m_ServerDriver);
        }
        connsToTerminate.Clear();
        UpdateableObjectManager.instance.ProcessUpdateQueue();
        
    }

    private void AcceptConnections()
    {
        NetworkConnection c;
        while ((c = m_ServerDriver.Accept()) != default(NetworkConnection))
        {
            int connID = c.GetHashCode();
            Debug.Log(string.Format("Accepting a connection with ConnID: {0}", connID));
            m_ServerConnections.Add(connID, c);
            string serverSuppliedToken = SecurityUtility.GenerateRandom(32);
            serverSuppliedTokens.Add(connID, serverSuppliedToken);

            Debug.Log("Accepted a connection");
            OnConnectionMessage ocm = new OnConnectionMessage();
            ocm.serverSuppliedToken = serverSuppliedToken;
            SendMessage(ocm, connID);
        }
    }

    //Remove stale connections
    private void CleanUpConnections()
    {
        NativeList<int> connsToRemove = new NativeList<int>(Allocator.Temp);
        NativeArray<int> connIDs = m_ServerConnections.GetKeyArray(Allocator.Temp);
        for (int i = 0; i < connIDs.Length; i++)
        {
            int connID = connIDs[i];
            if (!m_ServerConnections[connID].IsCreated)
            {
                connsToRemove.Add(connID);
            }
        }
        while (connsToRemove.Length > 0)
        {
            m_ServerConnections.Remove(connsToRemove[0]);
            connsToRemove.RemoveAt(0);
        }
        connsToRemove.Dispose();
        connIDs.Dispose();
    }

    public OnGameServerStartedMessage StartServer(StartGameServerMessage sgsm)
    {
        OnGameServerStartedMessage ogssm = new OnGameServerStartedMessage();
        ogssm.userChallenges = new NativeHashMap<int, FixedString64Bytes>(sgsm.users.Count, Allocator.Temp);
        maxConnections = sgsm.users.Count;
        Debug.Log("Starting Server!");
        streamReader = new StreamReader();
        m_ServerDriver = NetworkDriver.Create();
        NetworkEndPoint endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = GsClientBehaviour.instance.serverPort;
        if (m_ServerDriver.Bind(endpoint) != 0)
        {
            Debug.Log(string.Format("Failed to bind to port {0}", endpoint.Port));
            ogssm.success = 1;
            return ogssm;
        }
        else
        {
            m_ServerDriver.Listen();
        }
        m_ServerConnections = new NativeHashMap<int, NetworkConnection>(maxConnections, Allocator.Persistent);
        serverSuppliedTokens = new NativeHashMap<int, FixedString64Bytes>(maxConnections, Allocator.Persistent);
        clientSuppliedTokens = new NativeHashMap<int, FixedString64Bytes>(maxConnections, Allocator.Persistent);
        connsToTerminate = new NativeList<int>(Allocator.Persistent);
        serverUserManager = new ServerUserManager(maxConnections);

        foreach(ShareableServerUserInstance sharedUserInstance in sgsm.users)
        {
            ServerUserManager.RegisterUserInstanceFromSharedInstance(sharedUserInstance);
            FixedString64Bytes challenge = SecurityUtility.GenerateRandom(32);
            ogssm.userChallenges.Add(sharedUserInstance.userID, challenge);
            ServerUserManager.GetUserInstance(sharedUserInstance.userID).gameServerChallenge = challenge;

        }
        ogssm.success = 0;
        ogssm.lobbyID = sgsm.lobbyID;
        gameSettings = sgsm.gameSettings;
        
        running = true;
        return ogssm;
    }


}
