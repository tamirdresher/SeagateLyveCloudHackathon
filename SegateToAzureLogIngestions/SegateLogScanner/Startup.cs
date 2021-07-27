using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

[assembly: FunctionsStartup(typeof(SegateLogScanner.Startup))]
namespace SegateLogScanner
{
    public class Startup : FunctionsStartup
    {
        private const string LogAnalyticsKey = "LogAnalyticsKey";
        private const string LogAnalyticsWorkspaceID = "LogAnalyticsWorkspaceID";

        // override configure method
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var config = new ConfigurationBuilder()
               .SetBasePath(Environment.CurrentDirectory)
               .AddJsonFile("appsettings.json", false)
               .AddUserSecrets(Assembly.GetExecutingAssembly(), false)
               .AddEnvironmentVariables()
               .Build();

            builder.Services.AddLogging();
            builder.Services.AddSingleton<IConfiguration>(config);
            builder.Services.AddSingleton<IAzureLogAnalyticsClient>(svc =>
               {
                   var client = new AzureLogAnalyticsClient(
                     config[LogAnalyticsWorkspaceID],
                     config[LogAnalyticsKey]);
                   client.TimeStampField = config["LogAnalyticsTimestampField"];
                   return client;
               });
            // register your other services
        }
    }
}
