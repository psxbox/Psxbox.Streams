using Microsoft.Extensions.Logging;
using MQTTnet;
using Psxbox.MQTTClient;
using Psxbox.Utils;
using System.Buffers;

namespace Psxbox.Streams
{
    public class MqttStream : BaseStream
    {
        private IMqttClient? _mqttClient;
        private MqttManagedClient? _managedClient;
        private MqttAutoReconnectClient? _autoClient;
        private readonly string _subTopic;
        private readonly string _pubTopic;
        private readonly bool _disposeMqttClient;
        private readonly ILogger? _logger;
        private readonly CancellationTokenSource _cts = new();
        private bool _isMqttClientSubscribed = false;
        private bool _isManagedClientSubscribed = false;
        private bool _isAutoClientSubscribed = false;

        public override string Name => "MQTT Stream";

        public override bool IsConnected =>
            _mqttClient?.IsConnected
            ?? _managedClient?.IsConnected
            ?? _autoClient?.IsConnected
            ?? false;

        public MqttStream(IMqttClient mqttClient, string subTopic, string pubTopic,
            int timeOut = 7000, bool disposeMqttClient = false, ILogger? logger = null, bool use7E1 = false)
                : base(timeOut, use7E1)
        {
            this._mqttClient = mqttClient;
            this._subTopic = subTopic;
            this._pubTopic = pubTopic;
            this._disposeMqttClient = disposeMqttClient;
            this._logger = logger;
        }

        public MqttStream(MqttManagedClient managedClient, string subTopic, string pubTopic, int timeOut = 7000,
            bool disposeMqttClient = false, bool use7E1 = false) : base(timeOut, use7E1)
        {
            this._managedClient = managedClient;
            this._subTopic = subTopic;
            this._pubTopic = pubTopic;
            this._disposeMqttClient = disposeMqttClient;
        }

        // New constructor for auto reconnect client (MQTTnet v5)
        public MqttStream(MqttAutoReconnectClient autoClient, string subTopic, string pubTopic, int timeOut = 7000,
            bool disposeMqttClient = false, ILogger? logger = null, bool use7E1 = false) : base(timeOut, use7E1)
        {
            this._autoClient = autoClient;
            this._subTopic = subTopic;
            this._pubTopic = pubTopic;
            this._disposeMqttClient = disposeMqttClient;
            this._logger = logger;
        }

        private Task MangedClientOnMessage(string topic, byte[] payload)
        {
            if (topic != _subTopic) return Task.CompletedTask;

            LogReceivedMessage(topic, payload);

            foreach (var item in payload)
            {
                _channel.Writer.TryWrite(item);
            }

            return Task.CompletedTask;
        }

        private Task AutoClientOnMessage(string topic, byte[] payload)
        {
            if (topic != _subTopic) return Task.CompletedTask;

            LogReceivedMessage(topic, payload);

            foreach (var item in payload)
            {
                _channel.Writer.TryWrite(item);
            }

            return Task.CompletedTask;
        }

        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            var topic = arg.ApplicationMessage.Topic;
            var payload = arg.ApplicationMessage.Payload.ToArray();

            if (payload is null) return Task.CompletedTask;

            if (topic != _subTopic) return Task.CompletedTask;

            LogReceivedMessage(topic, payload);

            foreach (var item in payload)
            {
                _channel.Writer.TryWrite(item);
            }

            return Task.CompletedTask;
        }

