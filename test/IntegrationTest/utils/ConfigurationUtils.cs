namespace IntegrationTest.utils
{
    using CentrallyPackageVersions;
    using System;
    using System.IO;
    using System.Threading;

    public static class ConfigurationUtils
    {
        public static (Configuration, CancellationToken) Create(string solutionPath,
            TakeVersion takeStrategy = TakeVersion.Max)
        {
            var source = new CancellationTokenSource();
            source.CancelAfter(TimeSpan.FromMinutes(5));
            return (
                new Configuration
                {
                    Solution = Path.IsPathFullyQualified(solutionPath)
                        ? solutionPath
                        : $"./projects/{solutionPath}",
                    ConflictResolve = takeStrategy,
                    Verbose = true
                }, source.Token);
        }
    }
}