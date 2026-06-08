using System.Collections.Generic;

namespace Rivet.Protocol;

public class ConnectMsg : DataObject
{
    public string Playername { get; set; } = "";
    public ulong CSteamID { get; set; }
    public byte ForcedSelfID { get; set; } = byte.MaxValue;

    protected override List<object> Serialize() => new() { Playername, CSteamID, ForcedSelfID };
    protected override void Deserialize(List<object> fields) { Playername = (string)fields[0]; CSteamID = (ulong)fields[1]; ForcedSelfID = (byte)fields[2]; }
}

public class ConnectAnswer : DataObject
{
    public byte PlayerID { get; set; } = byte.MaxValue;
    public bool ConnectSuccessfull { get; set; }
    public byte ConnectionFailReason { get; set; } = byte.MaxValue;

    protected override List<object> Serialize() => new() { PlayerID, ConnectSuccessfull, ConnectionFailReason };
    protected override void Deserialize(List<object> fields) { PlayerID = (byte)fields[0]; ConnectSuccessfull = (bool)fields[1]; ConnectionFailReason = (byte)fields[2]; }
}

public class PlayerIDMsg : DataObject
{
    public byte PlayerID { get; set; } = byte.MaxValue;

    protected override List<object> Serialize() => new() { PlayerID };
    protected override void Deserialize(List<object> fields) { PlayerID = (byte)fields[0]; }
}

public class DisconnectMsg : PlayerIDMsg { }

public class PingMsgToServer : PlayerIDMsg
{
    public float EstimatedSelfPing { get; set; }

    protected override List<object> Serialize() { var l = base.Serialize(); l.Add(EstimatedSelfPing); return l; }
    protected override void Deserialize(List<object> fields) { base.Deserialize(fields); EstimatedSelfPing = (float)fields[1]; }
}

public class PingMsg : DataObject
{
    protected override List<object> Serialize() => new();
}

public class InputsMsg : PlayerIDMsg
{
    public byte Axis0 { get; set; } = 127;
    public byte Axis1 { get; set; } = 127;
    public byte Axis2 { get; set; } = 127;
    public byte Axis3 { get; set; } = 127;
    public byte Axis4 { get; set; } = 127;
    public byte Axis5 { get; set; } = 127;
    public byte Axis0Raw { get; set; } = 127;
    public byte Axis1Raw { get; set; } = 127;
    public byte Axis2Raw { get; set; } = 127;
    public byte Axis3Raw { get; set; } = 127;
    public byte Axis4Raw { get; set; } = 127;
    public byte Axis5Raw { get; set; } = 127;
    public ulong InputMap { get; set; }
    public ulong InputMapToggle { get; set; }

    protected override List<object> Serialize()
    {
        var l = base.Serialize();
        l.Add(Axis0); l.Add(Axis1); l.Add(Axis2); l.Add(Axis3); l.Add(Axis4); l.Add(Axis5);
        l.Add(Axis0Raw); l.Add(Axis1Raw); l.Add(Axis2Raw); l.Add(Axis3Raw); l.Add(Axis4Raw); l.Add(Axis5Raw);
        l.Add(InputMap); l.Add(InputMapToggle);
        return l;
    }
    protected override void Deserialize(List<object> fields)
    {
        base.Deserialize(fields);
        Axis0 = (byte)fields[1]; Axis1 = (byte)fields[2]; Axis2 = (byte)fields[3]; Axis3 = (byte)fields[4]; Axis4 = (byte)fields[5]; Axis5 = (byte)fields[6];
        Axis0Raw = (byte)fields[7]; Axis1Raw = (byte)fields[8]; Axis2Raw = (byte)fields[9]; Axis3Raw = (byte)fields[10]; Axis4Raw = (byte)fields[11]; Axis5Raw = (byte)fields[12];
        InputMap = (ulong)fields[13]; InputMapToggle = (ulong)fields[14];
    }
}

public class AllInputsMsg : DataObject
{
    public byte[] PlayerIDs { get; set; } = [];
    public byte[] StickLeftX { get; set; } = [];
    public byte[] StickLeftY { get; set; } = [];
    public byte[] StickRightX { get; set; } = [];
    public byte[] StickRightY { get; set; } = [];
    public byte[] TriggerLeft { get; set; } = [];
    public byte[] TriggerRight { get; set; } = [];
    public byte[] Axis0Raw { get; set; } = [];
    public byte[] Axis1Raw { get; set; } = [];
    public byte[] Axis2Raw { get; set; } = [];
    public byte[] Axis3Raw { get; set; } = [];
    public byte[] Axis4Raw { get; set; } = [];
    public byte[] Axis5Raw { get; set; } = [];
    public ulong[] InputMap { get; set; } = [];
    public ulong[] InputMapToggle { get; set; } = [];

