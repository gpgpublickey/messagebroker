using System.Text;
using RabbitMQ.Client;

namespace Publisher
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();
            await channel.ExchangeDeclareAsync(exchange: "logs3", type: ExchangeType.Topic);
            var message = "Holis";
            var body = Encoding.UTF8.GetBytes(message);
            await channel.BasicPublishAsync(exchange: "logs3", routingKey: "paises.ar", body: body);
            Console.WriteLine($" [x] Sent {message}");
            Console.ReadLine();
        }
    }
}
