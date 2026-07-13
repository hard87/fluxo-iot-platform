using System.Text;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Fluxo.Worker.Ingestion.Models;
using Fluxo.Worker.Ingestion.Options;
using Fluxo.Worker.Ingestion.Services;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace Fluxo.Worker.Ingestion.Workers;

public class MqttTelemetryIngestionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MqttTelemetryIngestionWorker> _logger;
    private readonly IIngestionMetrics _metrics;
    private readonly MqttIngestionOptions _options;
    private readonly MqttClientFactory _mqttFactory = new();
    private readonly Channel<QueuedMqttMessage> _channel;
    private readonly CancellationTokenSource _shutdownCts = new();

    private readonly List<Task> _processingTasks = [];
    private IMqttClient? _mqttClient;
    private long _queueDepth;
    private bool _connectedOnce;

    public MqttTelemetryIngestionWorker(
        IOptions<MqttIngestionOptions> options,
        IServiceScopeFactory scopeFactory,
        IIngestionMetrics metrics,
        ILogger<MqttTelemetryIngestionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _logger = logger;
        _options = options.Value;

        var channelCapacity = Math.Max(_options.ChannelCapacity, 100);
        _channel = Channel.CreateBounded<QueuedMqttMessage>(new BoundedChannelOptions(channelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateOptions(_options);
        StartProcessingLoops(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _mqttClient = _mqttFactory.CreateMqttClient();
                _mqttClient.ApplicationMessageReceivedAsync += HandleApplicationMessageAsync;
                _mqttClient.DisconnectedAsync += HandleDisconnectedAsync;

                var clientOptionsBuilder = new MqttClientOptionsBuilder()
                    .WithClientId(_options.ClientId)
                    .WithTcpServer(_options.BrokerHost, _options.BrokerPort)
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(_options.KeepAliveSeconds))
                    .WithCleanSession(false);

                if (!string.IsNullOrWhiteSpace(_options.Username))
                {
                    clientOptionsBuilder.WithCredentials(_options.Username, _options.Password);
                }

                if (_options.UseTls)
                {
                    var trustChain = LoadTrustChain(_options.TlsCaCertificatePath);

                    clientOptionsBuilder.WithTlsOptions(tls =>
                    {
                        tls.UseTls(true);

                        if (!string.IsNullOrWhiteSpace(_options.TlsTargetHost))
                            tls.WithTargetHost(_options.TlsTargetHost.Trim());

                        if (trustChain is not null)
                            tls.WithTrustChain(trustChain);

                        tls.WithAllowUntrustedCertificates(_options.TlsAllowUntrustedCertificates);
                        tls.WithIgnoreCertificateChainErrors(_options.TlsIgnoreCertificateChainErrors);
                        tls.WithIgnoreCertificateRevocationErrors(_options.TlsIgnoreCertificateRevocationErrors);
                    });

                    _logger.LogInformation(
                        "Conexao MQTT com TLS habilitado. TargetHost: {TargetHost}. CA configurada: {HasCa}.",
                        string.IsNullOrWhiteSpace(_options.TlsTargetHost) ? _options.BrokerHost : _options.TlsTargetHost,
                        trustChain is not null);

                    if (_options.TlsAllowUntrustedCertificates ||
                        _options.TlsIgnoreCertificateChainErrors ||
                        _options.TlsIgnoreCertificateRevocationErrors)
                    {
                        _logger.LogWarning(
                            "Validacao TLS MQTT relaxada por configuracao. Use apenas em laboratorio controlado.");
                    }
                }

                var clientOptions = clientOptionsBuilder.Build();

                _logger.LogInformation(
                    "Conectando ao broker MQTT em {Host}:{Port}...",
                    _options.BrokerHost,
                    _options.BrokerPort);

                await _mqttClient.ConnectAsync(clientOptions, stoppingToken);

                if (_connectedOnce)
                    _metrics.MqttReconnected();

                _connectedOnce = true;

                var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                    .WithTopicFilter(topic =>
                    {
                        topic.WithTopic(_options.TopicFilter);
                        topic.WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce);
                    })
                    .Build();

                await _mqttClient.SubscribeAsync(subscribeOptions, stoppingToken);

                _logger.LogInformation(
                    "Worker MQTT conectado e inscrito no topico: {TopicFilter}",
                    _options.TopicFilter);

                while (_mqttClient.IsConnected && !stoppingToken.IsCancellationRequested)
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha no loop principal de ingestao MQTT.");
            }
            finally
            {
                await CleanupClientAsync();
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                var delay = Math.Max(_options.ReconnectDelaySeconds, 1);
                _logger.LogWarning("Tentando reconectar ao broker em {Delay}s...", delay);
                await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);
            }
        }

        _shutdownCts.Cancel();
        _channel.Writer.Complete();

        try
        {
            await Task.WhenAll(_processingTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao finalizar workers de processamento interno.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _shutdownCts.Cancel();
        _channel.Writer.TryComplete();
        await CleanupClientAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task HandleApplicationMessageAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        try
        {
            _metrics.MessageReceived();

            var topic = args.ApplicationMessage.Topic;
            var payloadJson = args.ApplicationMessage.Payload is { Length: > 0 }
                ? Encoding.UTF8.GetString(args.ApplicationMessage.Payload)
                : string.Empty;

            await _channel.Writer.WriteAsync(
                new QueuedMqttMessage(topic, payloadJson, DateTime.UtcNow),
                _shutdownCts.Token);

            var queueDepth = Interlocked.Increment(ref _queueDepth);
            _metrics.MessageEnqueued(queueDepth);
        }
        catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested)
        {
            _logger.LogInformation("Enfileiramento interrompido durante encerramento do worker.");
        }
        catch (ChannelClosedException)
        {
            _logger.LogWarning("Canal interno encerrado; mensagem MQTT descartada.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enfileirar mensagem MQTT recebida.");
            _metrics.RecordResult(TelemetryIngestionProcessingResult.ProcessingError(
                "Falha ao enfileirar mensagem para processamento interno."));
        }
    }

    private Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        _logger.LogWarning(
            "Conexao MQTT encerrada. Reason: {Reason}.",
            args.Reason);
        return Task.CompletedTask;
    }

    private void StartProcessingLoops(CancellationToken stoppingToken)
    {
        var processingConcurrency = Math.Max(_options.ProcessingConcurrency, 1);

        for (var workerIndex = 0; workerIndex < processingConcurrency; workerIndex++)
        {
            _processingTasks.Add(Task.Run(
                () => ProcessQueueAsync(workerIndex + 1, stoppingToken),
                stoppingToken));
        }

        _logger.LogInformation(
            "Loop de processamento interno iniciado com {Workers} workers e capacidade de fila {Capacity}.",
            processingConcurrency,
            Math.Max(_options.ChannelCapacity, 100));
    }

    private async Task ProcessQueueAsync(int workerNumber, CancellationToken stoppingToken)
    {
        await foreach (var queuedMessage in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            var queueDepth = Interlocked.Decrement(ref _queueDepth);
            if (queueDepth < 0)
            {
                queueDepth = 0;
                Interlocked.Exchange(ref _queueDepth, 0);
            }

            _metrics.MessageDequeued(queueDepth);
            var stopwatch = Stopwatch.StartNew();

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ITelemetryIngestionProcessor>();

            try
            {
                var result = await processor.ProcessAsync(
                    queuedMessage.Topic,
                    queuedMessage.PayloadJson,
                    queuedMessage.ReceivedAtUtc,
                    stoppingToken);

                _metrics.RecordResult(result);

                if (result.Status is TelemetryIngestionProcessingStatus.DatabaseError
                    or TelemetryIngestionProcessingStatus.TransientFailure
                    or TelemetryIngestionProcessingStatus.ProcessingError)
                {
                    _logger.LogWarning(
                        "Worker interno #{WorkerNumber} processou mensagem com status {Status}. Motivo: {Reason}",
                        workerNumber,
                        result.Status,
                        result.Reason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Falha inesperada no worker interno #{WorkerNumber} ao processar topico {Topic}.",
                    workerNumber,
                    queuedMessage.Topic);

                _metrics.RecordResult(
                    TelemetryIngestionProcessingResult.ProcessingError("Falha inesperada no processamento interno."));
            }
            finally
            {
                stopwatch.Stop();
                _metrics.RecordProcessingDuration(stopwatch.Elapsed);
            }
        }
    }

    private async Task CleanupClientAsync()
    {
        if (_mqttClient is null)
            return;

        _mqttClient.ApplicationMessageReceivedAsync -= HandleApplicationMessageAsync;
        _mqttClient.DisconnectedAsync -= HandleDisconnectedAsync;

        if (_mqttClient.IsConnected)
            await _mqttClient.DisconnectAsync();

        _mqttClient.Dispose();
        _mqttClient = null;
    }

    private static void ValidateOptions(MqttIngestionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BrokerHost))
            throw new InvalidOperationException("MqttIngestion:BrokerHost is required.");

        if (options.BrokerPort <= 0)
            throw new InvalidOperationException("MqttIngestion:BrokerPort must be greater than zero.");

        if (string.IsNullOrWhiteSpace(options.TopicFilter))
            throw new InvalidOperationException("MqttIngestion:TopicFilter is required.");

        if (options.ChannelCapacity <= 0)
            throw new InvalidOperationException("MqttIngestion:ChannelCapacity must be greater than zero.");

        if (options.ProcessingConcurrency <= 0)
            throw new InvalidOperationException("MqttIngestion:ProcessingConcurrency must be greater than zero.");
        if (options.KeepAliveSeconds < 30)
            throw new InvalidOperationException("MqttIngestion:KeepAliveSeconds must be at least 30.");

        if (options.UseTls && !string.IsNullOrWhiteSpace(options.TlsCaCertificatePath) &&
            !File.Exists(options.TlsCaCertificatePath))
        {
            throw new FileNotFoundException(
                "MqttIngestion:TlsCaCertificatePath does not exist.",
                options.TlsCaCertificatePath);
        }
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
}