    protected override List<object> Serialize() => new()
    {
        PlayerIDs, StickLeftX, StickLeftY, StickRightX, StickRightY,
        TriggerLeft, TriggerRight, Axis0Raw, Axis1Raw, Axis2Raw,
        Axis3Raw, Axis4Raw, Axis5Raw, InputMap, InputMapToggle
    };
    protected override void Deserialize(List<object> fields)
    {
        PlayerIDs = (byte[])fields[0]; StickLeftX = (byte[])fields[1]; StickLeftY = (byte[])fields[2];
        StickRightX = (byte[])fields[3]; StickRightY = (byte[])fields[4];
        TriggerLeft = (byte[])fields[5]; TriggerRight = (byte[])fields[6];
        Axis0Raw = (byte[])fields[7]; Axis1Raw = (byte[])fields[8]; Axis2Raw = (byte[])fields[9];
        Axis3Raw = (byte[])fields[10]; Axis4Raw = (byte[])fields[11]; Axis5Raw = (byte[])fields[12];
        InputMap = (ulong[])fields[13]; InputMapToggle = (ulong[])fields[14];
    }
}

public class MsgTransformSyncToServer : PlayerIDMsg
{
    public byte[] IDs { get; set; } = [];
    public float[] PosX { get; set; } = [];
    public float[] PosY { get; set; } = [];
    public float[] PosZ { get; set; } = [];
    public float[] RotX { get; set; } = [];
    public float[] RotY { get; set; } = [];
    public float[] RotZ { get; set; } = [];
    public float[] VelX { get; set; } = [];
    public float[] VelY { get; set; } = [];
    public float[] VelZ { get; set; } = [];

    protected override List<object> Serialize()
    {
        var l = base.Serialize();
        l.Add(IDs); l.Add(PosX); l.Add(PosY); l.Add(PosZ);
        l.Add(RotX); l.Add(RotY); l.Add(RotZ);
        l.Add(VelX); l.Add(VelY); l.Add(VelZ);
        return l;
    }
    protected override void Deserialize(List<object> fields)
    {
        base.Deserialize(fields);
        IDs = (byte[])fields[1]; PosX = (float[])fields[2]; PosY = (float[])fields[3]; PosZ = (float[])fields[4];
        RotX = (float[])fields[5]; RotY = (float[])fields[6]; RotZ = (float[])fields[7];
        VelX = (float[])fields[8]; VelY = (float[])fields[9]; VelZ = (float[])fields[10];
    }
}

public class MsgTransformSyncToClient : DataObject
{
    public byte[] PlayerIDs { get; set; } = [];
    public byte[] IDs { get; set; } = [];
    public float[] PosX { get; set; } = [];
    public float[] PosY { get; set; } = [];
    public float[] PosZ { get; set; } = [];
    public float[] RotX { get; set; } = [];
    public float[] RotY { get; set; } = [];
    public float[] RotZ { get; set; } = [];
    public float[] VelX { get; set; } = [];
    public float[] VelY { get; set; } = [];
    public float[] VelZ { get; set; } = [];

    protected override List<object> Serialize() => new()
    {
        PlayerIDs, IDs, PosX, PosY, PosZ, RotX, RotY, RotZ, VelX, VelY, VelZ
    };
    protected override void Deserialize(List<object> fields)
    {
        PlayerIDs = (byte[])fields[0]; IDs = (byte[])fields[1];
        PosX = (float[])fields[2]; PosY = (float[])fields[3]; PosZ = (float[])fields[4];
        RotX = (float[])fields[5]; RotY = (float[])fields[6]; RotZ = (float[])fields[7];
        VelX = (float[])fields[8]; VelY = (float[])fields[9]; VelZ = (float[])fields[10];
    }
}

public class ChatToServerMsg : PlayerIDMsg
{
    public string Message { get; set; } = "";

    protected override List<object> Serialize() { var l = base.Serialize(); l.Add(Message); return l; }
    protected override void Deserialize(List<object> fields) { base.Deserialize(fields); Message = (string)fields[1]; }
}

public class ChatToClientMsg : DataObject
{
    public byte PlayerID { get; set; }
    public string Message { get; set; } = "";

    protected override List<object> Serialize() => new() { PlayerID, Message };
    protected override void Deserialize(List<object> fields) { PlayerID = (byte)fields[0]; Message = (string)fields[1]; }
}

public class PlayerList : DataObject
{
    public byte[] PlayerIDs { get; set; } = [];
    public string[] PlayerNames { get; set; } = [];
    public ulong[] CSteamIDs { get; set; } = [];
    public float[] Pings { get; set; } = [];

    protected override List<object> Serialize() => new() { PlayerIDs, PlayerNames, CSteamIDs, Pings };
    protected override void Deserialize(List<object> fields)
    {
        PlayerIDs = (byte[])fields[0]; PlayerNames = (string[])fields[1];
        CSteamIDs = (ulong[])fields[2]; Pings = (float[])fields[3];
    }
}

public class ServerInfoMsg : DataObject
{
    public ushort MaxPlayers { get; set; }
    public string ServerName { get; set; } = "";
    public string Password { get; set; } = "";

