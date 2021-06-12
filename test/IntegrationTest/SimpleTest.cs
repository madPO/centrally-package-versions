namespace IntegrationTest
{
    using CentrallyPackageVersions;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using utils;
    using Xunit;

    public class SimpleTest : DefaultTest
    {
        [Fact]
        public async Task BuildPropsTest()
        {
            var path = PrepareSolution("simple/Simple.sln", "BuildPropsTest");
            var (configuration, cancellationToken) = ConfigurationUtils.Create(path);

            using var aggregator = new VersionAggregator(configuration);
            await aggregator.CollectAsync(cancellationToken);

            Validate.BuildProps(configuration.Solution);
        }

        [Fact]
        public async Task PackagesPropsTest()
        {
            var path = PrepareSolution("simple/Simple.sln", "PackagesPropsTest");
            var (configuration, cancellationToken) = ConfigurationUtils.Create(path);

            using var aggregator = new VersionAggregator(configuration);
            await aggregator.CollectAsync(cancellationToken);

            Validate.PackagesProps(configuration.Solution,
                new Dictionary<string, string>
                {
                    ["Castle.Windsor"] = "5.0.0",
                    ["Castle.Windsor.MsDependencyInjection"] = "3.3.1",
                    ["Dapper"] = "1.60.6",
                    ["FluentMigrator"] = "3.2.1",
                    ["FluentMigrator.Runner"] = "3.2.1",
                    ["FluentMigrator.Runner.Postgres"] = "3.2.1",
                    ["Npgsql"] = "4.0.8"
                });
        }

        [Fact]
        public async Task ProjectsTest()
        {
            var path = PrepareSolution("simple/Simple.sln", "ProjectsTest");
            var (configuration, cancellationToken) = ConfigurationUtils.Create(path);

            using var aggregator = new VersionAggregator(configuration);
            await aggregator.CollectAsync(cancellationToken);

            Validate.Projects(configuration.Solution);
        }
    }
}