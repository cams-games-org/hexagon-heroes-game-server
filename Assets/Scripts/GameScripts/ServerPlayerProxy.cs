using HexagonHeroes_GameLibrary;
using HexagonHeroes_GameLibrary.GameEvents;
using HexagonHeroes_GameLibrary.MapScripts;
using HexagonHeroes_GameLibrary.Messages;
using HexHeroes.Users;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ServerPlayerProxy : PlayerProxy
{
    int connID;
    public override void Initialize(PlayerContainer playerContainer)
    {
        connID = ServerUserManager.GetUserFromPlayerIndex(playerContainer.playerIndex).connID;
    }

    public override void OnMatchStart(long matchStartTimestamp)
    {
        
    }

    public override void OnStructureBuilt(int structureInstanceID, int structureType, byte owner, byte variation, byte architectureSet, HexOffsetCoordinates baseCoords, byte rotation)
    {
        StructureBuiltEvent structureBuiltEvent = new StructureBuiltEvent();
        structureBuiltEvent.rotation = rotation;
        structureBuiltEvent.owner = owner;
        structureBuiltEvent.variation = variation;
        structureBuiltEvent.architectureSet = architectureSet;
        structureBuiltEvent.baseCoordinates = baseCoords;
        structureBuiltEvent.structureType = structureType;
        structureBuiltEvent.structureInstanceID = structureInstanceID;
        SendGameEvent(structureBuiltEvent);
    }

    public override void OnUnitMoved(int unitID, HexOffsetCoordinates finalPos)
    {
        throw new System.NotImplementedException();
    }

    public override void OnUnitSpawned(int unitTypeID, NodeOffsetCoordinates nodePos, byte rotation, ushort player)
    {
        UnitSpawnedEvent unitSpawnedEvent = new UnitSpawnedEvent();
        unitSpawnedEvent.unitTypeID = unitTypeID;
        unitSpawnedEvent.nodePos = nodePos;
        unitSpawnedEvent.rotation = rotation;
        unitSpawnedEvent.player = player;
        SendGameEvent(unitSpawnedEvent);
    }

    private void SendGameEvent(GameEvent gameEvent)
    {
        GameEventMessage gameEventMessage = new GameEventMessage();
        gameEventMessage.gameEvent = gameEvent;
        GsServerBehaviour.instance.SendMessage(gameEventMessage, connID);
    }
}