    protected override List<object> Serialize() => new() { MaxPlayers, ServerName, Password };
    protected override void Deserialize(List<object> fields)
    {
        MaxPlayers = (ushort)fields[0]; ServerName = (string)fields[1]; Password = (string)fields[2];
    }
}

public class MsgCheckPasswordToServer : DataObject
{
    public string Password { get; set; } = "";

    protected override List<object> Serialize() => new() { Password };
    protected override void Deserialize(List<object> fields) { Password = (string)fields[0]; }
}

public class MsgCheckPasswordToClient : DataObject
{
    public bool IsPasswordCorrect { get; set; }
    public bool IsNobleConnectServer { get; set; }

    protected override List<object> Serialize() => new() { IsPasswordCorrect, IsNobleConnectServer };
    protected override void Deserialize(List<object> fields) { IsPasswordCorrect = (bool)fields[0]; IsNobleConnectServer = (bool)fields[1]; }
}

public class MsgMultiplayerGameStateInfoToClient : DataObject
{
    public int MultiplayerGameState { get; set; }

    protected override List<object> Serialize() => new() { MultiplayerGameState };
    protected override void Deserialize(List<object> fields) { MultiplayerGameState = (int)fields[0]; }
}

public class MsgSetPlayerHostToClient : PlayerIDMsg
{
    public ulong CSteamIDHost { get; set; }

    protected override List<object> Serialize() { var l = base.Serialize(); l.Add(CSteamIDHost); return l; }
    protected override void Deserialize(List<object> fields) { base.Deserialize(fields); CSteamIDHost = (ulong)fields[1]; }
}

public class MsgIsPlainCSharpToClient : DataObject
{
    public bool IsPlainCSharpServer { get; set; }

    protected override List<object> Serialize() => new() { IsPlainCSharpServer };
    protected override void Deserialize(List<object> fields) { IsPlainCSharpServer = (bool)fields[0]; }
}

public class MsgGlobalTime : DataObject
{
    public float GlobalTime { get; set; }

    protected override List<object> Serialize() => new() { GlobalTime };
    protected override void Deserialize(List<object> fields) { GlobalTime = (float)fields[0]; }
}

public class MsgServerSettingsToClient : DataObject
{
    public byte[] SerializedServerSettings { get; set; } = [];

    protected override List<object> Serialize() => new() { SerializedServerSettings };
    protected override void Deserialize(List<object> fields) { SerializedServerSettings = (byte[])fields[0]; }
}

public class MsgPlayersGoingToRaceToClient : DataObject
{
    public byte[] PlayerIDsGoingToRace { get; set; } = [];
    public int BlacklistChallengeUniqueIDToStart { get; set; }
    public int AmountOfLapsToDrive { get; set; } = -1;
    public bool ShouldPlayerBeForcedToLoadDriveScene { get; set; }
    public int DamagePercentage { get; set; } = 100;
    public float MaxAcceleration { get; set; } = 200;
    public bool DownforceAllowed { get; set; } = true;
    public bool RepairingAllowed { get; set; } = true;
    public bool MapVoteAtEnd { get; set; } = true;
    public float RaceAutomaticallyStartsInSeconds { get; set; } = -1;
    public byte PlayMode { get; set; } = byte.MaxValue;
    public byte AmountOfRounds { get; set; } = byte.MaxValue;

    protected override List<object> Serialize() => new()
    {
        PlayerIDsGoingToRace, BlacklistChallengeUniqueIDToStart, AmountOfLapsToDrive,
        ShouldPlayerBeForcedToLoadDriveScene, DamagePercentage, MaxAcceleration,
        DownforceAllowed, RepairingAllowed, MapVoteAtEnd,
        RaceAutomaticallyStartsInSeconds, PlayMode, AmountOfRounds
    };
    protected override void Deserialize(List<object> fields)
    {
        PlayerIDsGoingToRace = (byte[])fields[0]; BlacklistChallengeUniqueIDToStart = (int)fields[1]; AmountOfLapsToDrive = (int)fields[2];
        ShouldPlayerBeForcedToLoadDriveScene = (bool)fields[3]; DamagePercentage = (int)fields[4]; MaxAcceleration = (float)fields[5];
        DownforceAllowed = (bool)fields[6]; RepairingAllowed = (bool)fields[7]; MapVoteAtEnd = (bool)fields[8];
        RaceAutomaticallyStartsInSeconds = (float)fields[9]; PlayMode = (byte)fields[10]; AmountOfRounds = (byte)fields[11];
    }
}

public class MsgCurrentIslandToClient : DataObject
{
    public int IslandUniqueID { get; set; }

    protected override List<object> Serialize() => new() { IslandUniqueID };
    protected override void Deserialize(List<object> fields) { IslandUniqueID = (int)fields[0]; }
}

public class MsgCurrentIslandToServer : DataObject
{
    public byte PlayerID { get; set; }
    public int IslandUniqueID { get; set; }

