# Rivet

Byte-exact C# reimplementation of the Street (formerly Project Vroom Vroom) dedicated server protocol. No Unity dependency. Runs on Linux, Windows, macOS.

## Quick Start

```bash
# Build
./build.sh

# Run
dotnet run --project src/Rivet.csproj -- -port 25000 -servername "My Server" -maxplayers 8 -password ""
```

Or via Docker:
```bash
docker build -t rivet-server .
docker run -p 25000:25000/udp -e SV_PORT=25000 -e SV_NAME="My Server" rivet-server
```

## CLI Arguments

| Arg | Env Var | Default | Description |
|---|---|---|---|
| `-port` | `SV_PORT` | `25000` | UDP listen port |
| `-maxplayers` | `SV_MAXPLAYERS` | `8` | Max players |
| `-servername` | `SV_NAME` | `"Rivet Server"` | Server display name |
| `-password` | `SV_PASSWORD` | `""` | Server password (empty = no password) |

## Console Commands

| Command | Description |
|---|---|
| `help` | Show commands |
| `status` | Server uptime + player count |
| `players` | List connected players |
| `say <msg>` | Broadcast message to all |
| `kick <id>` | Kick player by ID |
| `setname <name>` | Change server name |
| `quit` | Shutdown |

## Architecture

```
src/
├── Program.cs              # Entry point, message registration
├── Config.cs               # Config from CLI args + env vars
├── Protocol/
│   ├── PrimitiveType.cs    # 22-type serialization system
│   ├── DataObject.cs       # Base class: ToBytes/ParseBytes (byte-exact)
│   ├── MessageIds.cs       # All 161 message ID constants
│   ├── MessageRegistry.cs  # Type ↔ ID mapping
│   └── MessageTypes.cs     # All DataObject subclass definitions
├── Network/
│   ├── UdpTransport.cs     # Raw UDP socket receive thread
│   └── PacketProtocol.cs   # Protocol framing (0xFE/0xFF markers, ACK)
└── Server/
    ├── GameServer.cs       # Main server: message dispatch, relay, commands
    └── PlayerManager.cs    # Player tracking, timeout, ID assignment
```

## How To Add Features

### 1. Add a new message handler

In `GameServer.cs`, add one line in `RegisterAllMessages()`:

```csharp
Register<MyNewMsg>(MsgId.SomeMsgId, HandleMyNewMsg);
```

Then add the handler method:

```csharp
private void HandleMyNewMsg(IPEndPoint ep, MyNewMsg msg)
{
    // msg has all the deserialized fields
    Send(ep, true, new ResponseMsg { ... });
}
```

### 2. Add a new message type

In `MessageTypes.cs`:
```csharp
public class MyNewMsg : DataObject
{
    public string Foo { get; set; } = "";
    public int Bar { get; set; }

    protected override List<object> Serialize() => new() { Foo, Bar };
    protected override void Deserialize(List<object> fields)
    {
        Foo = (string)fields[0];
        Bar = (int)fields[1];
    }
}
```

Add the ID in `MessageIds.cs`, register in `Program.cs`.

### 3. Add a console command

In `GameServer.HandleConsoleCommand()`, add a case:
```csharp
case "mycommand":
    Console.WriteLine("doing thing");
    break;
```

### 4. Add a chat command

In `GameServer.HandleCommand()`, add a case:
```csharp
case "/mycommand":
    SendChat(player.EndPoint, "Result");
    break;
```

### 5. Add web dashboard / live map

The `GameServer` exposes player positions via `_players` list. Add a WebSocket/HTTP server that reads from it. The `OnRawMessage` callback in `PacketProtocol` already captures all `MsgTransformSyncToServer` data with positions.

## Protocol Compatibility

This server speaks the exact same wire protocol as the original game. Unmodded game clients connect without any changes.

Messages the server currently handles:
- `ConnectMsg` / `ConnectAnswer` — connection handshake
- `DisconnectMsg` — disconnect
- `PingMsgToServer` / `PingMsg` — keepalive
- `InputsMsg` → `AllInputsMsg` — input relay
- `MsgTransformSyncToServer` → `MsgTransformSyncToClient` — transform relay
- `ChatToServerMsg` → `ChatToClientMsg` — chat relay
- `MsgCheckPasswordToServer` / `MsgCheckPasswordToClient` — password auth
- `PlayerList` — periodic broadcast
- `ServerInfoMsg` — periodic server info
- `MsgSetPlayerHostToClient` — host assignment
- `MsgIsPlainCSharpToClient` — plain C# flag
- `MsgGlobalTime` — time sync
- `MsgServerSettingsToClient` — settings broadcast
- `MsgMultiplayerGameStateInfoToClient` — game state broadcast

Unhandled messages (sent by client, silently ignored by server):
- Building/garage sync (car data, figures, platforms)
- Challenge management (ready up, results, checkpoints)
- King mode, Crash Derby, Racing, and other game mode messages
- First person camera, horns, engine sounds
- Map voting, tournament, championship messages

These can be added incrementally without breaking existing functionality.
