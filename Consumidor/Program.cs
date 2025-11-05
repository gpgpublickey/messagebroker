namespace Consumidor
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var rpcServer = new RpcServer();
            await rpcServer.StartServer();
        }
    }
}
