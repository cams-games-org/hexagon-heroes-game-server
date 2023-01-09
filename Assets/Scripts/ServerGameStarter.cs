using HexagonHeroes_GameLibrary;
using HexagonHeroes_GameLibrary.GameDataContainers;
using HexagonHeroes_GameLibrary.MapScripts;
using HexagonHeroes_GameLibrary.Simulation;
using HexHeroes.Lobbies;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ServerGameStarter : MonoBehaviour
{
    private static ServerGameStarter instance;
    public GameManager gameManager;
    private bool mapGenerated;
    public GameManagerProxy gameManagerProxy;
    public MasterSimulationManager masterSimulationManager;
    public GameplaySimulator gameplaySimulator;
    private GameSettingsContainer gameSettings;

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
        instance.gameSettings = gameSettings;
        
        instance.gameManager.Initialize(gameSettings, true, instance.OnMapGenerated);
        
    }

    private void OnMapGenerated()
    {
        mapGenerated= true;
        gameManager.SetGrid(MapGenerator.GetCompleteGrid());
    }



    public static void StartGame()
    {
        instance.masterSimulationManager.gameObject.SetActive(true);
        GameManager.SetGameplaySimulationManager(instance.masterSimulationManager);
        instance.masterSimulationManager.Initialize(instance.gameplaySimulator, instance.gameSettings.PlayerCount, false, instance.gameSettings.GetAIPlayerIndexes());
        instance.masterSimulationManager.DoFirstTick();
    }
}