    protected override List<object> Serialize() => new() { PlayerID, IslandUniqueID };
    protected override void Deserialize(List<object> fields) { PlayerID = (byte)fields[0]; IslandUniqueID = (int)fields[1]; }
}

public class MsgIslandConfigToClient : DataObject
{
    public int IslandConfigUniqueID { get; set; }

    protected override List<object> Serialize() => new() { IslandConfigUniqueID };
    protected override void Deserialize(List<object> fields) { IslandConfigUniqueID = (int)fields[0]; }
}

public class MsgCurrentGameModeToClient : DataObject
{
    public int CurrentGameModeID { get; set; }

    protected override List<object> Serialize() => new() { CurrentGameModeID };
    protected override void Deserialize(List<object> fields) { CurrentGameModeID = (int)fields[0]; }
}

public class MsgMultiplayerGameStateToServer : DataObject
{
    public int MultiplayerGameState { get; set; }
    public int BlacklistChallengeUniqueIDToStart { get; set; }

    protected override List<object> Serialize() => new() { MultiplayerGameState, BlacklistChallengeUniqueIDToStart };
    protected override void Deserialize(List<object> fields) { MultiplayerGameState = (int)fields[0]; BlacklistChallengeUniqueIDToStart = (int)fields[1]; }
}

public class MsgAllPlayersThatAreReadyToClient : DataObject
{
    public byte[] PlayerIDsReady { get; set; } = [];

    protected override List<object> Serialize() => new() { PlayerIDsReady };
    protected override void Deserialize(List<object> fields) { PlayerIDsReady = (byte[])fields[0]; }
}

public class MsgReadyupToServer : PlayerIDMsg
{
    public bool IsPlayerReady { get; set; }

    protected override List<object> Serialize() { var l = base.Serialize(); l.Add(IsPlayerReady); return l; }
    protected override void Deserialize(List<object> fields) { base.Deserialize(fields); IsPlayerReady = (bool)fields[1]; }
}

public class MsgGameMenuStateToServer : PlayerIDMsg
{
    public byte GameMenuState { get; set; } = byte.MaxValue;

    protected override List<object> Serialize() { var l = base.Serialize(); l.Add(GameMenuState); return l; }
    protected override void Deserialize(List<object> fields) { base.Deserialize(fields); GameMenuState = (byte)fields[1]; }
}

public class MsgGameMenuStateToClient : DataObject
{
    public byte[] PlayerIDs { get; set; } = [];
    public byte[] GameMenuStates { get; set; } = [];

    protected override List<object> Serialize() => new() { PlayerIDs, GameMenuStates };
    protected override void Deserialize(List<object> fields) { PlayerIDs = (byte[])fields[0]; GameMenuStates = (byte[])fields[1]; }
}

public class MsgCharacterBytesToServer : PlayerIDMsg
{
    public byte[] CharacterBytes { get; set; } = [];
    public int BuildingPlatformUniqueID { get; set; }

    protected override List<object> Serialize() { var l = base.Serialize(); l.Add(CharacterBytes); l.Add(BuildingPlatformUniqueID); return l; }
    protected override void Deserialize(List<object> fields) { base.Deserialize(fields); CharacterBytes = (byte[])fields[1]; BuildingPlatformUniqueID = (int)fields[2]; }
}

public class MsgCharacterBytesToClient : MsgCharacterBytesToServer
{
}

public class MsgFirstPersonInfoToServer : PlayerIDMsg
{
    public Vec3 Position { get; set; }
    public Vec3 Rotation { get; set; }
    public bool IsRoamingInFirstPersonMode { get; set; }
    public byte[] CharacterBytes { get; set; } = [];

    protected override List<object> Serialize()
    {
        var l = base.Serialize();
        l.Add(Position); l.Add(Rotation); l.Add(IsRoamingInFirstPersonMode); l.Add(CharacterBytes);
        return l;
    }
    protected override void Deserialize(List<object> fields)
    {
        base.Deserialize(fields);
        Position = (Vec3)fields[1]; Rotation = (Vec3)fields[2];
        IsRoamingInFirstPersonMode = (bool)fields[3]; CharacterBytes = (byte[])fields[4];
    }
}

public class MsgFirstPersonInfoToClients : DataObject
{
    public Vec3[] Positions { get; set; } = [];
    public Vec3[] Rotations { get; set; } = [];
    public byte[] PlayerIDs { get; set; } = [];
    public bool[] IsRoamingInFirstPersonMode { get; set; } = [];
    public byte[] CharacterBytes { get; set; } = [];
    public byte PlayerIDWithCharacterBytes { get; set; } = byte.MaxValue;

    protected override List<object> Serialize() => new() { Positions, Rotations, PlayerIDs, IsRoamingInFirstPersonMode, CharacterBytes, PlayerIDWithCharacterBytes };
    protected override void Deserialize(List<object> fields)
    {
        Positions = (Vec3[])fields[0]; Rotations = (Vec3[])fields[1];
        PlayerIDs = (byte[])fields[2]; IsRoamingInFirstPersonMode = (bool[])fields[3];
        CharacterBytes = (byte[])fields[4]; PlayerIDWithCharacterBytes = (byte)fields[5];
    }
}

