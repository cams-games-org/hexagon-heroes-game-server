using HexagonHeroes_GameLibrary;
using HexagonHeroes_GameLibrary.BaseTypes;
using HexagonHeroes_GameLibrary.GameActions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ServerGameManagerProxy : GameManagerProxy
{
    public override void AfirmTickOver(int simulationStep)
    {
        
    }

    protected override void SendAction(GameAction gameAction)
    {
        GameManager.instance.PerformAction(gameAction);
    }
}
