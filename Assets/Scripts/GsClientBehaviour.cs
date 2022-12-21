using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Networking.Transport;
using HexHeroes.Messaging;
using HexHeroes.Messaging.Messages;
using HexHeroes.Streamables;
using HexHeroes.Utility;
using Unity.Collections;
using System;
using UnityTransportStreamExtensions;
using HexHeroes.Users;

public class GsClientBehaviour : MonoBehaviour, IMessageSender
{
    public static GsClientBehaviour instance;
    public NetworkDriver m_ClientDriver;
    public NetworkConnection m_ClientConnection;
    private MainStreamReader streamReader;
    private GameServerUpdateableObjectManager updateableObjectManager;
    public string gameServerIdentifier;
    public string gameServerSecret;
    public string serverSuppliedToken;
    public string gameServerSuppliedToken;
    public ushort serverPort;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        instance = null;
        ServerUserManager.ClearInstance();
    }

    public void Awake()
    {
        if(gameServerIdentifier == "")
        {
            gameServerIdentifier = SecurityUtility.GenerateRandom(32);
        }
        if (gameServerSecret == "")
        {
            gameServerSecret = SecurityUtility.GenerateRandom(128);
        }
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
            updateableObjectManager = new GameServerUpdateableObjectManager();
            updateableObjectManager.messageSender = this;
            UpdateableObjectManager.instance = updateableObjectManager;
            SecureMessage.keyGenerator = GenerateGameServerSignature;
        }
        else
        {
            Destroy(this);
        }
    }

    public FixedString512Bytes GenerateGameServerSignature(float timestamp, int connID)
    {
        string origin = timestamp.ToString() + gameServerSuppliedToken.ToString() + serverSuppliedToken.ToString();
        print(string.Format("Origin: {0}", origin));
        return SecurityUtility.Sha512FromString(origin);
    }

    public void SendMessage(Message m, int connID = -1)
    {
        Debug.Log(string.Format("Sending a message of type {0}", m.MessageType));
        m_ClientDriver.BeginSend(m_ClientConnection, out DataStreamWriter stream);
        stream.WriteInt(m.MessageType);
        m.SerializeToStream(ref stream, connID);
        if (m is ISecureMessage)
        {
            ((ISecureMessage)m).SerializeSecurityToStream(ref stream, connID);
        }
        stream.WriteInt(-1);
        Debug.Log(string.Format("Wrote {0} bytes to stream", stream.Length));
        m_ClientDriver.EndSend(stream);
    }

    public void OnDestroy()
    {
        UpdateableObjectManager.DisposeAllNatives();
        m_ClientDriver.Dispose();
    }

    // Start is called before the first frame update
    void Start()
    {
        streamReader = new MainStreamReader();
        m_ClientDriver = NetworkDriver.Create();
        m_ClientConnection = default(NetworkConnection);
        NetworkEndPoint clientEndpoint = NetworkEndPoint.LoopbackIpv4;
        clientEndpoint.Port = 9000;
        m_ClientConnection = m_ClientDriver.Connect(clientEndpoint);
    }

    private FixedString512Bytes GenerateVerificationString()
    {
        return SecurityUtility.GenerateGameServerVerificationString(gameServerIdentifier, gameServerSecret, serverSuppliedToken);
    }

    

    // Update is called once per frame
    void Update()
    {
        m_ClientDriver.ScheduleUpdate().Complete();

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        while ((cmd = m_ClientConnection.PopEvent(m_ClientDriver, out stream)) != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                Debug.Log("We are now connected to the main server");
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                Debug.Log(string.Format("Received {0} bytes of data", stream.Length));
                List<Message> messages = streamReader.ProcessMessagesFromConnection(ref stream, -1);
                foreach (Message message in messages)
                {
                    switch (message.MessageType)
                    {
                        default:
                            throw new NotImplementedException(string.Format("The message of type {0} has no logic defined for it.", message.MessageType));
                        case MessageTypes.OnConnection:
                            OnConnectionMessage ocm = (OnConnectionMessage)message;
                            serverSuppliedToken = ocm.serverSuppliedToken.ToString();
                            gameServerSuppliedToken = SecurityUtility.GenerateRandom(32);
                            GameServerValidationMessage gsvm = new GameServerValidationMessage();
                            gsvm.gameServerIdentifier = gameServerIdentifier;
                            gsvm.gameServerSuppliedToken = gameServerSuppliedToken;
                            gsvm.verificationString = GenerateVerificationString();
                            SendMessage(gsvm, -1);
                            break;
                        case MessageTypes.OnGameServerValidation:
                            OnGameServerValidationMessage ogsvm = (OnGameServerValidationMessage)message;
                            if(ogsvm.success == 0)
                            {
                                serverPort = ogsvm.port;
                            }
                            else
                            {
                                Debug.LogError("Failed to validate with main server");
                            }
                            break;
                        case MessageTypes.StartGameServer:
                            StartGameServerMessage sgsm = (StartGameServerMessage)message;
                            OnGameServerStartedMessage ogssm = GsServerBehaviour.instance.StartServer(sgsm);
                            SendMessage(ogssm, -1);
                            break;
                    }
                }
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                Debug.Log("Game Server got disconnected from the main server");
                m_ClientConnection = default(NetworkConnection);

            }
        }
        //UpdateableObjectManager.instance.CheckUpdates();
        UpdateableObjectManager.instance.ProcessUpdateQueue();
    }
}
