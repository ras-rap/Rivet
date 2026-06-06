using System;
using System.Linq;
using Rivet;
using Rivet.Protocol;
using Rivet.Server;

// Register all message types with their IDs
RegisterMessages();

var config = Config.LoadOrDefault();
config.ApplyArgs(args);
Console.WriteLine($"[Config] Loaded {config.ConfigPath}");
var server = new GameServer(config);

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\nShutting down...");
    server.Dispose();
    Environment.Exit(0);
};

server.Run();

static void RegisterMessages()
{
    MessageRegistry.Register<ConnectMsg>(MsgId.ConnectMsg);
    MessageRegistry.Register<ConnectAnswer>(MsgId.ConnectAnswer);
    MessageRegistry.Register<PlayerIDMsg>(MsgId.PlayerIDMsg);
    MessageRegistry.Register<DisconnectMsg>(MsgId.DisconnectMsg);
    MessageRegistry.Register<PingMsgToServer>(MsgId.PingMsgToServer);
    MessageRegistry.Register<PingMsg>(MsgId.PingMsg);
    MessageRegistry.Register<InputsMsg>(MsgId.InputsMsg);
    MessageRegistry.Register<AllInputsMsg>(MsgId.AllInputsMsg);
    MessageRegistry.Register<MsgTransformSyncToServer>(MsgId.MsgTransformSyncToServer);
    MessageRegistry.Register<MsgTransformSyncToClient>(MsgId.MsgTransformSyncToClient);
    MessageRegistry.Register<ChatToServerMsg>(MsgId.ChatToServerMsg);
    MessageRegistry.Register<ChatToClientMsg>(MsgId.ChatToClientMsg);
    MessageRegistry.Register<PlayerList>(MsgId.PlayerList);
    MessageRegistry.Register<ServerInfoMsg>(MsgId.ServerInfoMsg);
    MessageRegistry.Register<MsgCheckPasswordToServer>(MsgId.MsgCheckPasswordToServer);
    MessageRegistry.Register<MsgCheckPasswordToClient>(MsgId.MsgCheckPasswordToClient);
    MessageRegistry.Register<MsgMultiplayerGameStateInfoToClient>(MsgId.MsgMultiplayerGameStateInfoToClient);
    MessageRegistry.Register<MsgSetPlayerHostToClient>(MsgId.MsgSetPlayerHostToClient);
    MessageRegistry.Register<MsgIsPlainCSharpToClient>(MsgId.MsgIsPlainCSharpToClient);
    MessageRegistry.Register<MsgGlobalTime>(MsgId.MsgGlobalTime);
    MessageRegistry.Register<MsgServerSettingsToClient>(MsgId.MsgServerSettingsToClient);
    MessageRegistry.Register<MsgPlayersGoingToRaceToClient>(MsgId.MsgPlayersGoingToRaceToClient);
    MessageRegistry.Register<MsgCurrentIslandToClient>(MsgId.MsgCurrentIslandToClient);
    MessageRegistry.Register<MsgCurrentIslandToServer>(MsgId.MsgCurrentIslandToServer);
    MessageRegistry.Register<MsgIslandConfigToClient>(MsgId.MsgIslandConfigToClient);
    MessageRegistry.Register<MsgCurrentGameModeToClient>(MsgId.MsgCurrentGameModeToClient);
    MessageRegistry.Register<MsgMultiplayerGameStateToServer>(MsgId.MsgMultiplayerGameStateToServer);
    MessageRegistry.Register<MsgAllPlayersThatAreReadyToClient>(MsgId.MsgAllPlayersThatAreReadyToClient);
    MessageRegistry.Register<MsgReadyupToServer>(MsgId.MsgReadyupToServer);
    MessageRegistry.Register<MsgGameMenuStateToServer>(MsgId.MsgGameMenuStateToServer);
    MessageRegistry.Register<MsgGameMenuStateToClient>(MsgId.MsgGameMenuStateToClient);
    MessageRegistry.Register<MsgCharacterBytesToServer>(MsgId.MsgCharacterBytesToServer);
    MessageRegistry.Register<MsgCharacterBytesToClient>(MsgId.MsgCharacterBytesToClient);
    MessageRegistry.Register<MsgFirstPersonInfoToServer>(MsgId.MsgFirstPersonInfoToServer);
    MessageRegistry.Register<MsgFirstPersonInfoToClients>(MsgId.MsgFirstPersonInfoToClients);
    MessageRegistry.Register<MultiplayerGeneralInfoMsgToServer>(MsgId.MultiplayerGeneralInfoMsgToServer);
    MessageRegistry.Register<MultiplayerGeneralInfoMsgToClient>(MsgId.MultiplayerGeneralInfoMsgToClient);
    MessageRegistry.Register<MsgExcusePingToServer>(MsgId.MsgExcusePingToServer);
    MessageRegistry.Register<MsgCarSyncerGarageToServer>(MsgId.MsgCarSyncerGarageToServer);
    MessageRegistry.Register<MsgCarSyncerGarageToClient>(MsgId.MsgCarSyncerGarageToClient);
    MessageRegistry.Register<MsgSpawnPointPoseToServer>(MsgId.MsgSpawnPointPoseToServer);
    MessageRegistry.Register<MsgCarDataStateOfSelfToServer>(MsgId.MsgCarDataStateOfSelfToServer);
    MessageRegistry.Register<MsgCarDataToServer>(MsgId.MsgCarDataToServer);
    MessageRegistry.Register<MsgCarDataToClient>(MsgId.MsgCarDataToClient);
    MessageRegistry.Register<MsgRequestCarDataToServer>(MsgId.MsgRequestCarDataToServer);
    MessageRegistry.Register<MsgRequestCarDataToClient>(MsgId.MsgRequestCarDataToClient);
    MessageRegistry.Register<MsgCarsLoadingStateToClient>(MsgId.MsgCarsLoadingStateToClient);
    MessageRegistry.Register<MsgPlayersReadyListToClient>(MsgId.MsgPlayersReadyListToClient);
    MessageRegistry.Register<ReverseMsgToServer>(MsgId.ReverseMsgToServer);
    MessageRegistry.Register<ShiftGearServerMsg>(MsgId.ShiftGearServerMsg);
    MessageRegistry.Register<ReverseMsgToClient>(MsgId.ReverseMsgToClient);
    MessageRegistry.Register<SetSpawnPointMsgToClient>(MsgId.SetSpawnPointMsgToClient);
    MessageRegistry.Register<MsgCarDataStateAllPlayersToClient>(MsgId.MsgCarDataStateAllPlayersToClient);
    MessageRegistry.Register<RepairMsg>(MsgId.RepairMsg);
    MessageRegistry.Register<RepairClientMsg>(MsgId.RepairClientMsg);
    MessageRegistry.Register<MsgDestroyPartsToServer>(MsgId.MsgDestroyPartsToServer);
    MessageRegistry.Register<MsgDestroyPartsToClient>(MsgId.MsgDestroyPartsToClient);
    MessageRegistry.Register<DestroyedPartsMsg>(MsgId.DestroyedPartsMsg);
    MessageRegistry.Register<SelfCarStateMsg>(MsgId.SelfCarStateMsg);
    MessageRegistry.Register<AllPlayerCars>(MsgId.AllPlayerCars);
    MessageRegistry.Register<MsgSelfReadyToServer>(MsgId.MsgSelfReadyToServer);
    MessageRegistry.Register<MsgPerformHornToServer>(MsgId.MsgPerformHornToServer);
    MessageRegistry.Register<MsgPerformHornToClient>(MsgId.MsgPerformHornToClient);
    MessageRegistry.Register<MsgCommandToServer>(MsgId.MsgCommandToServer);
    MessageRegistry.Register<SetSpawnPointMsgToServer>(MsgId.SetSpawnPointMsgToServer);
}