public class MultiplayerGeneralInfoMsgToServer : PlayerIDMsg
{
    public byte IngameMenuStateByte { get; set; } = byte.MaxValue;
    public int Elo { get; set; }
    public int XP { get; set; }
    public int BronzeMedals { get; set; }
    public int SilverMedals { get; set; }
    public int GoldMedals { get; set; }
    public int MultiplayerWins { get; set; }
    public int DistanceDriven { get; set; }
    public int PartsBuilt { get; set; }

    protected override List<object> Serialize()
    {
        var l = base.Serialize();
        l.Add(IngameMenuStateByte); l.Add(Elo); l.Add(XP); l.Add(BronzeMedals);
        l.Add(SilverMedals); l.Add(GoldMedals); l.Add(MultiplayerWins);
        l.Add(DistanceDriven); l.Add(PartsBuilt);
        return l;
    }
    protected override void Deserialize(List<object> fields)
    {
        base.Deserialize(fields);
        IngameMenuStateByte = (byte)fields[1]; Elo = (int)fields[2]; XP = (int)fields[3];
        BronzeMedals = (int)fields[4]; SilverMedals = (int)fields[5]; GoldMedals = (int)fields[6];
        MultiplayerWins = (int)fields[7]; DistanceDriven = (int)fields[8]; PartsBuilt = (int)fields[9];
    }
}

public class MultiplayerGeneralInfoMsgToClient : DataObject
{
    public byte[] PlayerIDs { get; set; } = [];
    public byte[] IngameMenuStateBytes { get; set; } = [];
    public int[] Elos { get; set; } = [];
    public int[] XPs { get; set; } = [];
    public int[] BronzeMedals { get; set; } = [];
    public int[] SilverMedals { get; set; } = [];
    public int[] GoldMedals { get; set; } = [];
    public int[] MultiplayerWins { get; set; } = [];
    public int[] DistancesDriven { get; set; } = [];
    public int[] PartsBuilt { get; set; } = [];

    protected override List<object> Serialize() => new()
    {
        PlayerIDs, IngameMenuStateBytes, Elos, XPs, BronzeMedals,
        SilverMedals, GoldMedals, MultiplayerWins, DistancesDriven, PartsBuilt
    };
    protected override void Deserialize(List<object> fields)
    {
        PlayerIDs = (byte[])fields[0]; IngameMenuStateBytes = (byte[])fields[1];
        Elos = (int[])fields[2]; XPs = (int[])fields[3]; BronzeMedals = (int[])fields[4];
        SilverMedals = (int[])fields[5]; GoldMedals = (int[])fields[6];
        MultiplayerWins = (int[])fields[7]; DistancesDriven = (int[])fields[8]; PartsBuilt = (int[])fields[9];
    }
}

public class MsgExcusePingToServer : PlayerIDMsg
{
    public float ExcuseTime { get; set; }

    protected override List<object> Serialize() { var l = base.Serialize(); l.Add(ExcuseTime); return l; }
    protected override void Deserialize(List<object> fields) { base.Deserialize(fields); ExcuseTime = (float)fields[1]; }
}

public class MsgCarSyncerGarageToServer : PlayerIDMsg
{
    public int BaguetteBytesLen { get; set; } = -1;
    public int BytesArrayIndex { get; set; } = -1;
    public byte[] Bytes { get; set; } = [];
    public int HashCode { get; set; }

    protected override List<object> Serialize()
    {
        var l = base.Serialize();
        l.Add(BaguetteBytesLen); l.Add(BytesArrayIndex); l.Add(Bytes); l.Add(HashCode);
        return l;
    }
    protected override void Deserialize(List<object> fields)
    {
        base.Deserialize(fields);
        BaguetteBytesLen = (int)fields[1]; BytesArrayIndex = (int)fields[2];
        Bytes = (byte[])fields[3]; HashCode = (int)fields[4];
    }
}

public class MsgCarSyncerGarageToClient : MsgCarSyncerGarageToServer
{
    public byte PlatformPositionIndex { get; set; } = byte.MaxValue;

    protected override List<object> Serialize() { var l = base.Serialize(); l.Add(PlatformPositionIndex); return l; }
    protected override void Deserialize(List<object> fields) { base.Deserialize(fields); PlatformPositionIndex = (byte)fields[5]; }
}

// --- Car spawn / loading messages ---

public class SetSpawnPointMsgToServer : PlayerIDMsg
{
    public int SpawnPointIndex { get; set; } = -1;
    public int FreeDriveSpawnPointUniqueID { get; set; }

