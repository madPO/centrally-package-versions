using CentrallyPackageVersions;
using CommandLine;
using System;
using System.Threading;
using System.Threading.Tasks;

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

        using var aggregator = new VersionAggregator(config);

        await aggregator.CollectAsync(source.Token);
    });

// wait console log
await Task.Delay(TimeSpan.FromSeconds(3));