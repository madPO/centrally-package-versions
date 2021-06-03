namespace CentrallyPackageVersions
{
    using CommandLine;
    using Microsoft.Build.Locator;
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    class Program
    {
        static async Task Main(string[] args)
        {
            MSBuildLocator.RegisterDefaults();
            var parser = new Parser(with =>
            {
                with.EnableDashDash = true;
                with.AutoHelp = true;
                with.IgnoreUnknownArguments = false;
                with.HelpWriter = Console.Out;
            });


            await parser.ParseArguments<Configuration>(args)
                .WithParsedAsync(async config =>
                {
                    var source = new CancellationTokenSource();
                    source.CancelAfter(config.Timeout);

                    config.Solution = Path.IsPathFullyQualified(config.Solution)
                        ? config.Solution
                        : Path.GetFullPath(config.Solution, Environment.CurrentDirectory);

                    var aggregator = new VersionAggregator(config);

                    await aggregator.CollectAsync(source.Token);
                });
        }
    }
}