    protected override List<object> Serialize() { var l = base.Serialize(); l.Add(SpawnPointIndex); l.Add(FreeDriveSpawnPointUniqueID); return l; }
    protected override void Deserialize(List<object> fields) { base.Deserialize(fields); SpawnPointIndex = (int)fields[1]; FreeDriveSpawnPointUniqueID = (int)fields[2]; }
}

public class SetSpawnPointMsgToClient : SetSpawnPointMsgToServer { }

public class MsgSpawnPointPoseToServer : PlayerIDMsg
{
    public float SpawnPosX { get; set; }
    public float SpawnPosY { get; set; }
    public float SpawnPosZ { get; set; }
    public float SpawnRotX { get; set; }
    public float SpawnRotY { get; set; }
    public float SpawnRotZ { get; set; }

    protected override List<object> Serialize()
    {
        var l = base.Serialize();
        l.Add(SpawnPosX); l.Add(SpawnPosY); l.Add(SpawnPosZ);
        l.Add(SpawnRotX); l.Add(SpawnRotY); l.Add(SpawnRotZ);
        return l;
    }
    protected override void Deserialize(List<object> fields)
    {
        base.Deserialize(fields);
        SpawnPosX = (float)fields[1]; SpawnPosY = (float)fields[2]; SpawnPosZ = (float)fields[3];
        SpawnRotX = (float)fields[4]; SpawnRotY = (float)fields[5]; SpawnRotZ = (float)fields[6];
    }
}

public class MsgCarDataStateOfSelfToServer : PlayerIDMsg
{
    public string CarFileName { get; set; } = "";
    public int BaguetteBytesLen { get; set; } = -1;
    public int CCCBytesLen { get; set; } = -1;

    protected override List<object> Serialize()
    {
        var l = base.Serialize();
        l.Add(CarFileName); l.Add(BaguetteBytesLen); l.Add(CCCBytesLen);
        return l;
    }
    protected override void Deserialize(List<object> fields)
    {
        base.Deserialize(fields);
        CarFileName = (string)fields[1]; BaguetteBytesLen = (int)fields[2]; CCCBytesLen = (int)fields[3];
    }
}

public class MsgCarDataToServer : PlayerIDMsg
{
    public bool IsBaguetteFile { get; set; }
    public string CarFileName { get; set; } = "";
    public int BaguetteBytesLen { get; set; } = -1;
    public int CCCBytesLen { get; set; } = -1;
    public int BytesArrayIndex { get; set; } = -1;
    public byte[] Bytes { get; set; } = [];
    public int HashCode { get; set; }

    protected override List<object> Serialize()
    {
        var l = base.Serialize();
        l.Add(IsBaguetteFile); l.Add(CarFileName); l.Add(BaguetteBytesLen);
        l.Add(CCCBytesLen); l.Add(BytesArrayIndex); l.Add(Bytes); l.Add(HashCode);
        return l;
    }
    protected override void Deserialize(List<object> fields)
    {
        base.Deserialize(fields);
        IsBaguetteFile = (bool)fields[1]; CarFileName = (string)fields[2];
        BaguetteBytesLen = (int)fields[3]; CCCBytesLen = (int)fields[4];
        BytesArrayIndex = (int)fields[5]; Bytes = (byte[])fields[6]; HashCode = (int)fields[7];
    }
}

public class MsgCarDataToClient : MsgCarDataToServer { }

public class MsgRequestCarDataToClient : DataObject
{
    public byte PlayerID { get; set; }
    public bool IsBaguetteFile { get; set; }
    public string CarFileName { get; set; } = "";
    public int BaguetteBytesLen { get; set; } = -1;
    public int CCCBytesLen { get; set; } = -1;
    public int BytesArrayIndex { get; set; } = -1;
    public byte PlayerIDOfWhomCarIsRequested { get; set; } = byte.MaxValue;

    protected override List<object> Serialize() => new() { PlayerID, IsBaguetteFile, CarFileName, BaguetteBytesLen, CCCBytesLen, BytesArrayIndex, PlayerIDOfWhomCarIsRequested };
    protected override void Deserialize(List<object> fields)
    {
        PlayerID = (byte)fields[0]; IsBaguetteFile = (bool)fields[1]; CarFileName = (string)fields[2];
        BaguetteBytesLen = (int)fields[3]; CCCBytesLen = (int)fields[4]; BytesArrayIndex = (int)fields[5];
        PlayerIDOfWhomCarIsRequested = (byte)fields[6];
    }
}

public class MsgRequestCarDataToServer : PlayerIDMsg
{
    public bool IsBaguetteFile { get; set; }
    public string CarFileName { get; set; } = "";
    public int BaguetteBytesLen { get; set; } = -1;
    public int CCCBytesLen { get; set; } = -1;
    public int BytesArrayIndex { get; set; } = -1;
    public byte PlayerIDOfWhomCarIsRequested { get; set; } = byte.MaxValue;