        private void LogReceivedMessage(string topic, byte[] payload)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Received message on topic {Topic}, Payload: {Payload}",
                    topic, Convert.ToHexString(payload));
            }
        }

        public override void Write(byte[] data)
        {
            try
            {
                WriteAsync(data).Wait(TimeSpan.FromSeconds(5), _cts.Token);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException("MQTT Write timeout (5s)");
            }
        }

        public override async Task WriteAsync(byte[] data)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Publishing to topic {Topic}, Payload: {Payload}",
                    _pubTopic, Convert.ToHexString(data));
            }

            // var payload = Use7E1 ? data.ConvertTo7Bit('E') : data;
            var payload = data;

            if (Use7E1)
            {
                Converters.ConvertTo7Bit(payload.AsSpan(), 'E');
            }
            
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic(_pubTopic)
                .WithPayload(payload)
                .Build();

            try
            {
                if (_mqttClient != null)
                {
                    await _mqttClient.PublishAsync(msg, _cts.Token).ConfigureAwait(false);
                }

                if (_managedClient != null)
                {
                    await _managedClient.PublishAsync(_pubTopic, payload).ConfigureAwait(false);
                }

                if (_autoClient != null)
                {
                    await _autoClient.PublishAsync(_pubTopic, payload).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Write error: {Message}", ex.Message);
            }
        }

        public override void Connect()
        {
            ConnectAsync().Wait(TimeSpan.FromSeconds(5), _cts.Token);
        }

        private readonly SemaphoreSlim connectSemaphore = new(1, 1);

        public override async Task ConnectAsync()
        {
            await connectSemaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (_mqttClient != null)
                {
                    if (!_mqttClient.IsConnected)
                        await _mqttClient.ConnectAsync(_mqttClient.Options, _cts.Token).ConfigureAwait(false);

                    if (!_isMqttClientSubscribed)
                    {
                        await _mqttClient.SubscribeAsync(_subTopic).ConfigureAwait(false);
                        _mqttClient.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
                        _isMqttClientSubscribed = true;
                    }
                }

                if (_managedClient != null)
                {
                    if (!_managedClient.IsConnected)
                        await _managedClient.ConnectMqttClientAsync().ConfigureAwait(false);

                    if (!_isManagedClientSubscribed)
                    {
                        await _managedClient.SubscribeAsync(_subTopic).ConfigureAwait(false);
                        _managedClient.OnMessage += MangedClientOnMessage;
                        _isManagedClientSubscribed = true;
                    }
                }

                if (_autoClient != null)
                {
                    if (!_autoClient.IsConnected)
                        await _autoClient.StartAsync(_cts.Token).ConfigureAwait(false);

                    if (!_isAutoClientSubscribed)
                    {
                        await _autoClient.SubscribeAsync(_subTopic, _cts.Token).ConfigureAwait(false);
                        _autoClient.OnMessage += AutoClientOnMessage;
                        _isAutoClientSubscribed = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Connect error: {Message}", ex.Message);
            }
            finally
            {
                connectSemaphore.Release();
            }
        }

        public override void Close()
        {
            CloseAsync().Wait(TimeSpan.FromSeconds(5), _cts.Token);
        }

        public override async Task CloseAsync()
        {
            await connectSemaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (_mqttClient is not null && _isMqttClientSubscribed)
                {
                    _mqttClient.ApplicationMessageReceivedAsync -= MqttClient_ApplicationMessageReceivedAsync;
                    if (_mqttClient.IsConnected) await _mqttClient.UnsubscribeAsync(_subTopic).ConfigureAwait(false);
                    _isMqttClientSubscribed = false;
                }

                if (_managedClient is not null && _isManagedClientSubscribed)
                {
                    _managedClient.OnMessage -= MangedClientOnMessage;
                    if (_managedClient.IsConnected) await _managedClient.UnsubscribeAsync(_subTopic).ConfigureAwait(false);
                    _isManagedClientSubscribed = false;
                }

                if (_autoClient is not null && _isAutoClientSubscribed)
                {
                    _autoClient.OnMessage -= AutoClientOnMessage;
                    if (_autoClient.IsConnected) await _autoClient.UnsubscribeAsync(_subTopic, _cts.Token).ConfigureAwait(false);
                    _isAutoClientSubscribed = false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Close error for topic {Topic}: {Message}", _subTopic, ex.Message);
            }
            finally
            {
                connectSemaphore.Release();
            }
        }

        public override async ValueTask DisposeAsync()
        {
            try
            {
                await CloseAsync().ConfigureAwait(false);
                await _cts.CancelAsync().ConfigureAwait(false);

                if (_disposeMqttClient)
                {
                    if (_mqttClient is not null && _mqttClient.IsConnected)
                        await _mqttClient.DisconnectAsync().ConfigureAwait(false);

                    if (_managedClient is not null && _managedClient.IsConnected)
                        await _managedClient.DisconnectAsync().ConfigureAwait(false);

                    if (_autoClient is not null)
                    {
                        await _autoClient.StopAsync(_cts.Token).ConfigureAwait(false);
                    }

                    _mqttClient?.Dispose();
                    _managedClient?.Dispose();
                    _autoClient?.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Dispose error: {Message}", ex.Message);
            }
            finally
            {
                _mqttClient = null;
                _managedClient = null;
                _autoClient = null;

                await base.DisposeAsync().ConfigureAwait(false);
                GC.SuppressFinalize(this);
            }
        }
    }
}