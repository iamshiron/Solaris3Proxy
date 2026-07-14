using Shiron.Solaris3Proxy.Options;
using Tmds.DBus.Protocol;

namespace Shiron.Solaris3Proxy.Services.Screen.Wayland;

/// <summary>
/// Drives the <c>org.freedesktop.portal.ScreenCast</c> D-Bus portal to obtain a permissioned
/// PipeWire screen-cast stream. The handshake is CreateSession → SelectSources → Start
/// (which prompts the user for consent) → the granted PipeWire node id.
/// </summary>
/// <remarks>
/// The D-Bus connection and portal session are kept open for the lifetime of this object so the
/// stream stays alive; <see cref="DisposeAsync"/> closes the session to avoid leaking portal state.
/// </remarks>
public sealed class ScreenCastPortal(ILogger<ScreenCastPortal> logger) : IAsyncDisposable {
    private const string Destination = "org.freedesktop.portal.Desktop";
    private const string ObjectPathString = "/org/freedesktop/portal/desktop";
    private const string ScreenCastInterface = "org.freedesktop.portal.ScreenCast";

    // Non-interactive handshake calls should not hang if the portal backend is unresponsive.
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(30);

    private readonly List<IDisposable> _registrations = [];
    private DBusConnection? _connection;
    private string _sender = string.Empty;
    private ObjectPath _session;
    private bool _sessionOpen;

    /// <summary>The granted PipeWire node id and an optional restore token for future non-interactive starts.</summary>
    public sealed record ScreenCastStream(uint NodeId, string? RestoreToken);