    protected override List<object> Serialize()
    {
        var l = base.Serialize();
        l.Add(IsBaguetteFile); l.Add(CarFileName); l.Add(BaguetteBytesLen);
        l.Add(CCCBytesLen); l.Add(BytesArrayIndex); l.Add(PlayerIDOfWhomCarIsRequested);
        return l;
    }
    protected override void Deserialize(List<object> fields)
    {
        base.Deserialize(fields);
        IsBaguetteFile = (bool)fields[1]; CarFileName = (string)fields[2];
        BaguetteBytesLen = (int)fields[3]; CCCBytesLen = (int)fields[4];
        BytesArrayIndex = (int)fields[5]; PlayerIDOfWhomCarIsRequested = (byte)fields[6];
    }
}

public class MsgCarsLoadingStateToClient : DataObject
{
    public byte[] PlayerIDs { get; set; } = [];
    public float[] LoadingPercentages { get; set; } = [];

    protected override List<object> Serialize() => new() { PlayerIDs, LoadingPercentages };
    protected override void Deserialize(List<object> fields)
    {
        PlayerIDs = (byte[])fields[0]; LoadingPercentages = (float[])fields[1];
    }
}

public class MsgPlayersReadyListToClient : DataObject
{
    public byte[] PlayerIDs { get; set; } = [];
    public bool[] PlayersReadyStates { get; set; } = [];
    public float WaitTimeAllAreReadyCounter { get; set; } = -1f;

    protected override List<object> Serialize() => new() { PlayerIDs, PlayersReadyStates, WaitTimeAllAreReadyCounter };
    protected override void Deserialize(List<object> fields)
    {
        PlayerIDs = (byte[])fields[0]; PlayersReadyStates = (bool[])fields[1]; WaitTimeAllAreReadyCounter = (float)fields[2];
    }
}

public class ReverseMsgToServer : PlayerIDMsg
{
    public bool IsReversing { get; set; }

    protected override List<object> Serialize() { var l = base.Serialize(); l.Add(IsReversing); return l; }
    protected override void Deserialize(List<object> fields) { base.Deserialize(fields); IsReversing = (bool)fields[1]; }
}

public class ShiftGearServerMsg : PlayerIDMsg
{
    public bool IsManuallyShifting { get; set; }
    public int Gear { get; set; } = -1;

    protected override List<object> Serialize() { var l = base.Serialize(); l.Add(IsManuallyShifting); l.Add(Gear); return l; }
    protected override void Deserialize(List<object> fields) { base.Deserialize(fields); IsManuallyShifting = (bool)fields[1]; Gear = (int)fields[2]; }
}

public class RepairMsg : PlayerIDMsg
{
    public bool IsRepairInsteadOfReset { get; set; }

    protected override List<object> Serialize() { var l = base.Serialize(); l.Add(IsRepairInsteadOfReset); return l; }
    protected override void Deserialize(List<object> fields) { base.Deserialize(fields); IsRepairInsteadOfReset = (bool)fields[1]; }
}

public class RepairClientMsg : PlayerIDMsg
{
    public bool IsRepairInsteadOfReset { get; set; }

    protected override List<object> Serialize() { var l = base.Serialize(); l.Add(IsRepairInsteadOfReset); return l; }
    protected override void Deserialize(List<object> fields) { base.Deserialize(fields); IsRepairInsteadOfReset = (bool)fields[1]; }
}

public class ReverseMsgToClient : DataObject
{
    public byte[] PlayerIDs { get; set; } = [];
    public bool[] AreReversing { get; set; } = [];

    protected override List<object> Serialize() => new() { PlayerIDs, AreReversing };
    protected override void Deserialize(List<object> fields) { PlayerIDs = (byte[])fields[0]; AreReversing = (bool[])fields[1]; }
}

public class ShiftGearClientMsg : DataObject
{
    public byte[] PlayerIDs { get; set; } = [];
    public bool[] IsManuallyShifting { get; set; } = [];
    public int[] Gear { get; set; } = [];

    protected override List<object> Serialize() => new() { PlayerIDs, IsManuallyShifting, Gear };
    protected override void Deserialize(List<object> fields)
    {
        PlayerIDs = (byte[])fields[0]; IsManuallyShifting = (bool[])fields[1]; Gear = (int[])fields[2];
    }
}

public class MsgCarDataStateAllPlayersToClient : DataObject
{
    public string[] CarFileNames { get; set; } = [];
    public byte[] PlayerIDs { get; set; } = [];
    public int[] BaguetteBytesLen { get; set; } = [];
    public int[] CCCBytesLen { get; set; } = [];
    public bool[] IsServerDataUpToDate { get; set; } = [];

    protected override List<object> Serialize() => new() { CarFileNames, PlayerIDs, BaguetteBytesLen, CCCBytesLen, IsServerDataUpToDate };
    protected override void Deserialize(List<object> fields)
    {
        CarFileNames = (string[])fields[0]; PlayerIDs = (byte[])fields[1];
        BaguetteBytesLen = (int[])fields[2]; CCCBytesLen = (int[])fields[3];
        IsServerDataUpToDate = (bool[])fields[4];
    }
}

