using HexagonHeroes_GameLibrary;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ServerGameStarter : MonoBehaviour
{
    private static ServerGameStarter instance;
    public GameManager gameManager;
    private bool mapGenerated;
    public GameManagerProxy gameManagerProxy;

    public static bool IsMapGenerated { get { return instance.mapGenerated; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        instance = null;
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    public static void PrepareGameScene(GameSettingsContainer gameSettings)
    {
        if (instance == null)
        {
            Debug.Log("Instance is null!");
        }
        if (gameSettings == null) { Debug.Log("Game Settings are null!"); }
        instance.gameManager.Initialize(gameSettings, true, instance.OnMapGenerated);
        InboundActionHandler.SetGameManagerProxy(instance.gameManagerProxy);
    }

    private void OnMapGenerated()
    {
        mapGenerated= true;
    }



    public static void StartGame()
    {
        
    }
}
