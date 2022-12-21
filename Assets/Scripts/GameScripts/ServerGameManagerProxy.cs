using HexagonHeroes_GameLibrary;
using HexagonHeroes_GameLibrary.MapScripts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ServerGameManagerProxy : GameManagerProxy
{

    public override void AdminSpawnStructure(int structureType, byte owner, byte variation, byte architectureSet, HexOffsetCoordinates baseCoords, byte rotation)
    {
        gameManager.AdminSpawnStructure(structureType, owner, variation, architectureSet, baseCoords, rotation);
    }

    public override void ForceSpawnUnit(int unitTypeID, HexOffsetCoordinates hexPos, byte rotation, ushort player)
    {
        gameManager.ForceSpawnUnit(unitTypeID, hexPos, rotation, player);
    }

    public override void MoveUnit(int playerID, HexOffsetCoordinates finalPos)
    {
        
    }
}