public class MsgDestroyPartsToServer : PlayerIDMsg
{
    public int PartParentID { get; set; } = -1;
    public int[] DestroyedPartsIDs { get; set; } = [];
    public Vec3 CarVelocity { get; set; }

    protected override List<object> Serialize() { var l = base.Serialize(); l.Add(PartParentID); l.Add(DestroyedPartsIDs); l.Add(CarVelocity); return l; }
    protected override void Deserialize(List<object> fields) { base.Deserialize(fields); PartParentID = (int)fields[1]; DestroyedPartsIDs = (int[])fields[2]; CarVelocity = (Vec3)fields[3]; }
}

public class MsgDestroyPartsToClient : MsgDestroyPartsToServer { }

public class DestroyedPartsMsg : PlayerIDMsg
{
    public int[] InstPartIDs { get; set; } = [];
    public Vec3 ImpactVector { get; set; }
    public byte PlayerIDCollidingWith { get; set; } = byte.MaxValue;
    public Vec3 DamagePosWorld { get; set; }

    protected override List<object> Serialize()
    {
        var l = base.Serialize();
        l.Add(InstPartIDs); l.Add(ImpactVector); l.Add(PlayerIDCollidingWith); l.Add(DamagePosWorld);
        return l;
    }
    protected override void Deserialize(List<object> fields)
    {
        base.Deserialize(fields);
        InstPartIDs = (int[])fields[1]; ImpactVector = (Vec3)fields[2];
        PlayerIDCollidingWith = (byte)fields[3]; DamagePosWorld = (Vec3)fields[4];
    }
}

public class SelfCarStateMsg : PlayerIDMsg
{
    public byte CarStateMode { get; set; }
    public Vec3 Position { get; set; }
    public Vec3 Rotation { get; set; }

    protected override List<object> Serialize()
    {
        var l = base.Serialize();
        l.Add(CarStateMode); l.Add(Position); l.Add(Rotation);
        return l;
    }
    protected override void Deserialize(List<object> fields)
    {
        base.Deserialize(fields);
        CarStateMode = (byte)fields[1]; Position = (Vec3)fields[2]; Rotation = (Vec3)fields[3];
    }
}

public class AllPlayerCars : DataObject
{
    public byte[] PlayerIDs { get; set; } = [];
    public string[] CarFileNames { get; set; } = [];

    protected override List<object> Serialize() => new() { PlayerIDs, CarFileNames };
    protected override void Deserialize(List<object> fields) { PlayerIDs = (byte[])fields[0]; CarFileNames = (string[])fields[1]; }
}

public class MsgSelfReadyToServer : PlayerIDMsg
{
    public bool IsSelfReady { get; set; }

    protected override List<object> Serialize() { var l = base.Serialize(); l.Add(IsSelfReady); return l; }
    protected override void Deserialize(List<object> fields) { base.Deserialize(fields); IsSelfReady = (bool)fields[1]; }
}

public class MsgPerformHornToServer : PlayerIDMsg
{
    public int SoundIndex { get; set; } = -1;
    public int PartInstID { get; set; } = -1;
    public Vec3 WorldPosition { get; set; }

    protected override List<object> Serialize() { var l = base.Serialize(); l.Add(SoundIndex); l.Add(PartInstID); l.Add(WorldPosition); return l; }
    protected override void Deserialize(List<object> fields) { base.Deserialize(fields); SoundIndex = (int)fields[1]; PartInstID = (int)fields[2]; WorldPosition = (Vec3)fields[3]; }
}

public class MsgPerformHornToClient : MsgPerformHornToServer { }

public class MsgCommandToServer : PlayerIDMsg
{
    public string[] CommandArgs { get; set; } = [];

    protected override List<object> Serialize() { var l = base.Serialize(); l.Add(CommandArgs); return l; }
    protected override void Deserialize(List<object> fields) { base.Deserialize(fields); CommandArgs = (string[])fields[1]; }
}

public class AllRigsInfoMsg : DataObject
{
    public byte[] PlayerIDs { get; set; } = [];
    public ushort[] RigidbodyIDs { get; set; } = [];
    public Vec3[] Positions { get; set; } = [];
    public Vec3[] Rotations { get; set; } = [];
    public Vec3[] Velocities { get; set; } = [];
    public Vec3[] AngularVelocities { get; set; } = [];
    public float GlobalTime { get; set; }

    protected override List<object> Serialize() => new()
    {
        PlayerIDs, RigidbodyIDs, Positions, Rotations, Velocities, AngularVelocities, GlobalTime
    };
    protected override void Deserialize(List<object> fields)
    {
        PlayerIDs = (byte[])fields[0]; RigidbodyIDs = (ushort[])fields[1];
        Positions = (Vec3[])fields[2]; Rotations = (Vec3[])fields[3];
        Velocities = (Vec3[])fields[4]; AngularVelocities = (Vec3[])fields[5];
        GlobalTime = (float)fields[6];
    }
}
