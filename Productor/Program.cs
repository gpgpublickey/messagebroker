using System.Net.Sockets;

namespace Productor
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Client");
            string n = args.Length > 0 ? args[0] : "30";
            await InvokeAsync(n);

            Console.WriteLine("Press enter to exit.");
            Console.ReadLine();
        }

        private static async Task InvokeAsync(string n)
        {
            var rpcClient = new RpcClient();
            await rpcClient.StartAsync();


            while (true)
            {
                Thread.Sleep(1000);
                n = new Random().Next(1, 11).ToString();
                Console.WriteLine(" [x] Requesting fib({0})", n);
                var response = await rpcClient.CallAsync(n);
                Console.WriteLine(" [.] Got '{0}'", response);
            }
        }
    }
}
