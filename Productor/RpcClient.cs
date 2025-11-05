using System.Collections.Concurrent;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Productor
{
    public class RpcClient : IAsyncDisposable
    {
        private const string QUEUE_NAME = "rpc_queue";

        private readonly IConnectionFactory _connectionFactory;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _callbackMapper
            = new();

        private IConnection? _connection;
        private IChannel? _channel;
        private string? _replyQueueName;

        public RpcClient()
        {
            _connectionFactory = new ConnectionFactory { HostName = "localhost" };
        }

        public async Task StartAsync() // Configura el canal de comunicacion con el message broker y empieza a escuchar mensajes en la cola de respuestas
        {
            _connection = await _connectionFactory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            // Declara una cola nombrada por el servidor para recibir respuestas
            QueueDeclareOk repliesQueue = await _channel.QueueDeclareAsync();
            _replyQueueName = repliesQueue.QueueName; // Assigna el nombre default de la cola generado por el servidor
            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += (model, ea) => // Callback que maneja los mensajes recibidos
            {
                string? correlationId = ea.BasicProperties.CorrelationId;

                if (!string.IsNullOrEmpty(correlationId))
                {
                    if (_callbackMapper.TryRemove(correlationId, out var tcs))
                    {
                        var body = ea.Body.ToArray();
                        var response = Encoding.UTF8.GetString(body);
                        tcs.TrySetResult(response);
                    }
                }

                return Task.CompletedTask;
            };

            await _channel.BasicConsumeAsync(_replyQueueName, true, consumer);
        }

        public async Task<string> CallAsync(string message,
            CancellationToken cancellationToken = default) // Envia un mensaje a la cola de peticiones y espera la respuesta
        {
            if (_channel is null)
            {
                throw new InvalidOperationException();
            }

            string correlationId = Guid.NewGuid().ToString(); // Creamos el correlation ID
            var props = new BasicProperties
            {
                CorrelationId = correlationId,
                ReplyTo = _replyQueueName // Indicamos la cola donde se espera la respuesta (la que creamos en el metodo StartAsync)
            };
            
            // Es una Task no sujeta a un delegado, con el objetivo de especificar su resultado manualmente en otro momento
            var requestReplyPromise = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously); 
            _callbackMapper.TryAdd(correlationId, requestReplyPromise);

            var messageBytes = Encoding.UTF8.GetBytes(message);

            // Publica el mensaje en la cola de peticiones
            await _channel.BasicPublishAsync(
                exchange: string.Empty, // Usa el exchange por defecto que es un Direct Exchange
                routingKey: QUEUE_NAME,
                mandatory: true, // Indica que el mensaje debe ser enrutado a una cola, si no es asi se devuelve al publicador
                basicProperties: props,
                body: messageBytes);

            using CancellationTokenRegistration ctr =
                cancellationToken.Register(() =>
                {
                    _callbackMapper.TryRemove(correlationId, out _);
                    requestReplyPromise.SetCanceled();
                });

            return await requestReplyPromise.Task;
        }

        public async ValueTask DisposeAsync()
        {
            if (_channel is not null)
            {
                await _channel.CloseAsync();
            }

            if (_connection is not null)
            {
                await _connection.CloseAsync();
            }
        }
    }
}
