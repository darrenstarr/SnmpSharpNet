namespace SnmpV2Get
{
    using System.Net;
    using System.Threading.Tasks;

    class Program
    {
        static void Main(string[] args)
        {
            Task.Factory.StartNew(async () =>
            {
                var x = await SnmpSharpNet.Helpers.GetAsync(IPAddress.Parse("10.200.30.100"), "Developer", "1.3.6.1.2.1.1.5.0");

                System.Console.Write(x.ToString());
                System.Console.WriteLine();
            }).Wait();

            return;
        }
    }
}
