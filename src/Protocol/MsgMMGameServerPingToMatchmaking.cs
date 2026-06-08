using System.Collections.Generic;

namespace Rivet.Protocol;

public class MsgMMGameServerPingToMatchmaking : DataObject
{
    public string SelfPublicIP { get; set; } = "";
    public int SelfPublicPort { get; set; } = -1;
    public string ServerName { get; set; } = "";
    public int MaxPlayers { get; set; } = -1;
    public int CurrentPlayers { get; set; } = -1;
    public string GameVersion { get; set; } = "";
    public string MetaInfo1 { get; set; } = "";
    public string MetaInfo2 { get; set; } = "";
    public bool HasPassword { get; set; }
    public string Description { get; set; } = "";

    protected override List<object> Serialize()
    {
        return
        [
            SelfPublicIP,
            SelfPublicPort,
            ServerName,
            MaxPlayers,
            CurrentPlayers,
            GameVersion,
            MetaInfo1,
            MetaInfo2,
            HasPassword,
            Description
        ];
    }
}
