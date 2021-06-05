namespace CentrallyPackageVersions
{
    using Microsoft.Build.Construction;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Logging.Console;
    using System;
    using System.Buffers;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Package and version aggregator
    /// </summary>
    public class VersionAggregator : IDisposable
    {
        private readonly Configuration _configuration;

        private readonly ILogger _logger;

        private readonly ILoggerFactory _loggerFactory;

        public VersionAggregator(Configuration configuration)
        {
            _configuration = configuration;
            _logger = NullLogger.Instance;
            _loggerFactory = null;

            if (configuration.Verbose)
            {
                _loggerFactory =
                    LoggerFactory.Create(builder => builder.AddConsole(options =>
                    {
                        options.IncludeScopes = false;
                        options.DisableColors = false;
                        options.Format = ConsoleLoggerFormat.Default;
                    }).SetMinimumLevel(LogLevel.Debug));

                _logger = _loggerFactory.CreateLogger(_configuration.Solution);
            }
        }

        /// <summary>
        /// Process all project in solution and collect packages
        /// </summary>
        public async Task CollectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ValidatePath();

            _logger.LogDebug($"Loading solution {_configuration.Solution}");

            var solution = await LoadSolutionAsync(cancellationToken);

            _logger.LogDebug("solution loaded.");

            var references = await CollectPackageAsync(solution, cancellationToken);

            ClearProps();
            await CreateBuildPropsAsync(cancellationToken);
            CreatePackageProps(references);
        }

        private void CreatePackageProps(ConcurrentDictionary<string, Package> references)
        {
            var path = Path.GetDirectoryName(_configuration.Solution);
            var packageProps = ProjectRootElement.Create(Path.Combine(path, "Directory.Packages.props"));
            var itemsGroup = packageProps.AddItemGroup();

            foreach (var reference in references.OrderBy(x => x.Key).Select(x => x.Value).ToArray())
            {
                var item = itemsGroup.AddItem("PackageVersion", reference.Name);
                item.AddMetadata("Version", reference.Version.ToString(), true);

                foreach (var element in reference.Attributes)
                {
                    item.AddMetadata(element.Name, element.Value, true);
                }
            }

            packageProps.Save();
        }

        private async Task CreateBuildPropsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var provider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly());
            var path = Path.GetDirectoryName(_configuration.Solution);

            _logger.LogDebug("Create Directory.Build.props");

            await using var original = provider.GetFileInfo("content/Directory.Build.template").CreateReadStream();
            await using var props = File.Create(Path.Combine(path, "Directory.Build.props"));
            await original.CopyToAsync(props, cancellationToken);
        }

        private void ClearProps()
        {
            var path = Path.GetDirectoryName(_configuration.Solution);

            var buildProps = Path.Combine(path, "Directory.Build.props");
            if (File.Exists(buildProps))
            {
                _logger.LogDebug("Delete Directory.Build.props");
                File.Delete(buildProps);
            }

            var packagesProps = Path.Combine(path, "Directory.Packages.props");
            if (File.Exists(packagesProps))
            {
                _logger.LogDebug("Delete Directory.Packages.props");
                File.Delete(Path.Combine(path, "Directory.Packages.props"));
            }
        }

        private async Task<ConcurrentDictionary<string, Package>> CollectPackageAsync(SolutionFile solution,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var projects = solution.ProjectsInOrder.ToArray();
            var tasks = ArrayPool<Task>.Shared.Rent(projects.Length);
            var references = new ConcurrentDictionary<string, Package>();
            try
            {
                for (var i = 0; i < tasks.Length; i++)
                {
                    if (i >= projects.Length)
                    {
                        tasks[i] = Task.CompletedTask;
                        continue;
                    }

                    var project = projects[i];
                    // todo: max thread
                    tasks[i] = Task.Run(() =>
                    {
                        try
                        {
                            ProcessProject(project, references);
                        }
                        catch (Exception exception)
                        {
                            _logger.LogError(exception, $"Error in project {project.AbsolutePath}");
                        }
                    }, cancellationToken);
                }

                await Task.WhenAll(tasks);

                return references;
            }
            finally
            {
                ArrayPool<Task>.Shared.Return(tasks);
            }
        }

        private void ProcessProject(ProjectInSolution project, ConcurrentDictionary<string, Package> references)
        {
            if (project == null || project.ProjectType != SolutionProjectType.KnownToBeMSBuildFormat)
            {
                return;
            }

            _logger.LogDebug($"Process {project.AbsolutePath}");

            if (project.AbsolutePath == null || !File.Exists(project.AbsolutePath))
            {
                _logger.LogWarning($"Project {project?.AbsolutePath} not found!");
                return;
            }

            var root = ProjectRootElement.Open(project.AbsolutePath);

            if (root == null)
            {
                _logger.LogWarning($"Project {project.AbsolutePath} not parsed!");
                return;
            }

            foreach (var item in root.ItemGroups)
            {
                foreach (var reference in item.Items)
                {
                    if (!reference.ElementName.Equals("PackageReference", StringComparison.CurrentCultureIgnoreCase))
                    {
                        continue;
                    }

                    var package = Package.Parse(reference);

                    if (package != null)
                    {
                        _logger.LogDebug($"Found {package}");
                        references.AddOrUpdate(package.Name, package, (k, v) => v.CompareTo(package) > 0 ? v : package);
                    }

                    reference.RemoveAllChildren();
                }
            }

            root.Save();
        }

        private void ValidatePath()
        {
            if (!_configuration.Solution.EndsWith(".sln"))
                throw new ArgumentException($"Path {_configuration.Solution} is not a solution!");

            if (!File.Exists(_configuration.Solution))
                throw new ArgumentException($"Solution {_configuration.Solution} not found!");
        }

        private Task<SolutionFile> LoadSolutionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(SolutionFile.Parse(_configuration.Solution));
        }

        public void Dispose()
        {
            _loggerFactory?.Dispose();
        }
    }
}