    /// <summary>Runs the full portal handshake and returns the granted stream.</summary>
    public async Task<ScreenCastStream> StartAsync(ScreenCaptureOptions options, string? restoreToken, CancellationToken cancellationToken) {
        var address = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS")
            ?? $"unix:path={Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")}/bus";

        var connection = new DBusConnection(new DBusConnectionOptions(address) { AutoConnect = false });
        await connection.ConnectAsync();
        _connection = connection;
        _sender = connection.UniqueName!.TrimStart(':').Replace('.', '_');
        logger.LogInformation("Connected to session bus as {UniqueName}; starting ScreenCast portal handshake.", connection.UniqueName);

        var createResults = await CallAsync(CreateSessionMessage, cancellationToken, HandshakeTimeout);
        _session = new ObjectPath(createResults["session_handle"].GetString());
        _sessionOpen = true;
        logger.LogInformation("Step 1/3 CreateSession OK: session={Session}.", _session.ToString());

        logger.LogInformation("Step 2/3 SelectSources (types=MONITOR, cursor_mode={CursorMode}, restore_token={HasToken}).",
            options.CursorMode, restoreToken is { Length: > 0 });
        await CallAsync(token => SelectSourcesMessage(token, options, restoreToken), cancellationToken, HandshakeTimeout);

        logger.LogInformation("Step 3/3 Start: awaiting screen-share consent dialog (up to {Timeout}s) — approve it to begin capture.", options.ConsentTimeoutSeconds);
        var startResults = await CallAsync(StartMessage, cancellationToken, TimeSpan.FromSeconds(options.ConsentTimeoutSeconds));

        var nodeId = ParseFirstNodeId(startResults);
        var newToken = startResults.TryGetValue("restore_token", out var rt) ? rt.GetString() : null;
        logger.LogInformation("Screen-share granted; PipeWire node={NodeId}, restore_token={HasToken}.", nodeId, newToken is { Length: > 0 });

        return new ScreenCastStream(nodeId, newToken);
    }

    /// <summary>Invokes a portal method and awaits its asynchronous <c>Response</c> signal.</summary>
    private async Task<Dictionary<string, VariantValue>> CallAsync(
        Func<string, MessageBuffer> messageFactory, CancellationToken cancellationToken, TimeSpan? timeout = null) {
        var connection = _connection!;
        var token = NewToken();
        var requestPath = $"{ObjectPathString}/request/{_sender}/{token}";

        var completion = new TaskCompletionSource<Dictionary<string, VariantValue>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var rule = new MatchRule {
            Type = MessageType.Signal,
            Interface = "org.freedesktop.portal.Request",
            Member = "Response",
            Path = requestPath,
        };

        // Subscribe (awaited) before issuing the call so the Response can never be missed.
        // NB: Notification.Exception throws unless the notification is a completion — only ever
        // touch it after checking HasValue/IsCompletion, otherwise valid responses are lost.
        var registration = await connection.AddMatchAsync(rule, ReadResponse,
            (Notification<(uint code, Dictionary<string, VariantValue> results)> notification) => {
                if (!notification.HasValue) return; // disposal / reader-failure notification
                var (code, results) = notification.Value;
                if (code != 0) completion.TrySetException(
                    new InvalidOperationException($"Portal request was cancelled or failed (code {code})."));
                else completion.TrySetResult(results);
            },
            emitOnCapturedContext: false, ObserverFlags.None, null);
        _registrations.Add(registration);

        logger.LogDebug("Portal call sent; awaiting Response at {RequestPath}.", requestPath);
        await connection.CallMethodAsync(messageFactory(token), static (Message m, object? s) => m.GetBodyReader().ReadObjectPath(), null);

        var responseTask = timeout is { } t ? completion.Task.WaitAsync(t, cancellationToken) : completion.Task.WaitAsync(cancellationToken);
        return await responseTask;
    }

    private static (uint code, Dictionary<string, VariantValue> results) ReadResponse(Message message, object? state) {
        var reader = message.GetBodyReader();
        return (reader.ReadUInt32(), reader.ReadDictionaryOfStringToVariantValue());
    }

    private static uint ParseFirstNodeId(Dictionary<string, VariantValue> results) {
        // "streams" is an array of (uint node_id, a{sv} properties).
        var streams = results["streams"];
        if (streams.Count == 0) throw new InvalidOperationException("Portal granted no screen-cast streams.");
        return streams.GetItem(0).GetItem(0).GetUInt32();
    }

    private MessageBuffer CreateSessionMessage(string token) {
        var options = new Dictionary<string, VariantValue> {
            ["handle_token"] = token,
            ["session_handle_token"] = NewToken(),
        };
        var writer = _connection!.GetMessageWriter();
        writer.WriteMethodCallHeader(Destination, ObjectPathString, ScreenCastInterface, "CreateSession", "a{sv}", MessageFlags.None);
        writer.WriteDictionary(options);
        var message = writer.CreateMessage();
        writer.Dispose();
        return message;
    }

    private MessageBuffer SelectSourcesMessage(string token, ScreenCaptureOptions options, string? restoreToken) {
        var arguments = new Dictionary<string, VariantValue> {
            ["handle_token"] = token,
            ["types"] = 1u,                   // MONITOR
            ["cursor_mode"] = options.CursorMode,
            ["persist_mode"] = 2u,            // persistent — enables restore tokens
        };
        if (!string.IsNullOrEmpty(restoreToken)) arguments["restore_token"] = restoreToken;

        var writer = _connection!.GetMessageWriter();
        writer.WriteMethodCallHeader(Destination, ObjectPathString, ScreenCastInterface, "SelectSources", "oa{sv}", MessageFlags.None);
        writer.WriteObjectPath(_session);
        writer.WriteDictionary(arguments);
        var message = writer.CreateMessage();
        writer.Dispose();
        return message;
    }

    private MessageBuffer StartMessage(string token) {
        var options = new Dictionary<string, VariantValue> { ["handle_token"] = token };
        var writer = _connection!.GetMessageWriter();
        writer.WriteMethodCallHeader(Destination, ObjectPathString, ScreenCastInterface, "Start", "osa{sv}", MessageFlags.None);
        writer.WriteObjectPath(_session);
        writer.WriteString(string.Empty); // parent_window
        writer.WriteDictionary(options);
        var message = writer.CreateMessage();
        writer.Dispose();
        return message;
    }

    private static string NewToken() => "s3p_" + Guid.NewGuid().ToString("N")[..12];

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() {
        if (_connection is { } connection) {
            if (_sessionOpen) {
                try {
                    var writer = connection.GetMessageWriter();
                    writer.WriteMethodCallHeader(Destination, _session.ToString(), "org.freedesktop.portal.Session", "Close", null, MessageFlags.None);
                    var message = writer.CreateMessage();
                    writer.Dispose();
                    await connection.CallMethodAsync(message);
                } catch (Exception ex) {
                    logger.LogDebug(ex, "Failed to close ScreenCast portal session cleanly.");
                }
            }

            foreach (var registration in _registrations) registration.Dispose();
            connection.Dispose();
        }
    }
}
