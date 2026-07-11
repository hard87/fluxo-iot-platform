using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace Fluxo.Infrastructure.Mqtt;

public sealed class DynamicSecurityCommandException : Exception
{
    public DynamicSecurityCommandException(string message) : base(message)
    {
    }
}

public sealed class DynamicSecurityControlClient : IAsyncDisposable
{
    private const string CommandTopic = "$CONTROL/dynamic-security/v1";
    private const string ResponseTopic = "$CONTROL/dynamic-security/v1/response";

    private readonly MqttDynamicSecurityOptions _options;
    private readonly ILogger<DynamicSecurityControlClient> _logger;
    private readonly MqttClientFactory _clientFactory = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly SemaphoreSlim _commandLock = new(1, 1);

    private IMqttClient? _client;
    private TaskCompletionSource<string>? _pendingResponse;

    public DynamicSecurityControlClient(
        IOptions<MqttDynamicSecurityOptions> options,
        ILogger<DynamicSecurityControlClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// Commands are serialized (one in-flight at a time): the dynamic-security control
    /// protocol replies on a shared response topic without a documented way to correlate
    /// concurrent requests, so a single pending-response slot guarantees the reply we get
    /// belongs to the command we just sent.
    public async Task<JsonElement> SendCommandAsync(
        string command,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        await _commandLock.WaitAsync(cancellationToken);
        try
        {
            var client = await EnsureConnectedAsync(cancellationToken);

            var completionSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingResponse = completionSource;

            var commandPayload = new Dictionary<string, object?>(parameters) { ["command"] = command };
            var envelope = new Dictionary<string, object?> { ["commands"] = new[] { commandPayload } };
            var json = JsonSerializer.Serialize(envelope);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(CommandTopic)
                .WithPayload(json)
                .WithResponseTopic(ResponseTopic)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await client.PublishAsync(message, cancellationToken);

            var timeoutSeconds = Math.Max(_options.CommandTimeoutSeconds, 1);
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            await using var registration = linkedCts.Token.Register(() => completionSource.TrySetCanceled());

            string responseJson;
            try
            {
                responseJson = await completionSource.Task;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new DynamicSecurityCommandException(
                    $"Timed out waiting for dynamic-security response to command '{command}'.");
            }

            using var document = JsonDocument.Parse(responseJson);
            var firstResponse = document.RootElement.GetProperty("responses").EnumerateArray().First();

            if (firstResponse.TryGetProperty("error", out var errorProperty) &&
                errorProperty.ValueKind != JsonValueKind.Null)
            {
                throw new DynamicSecurityCommandException(
                    $"dynamic-security command '{command}' failed: {errorProperty.GetString()}");
            }

            return firstResponse.Clone();
        }
        finally
        {
            _pendingResponse = null;
            _commandLock.Release();
        }
    }

    private async Task<IMqttClient> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client is { IsConnected: true })
            return _client;

        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            if (_client is { IsConnected: true })
                return _client;

            if (_client is not null)
            {
                _client.ApplicationMessageReceivedAsync -= HandleApplicationMessageAsync;
                _client.Dispose();
                _client = null;
            }

            var client = _clientFactory.CreateMqttClient();
            client.ApplicationMessageReceivedAsync += HandleApplicationMessageAsync;

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithClientId($"fluxo-dynsec-admin-{Guid.NewGuid():N}")
                .WithTcpServer(_options.BrokerHost, _options.BrokerPort)
                .WithCredentials(_options.AdminUsername, _options.AdminPassword)
                .WithCleanSession(true);

            if (_options.UseTls)
            {
                var trustChain = LoadTrustChain(_options.TlsCaCertificatePath);

                optionsBuilder.WithTlsOptions(tls =>
                {
                    tls.UseTls(true);

                    if (!string.IsNullOrWhiteSpace(_options.TlsTargetHost))
                        tls.WithTargetHost(_options.TlsTargetHost.Trim());

                    if (trustChain is not null)
                        tls.WithTrustChain(trustChain);

                    tls.WithAllowUntrustedCertificates(_options.TlsAllowUntrustedCertificates);
                });
            }

            var connectResult = await client.ConnectAsync(optionsBuilder.Build(), cancellationToken);

            if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
            {
                client.Dispose();
                throw new DynamicSecurityCommandException(
                    $"Falha ao conectar no broker MQTT ({_options.BrokerHost}:{_options.BrokerPort}) para administracao " +
                    $"dynamic-security: {connectResult.ResultCode} ({connectResult.ReasonString}). " +
                    "Verifique MqttDynamicSecurity:AdminUsername/AdminPassword.");
            }

            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(topic => topic
                    .WithTopic(ResponseTopic)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                .Build();

            await client.SubscribeAsync(subscribeOptions, cancellationToken);

            _client = client;
            _logger.LogInformation(
                "Conectado ao broker MQTT para administracao dynamic-security em {Host}:{Port}.",
                _options.BrokerHost,
                _options.BrokerPort);

            return client;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private Task HandleApplicationMessageAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var pending = _pendingResponse;
        if (pending is null)
            return Task.CompletedTask;

        try
        {
            var payloadJson = Encoding.UTF8.GetString(args.ApplicationMessage.Payload);
            pending.TrySetResult(payloadJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao ler resposta de comando dynamic-security.");
            pending.TrySetException(ex);
        }

        return Task.CompletedTask;
    }

    private static X509Certificate2Collection? LoadTrustChain(string? certificatePath)
    {
        if (string.IsNullOrWhiteSpace(certificatePath))
            return null;

        var certificate = X509Certificate2.CreateFromPemFile(certificatePath.Trim());
        var collection = new X509Certificate2Collection();
        collection.Add(certificate);
        return collection;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is null)
            return;

        _client.ApplicationMessageReceivedAsync -= HandleApplicationMessageAsync;

        if (_client.IsConnected)
            await _client.DisconnectAsync();

        _client.Dispose();
        _client = null;
    }
}
