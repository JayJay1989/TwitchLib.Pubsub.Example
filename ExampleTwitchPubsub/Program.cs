using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;


namespace ExampleTwitchPubsub
{
    class Program
    {
        private static ILogger _logger;
        public static IConfiguration Settings;

        static void Main(string[] args)
        {            
            var outputTemplate = "[{Timestamp:HH:mm:ss} {Level}] {Message}{NewLine}{Exception}";
            _logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: outputTemplate)
                .WriteTo.File("log/log_.txt", outputTemplate: outputTemplate, rollingInterval: RollingInterval.Day)
                .CreateLogger();
            Settings = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("Settings.json", false, true)
                .AddEnvironmentVariables()
                .Build();
            new Program().MainAsync(args).GetAwaiter().GetResult();
        }

        async Task MainAsync(string[] args)
        {

        }
    }
}
