using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Rivet.Server;

public class ApiServer : IDisposable
{
    private readonly GameServer _server;
    private readonly Config _config;
    private readonly LogWriter _log;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private bool _running;

    public ApiServer(GameServer server, Config config, LogWriter log)
    {
        _server = server;
        _config = config;
        _log = log;
    }

    public void Start()
    {
        if (string.IsNullOrEmpty(_config.ApiKey) || _config.ApiPort <= 0) return;

        try
        {
            _listener = new TcpListener(IPAddress.Any, _config.ApiPort);
            _listener.Start();
            _running = true;
            _cts = new CancellationTokenSource();
            _ = ListenLoop();
            _log.Info($"[API] Listening on port {_config.ApiPort}");
        }
        catch (Exception ex)
        {
            _log.Warn($"[API] Failed to start: {ex.Message}");
        }
    }

    private async Task ListenLoop()
    {
        while (_running)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync();
                _ = HandleClient(client);
            }
            catch (ObjectDisposedException) { break; }
            catch { if (_running) break; }
        }
    }

    private async Task HandleClient(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            try
            {
                var buf = new byte[8192];
                var offset = 0;

                while (offset < buf.Length)
                {
                    var read = await stream.ReadAsync(buf, offset, buf.Length - offset);
                    if (read == 0) return;
                    offset += read;

                    var headerEnd = Search(buf, offset, "\r\n\r\n");
                    if (headerEnd >= 0)
                    {
                        var headerStr = Encoding.ASCII.GetString(buf, 0, headerEnd);
                        var bodyStart = headerEnd + 4;
                        var bodyLen = offset - bodyStart;

                        var lines = headerStr.Split("\r\n");
                        var reqLine = lines[0].Split(' ');
                        var method = reqLine.Length > 0 ? reqLine[0] : "";
                        var path = reqLine.Length > 1 ? reqLine[1] : "";

                        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 1; i < lines.Length; i++)
                        {
                            var colon = lines[i].IndexOf(':');
                            if (colon > 0)
                                headers[lines[i][..colon].Trim()] = lines[i][(colon + 1)..].Trim();
                        }

                        // Handle Content-Length for POST body
                        if (headers.TryGetValue("Content-Length", out var clStr) && int.TryParse(clStr, out var cl) && cl > bodyLen)
                        {
                            while (offset < buf.Length && bodyLen < cl)
                            {
                                var more = await stream.ReadAsync(buf, offset, buf.Length - offset);
                                if (more == 0) break;
                                offset += more;
                                bodyLen = offset - bodyStart;
                            }
                        }

                        var body = bodyLen > 0 ? Encoding.UTF8.GetString(buf, bodyStart, bodyLen) : "";

                        var auth = headers.GetValueOrDefault("Authorization", "");

                        if (path.StartsWith("/api/ws") && headers.TryGetValue("Upgrade", out var upgrade) && upgrade.Contains("websocket", StringComparison.OrdinalIgnoreCase))
                        {
                            await HandleWebSocket(path, headers, stream);
                            return;
                        }

                        await HandleRequest(method, path, body, auth, stream);
                        return;
                    }

                    if (offset >= buf.Length)
                    {
                        await Respond(stream, 413, "application/json", "{\"error\":\"Request too large\"}");
                        return;
                    }
                }
            }
            catch { }
        }
    }

    private async Task HandleWebSocket(string path, Dictionary<string, string> headers, NetworkStream stream)
    {
        if (!headers.TryGetValue("Sec-WebSocket-Key", out var key)) return;

        // Check for token in query if auth header is missing
        var token = "";
        if (headers.TryGetValue("Authorization", out var auth) && auth.StartsWith("Bearer "))
            token = auth[7..];
        else if (path.Contains("token="))
        {
            var parts = path.Split("token=");
            if (parts.Length > 1)
                token = parts[1].Split('&')[0];
        }

        if (string.IsNullOrEmpty(_config.ApiKey) || token != _config.ApiKey)
        {
            await Respond(stream, 401, "application/json", "{\"error\":\"Unauthorized\"}");
            return;
        }

        var accept = ComputeWebSocketAccept(key);
        var response = "HTTP/1.1 101 Switching Protocols\r\n" +
                       "Upgrade: websocket\r\n" +
                       "Connection: Upgrade\r\n" +
                       $"Sec-WebSocket-Accept: {accept}\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response));

        try
        {
            using var ws = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true });

            while (ws.State == WebSocketState.Open && _running)
            {
                var data = _server.ApiGetPlayerPositions();
                var json = JsonSerializer.Serialize(data);
                var bytes = Encoding.UTF8.GetBytes(json);
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                await Task.Delay(500); // 2 updates per second
            }
        }
        catch { }
    }

    private static string ComputeWebSocketAccept(string key)
    {
        const string guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        var combined = key + guid;
        var hash = System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToBase64String(hash);
    }

    private static int Search(byte[] buf, int len, string pattern)
    {
        var p = Encoding.ASCII.GetBytes(pattern);
        for (int i = 0; i <= len - p.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < p.Length; j++)
                if (buf[i + j] != p[j]) { match = false; break; }
            if (match) return i;
        }
        return -1;
    }

    public string WebRoot { get; set; } = "web/dist";

    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".html", "text/html" },
        { ".js", "application/javascript" },
        { ".css", "text/css" },
        { ".json", "application/json" },
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".svg", "image/svg+xml" },
        { ".ico", "image/x-icon" }
    };

    private async Task HandleRequest(string method, string path, string body, string auth, NetworkStream stream)
    {
        path = path.TrimEnd('/');
        if (string.IsNullOrEmpty(path)) path = "/index.html";
        
        // API routes
        if (path.StartsWith("/api/"))
        {
            await HandleApiRequest(method, path, body, auth, stream);
            return;
        }

        // Static files
        if (method == "GET")
        {
            var localPath = Path.Combine(WebRoot, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            
            // SPA fallback: if file doesn't exist and doesn't look like a file, serve index.html
            if (!File.Exists(localPath) && !path.Contains('.'))
                localPath = Path.Combine(WebRoot, "index.html");

            if (File.Exists(localPath))
            {
                var ext = Path.GetExtension(localPath);
                var mime = MimeTypes.GetValueOrDefault(ext, "application/octet-stream");
                var bytes = await File.ReadAllBytesAsync(localPath);
                await RespondRaw(stream, 200, mime, bytes);
                return;
            }
        }

        await Respond(stream, 404, "application/json", "{\"error\":\"Not found\"}");
    }

    private async Task HandleApiRequest(string method, string path, string body, string auth, NetworkStream stream)
    {
        // Public routes or OPTIONS for CORS
        bool isPublic = (method == "GET" && (path == "/api/stats" || path == "/api/players")) || method == "OPTIONS";

        if (!isPublic && !Authenticate(auth))
        {
            await Respond(stream, 401, "application/json", "{\"error\":\"Unauthorized\"}");
            return;
        }

        try
        {
            switch (method)
            {
                case "GET":
                    switch (path)
                    {
                        case "/api/stats":
                            await Respond(stream, 200, "application/json", JsonSerializer.Serialize(_server.ApiGetStats()));
                            return;
                        case "/api/players":
                            await Respond(stream, 200, "application/json", JsonSerializer.Serialize(_server.ApiGetPlayers()));
                            return;
                        case "/api/positions":
                            await Respond(stream, 200, "application/json", JsonSerializer.Serialize(_server.ApiGetPlayerPositions()));
                            return;
                        case "/api/config":
                            await Respond(stream, 200, "application/json", JsonSerializer.Serialize(_server.ApiGetConfig()));
                            return;
                    }
                    break;

                case "OPTIONS":
                    await Respond(stream, 200, "text/plain", "");
                    return;

                case "POST":
                    JsonElement data;
                    try { data = JsonSerializer.Deserialize<JsonElement>(body); }
                    catch { await Respond(stream, 400, "application/json", "{\"error\":\"Invalid JSON\"}"); return; }

                    switch (path)
                    {
                        case "/api/kick":
                            _server.ApiKick(data.GetProperty("playerId").GetByte());
                            await Respond(stream, 200, "application/json", "{\"success\":true}");
                            return;
                        case "/api/ban":
                            _server.ApiBan(data.GetProperty("playerId").GetByte());
                            await Respond(stream, 200, "application/json", "{\"success\":true}");
                            return;
                        case "/api/slay":
                            _server.ApiSlay(data.GetProperty("playerId").GetByte());
                            await Respond(stream, 200, "application/json", "{\"success\":true}");
                            return;
                        case "/api/setmap":
                            _server.ApiSetMap(data.GetProperty("mapId").GetInt32());
                            await Respond(stream, 200, "application/json", "{\"success\":true}");
                            return;
                        case "/api/say":
                            _server.ApiSay(data.GetProperty("message").GetString() ?? "");
                            await Respond(stream, 200, "application/json", "{\"success\":true}");
                            return;
                        case "/api/setname":
                            _server.ApiSetName(data.GetProperty("name").GetString() ?? "");
                            await Respond(stream, 200, "application/json", "{\"success\":true}");
                            return;
                        case "/api/damage":
                            _server.ApiDamage(data.GetProperty("factor").GetSingle());
                            await Respond(stream, 200, "application/json", "{\"success\":true}");
                            return;
                    }
                    break;
            }

            await Respond(stream, 404, "application/json", "{\"error\":\"Not found\"}");
        }
        catch (KeyNotFoundException ex)
        {
            await Respond(stream, 400, "application/json", $"{{\"error\":\"Missing field: {ex.Message}\"}}");
        }
        catch (Exception ex)
        {
            await Respond(stream, 400, "application/json", $"{{\"error\":\"{ex.Message.Replace("\"", "'")}\"}}");
        }
    }

    private static async Task RespondRaw(NetworkStream stream, int status, string contentType, byte[] bodyBytes)
    {
        var header = $"HTTP/1.1 {status} OK\r\n" +
                     "Access-Control-Allow-Origin: *\r\n" +
                     $"Content-Type: {contentType}\r\n" +
                     $"Content-Length: {bodyBytes.Length}\r\n" +
                     "Connection: close\r\n\r\n";

        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
        await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
    }

    private bool Authenticate(string authHeader)
    {
        if (string.IsNullOrEmpty(_config.ApiKey)) return false;
        if (!authHeader.StartsWith("Bearer ")) return false;
        return authHeader[7..] == _config.ApiKey;
    }

    private static async Task Respond(NetworkStream stream, int status, string contentType, string body)
    {
        var statusText = status switch
        {
            200 => "OK",
            400 => "Bad Request",
            401 => "Unauthorized",
            404 => "Not Found",
            413 => "Request Entity Too Large",
            _ => "Unknown"
        };

        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var header = $"HTTP/1.1 {status} {statusText}\r\n" +
                     "Access-Control-Allow-Origin: *\r\n" +
                     "Access-Control-Allow-Headers: Authorization, Content-Type\r\n" +
                     "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                     $"Content-Type: {contentType}\r\n" +
                     $"Content-Length: {bodyBytes.Length}\r\n" +
                     "Connection: close\r\n\r\n";

        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
        await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
    }

    public void Dispose()
    {
        _running = false;
        _cts?.Cancel();
        _listener?.Stop();
        ((IDisposable?)_listener)?.Dispose();
    }
}
