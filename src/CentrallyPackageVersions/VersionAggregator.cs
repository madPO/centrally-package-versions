namespace CentrallyPackageVersions
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.MSBuild;
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
    using System.Xml;

    /// <summary>
    /// Package and version aggregator
    /// </summary>
    public class VersionAggregator
    {
        private readonly Configuration _configuration;

        private readonly ILogger _logger;

        public VersionAggregator(Configuration configuration)
        {
            _configuration = configuration;
            _logger = NullLogger.Instance;

            if (configuration.Verbose)
            {
                var loggerFactory =
                    LoggerFactory.Create(builder => builder.AddConsole(options =>
                    {
                        options.IncludeScopes = false;
                        options.DisableColors = false;
                        options.Format = ConsoleLoggerFormat.Default;
                    }).SetMinimumLevel(LogLevel.Debug));

                _logger = loggerFactory.CreateLogger(_configuration.Solution);
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
            await CreatePackagePropsAsync(references, cancellationToken);
        }

        private async Task CreatePackagePropsAsync(ConcurrentDictionary<string, Version> references,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = Path.GetDirectoryName(_configuration.Solution);
            var newPath = Path.Combine(path, "Directory.Packages.props");

            await using var stream = File.Open(newPath, FileMode.CreateNew);
            var document = new XmlDocument();

            var project = document.CreateElement("Project");
            document.AppendChild(project);
            var group = document.CreateElement("ItemGroup");
            foreach (var reference in references.OrderBy(x => x.Key).ToArray())
            {
                var package = document.CreateElement("PackageVersion");
                var update = document.CreateAttribute("Include");
                var version = document.CreateAttribute("Version");

                update.Value = reference.Key;
                version.Value = reference.Value.ToString();
                package.Attributes.Append(update);
                package.Attributes.Append(version);

                group.AppendChild(package);
            }

            project.AppendChild(group);
            document.Save(stream);
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

        private async Task<ConcurrentDictionary<string, Version>> CollectPackageAsync(Solution solution,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var projects = solution.Projects.ToArray();
            var tasks = ArrayPool<Task>.Shared.Rent(projects.Length);
            var references = new ConcurrentDictionary<string, Version>();
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
                            _logger.LogError(exception, $"Error in project {project.FilePath}");
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

        private void ProcessProject(Project project, ConcurrentDictionary<string, Version> references)
        {
            _logger.LogDebug($"Process {project?.FilePath}");

            if (project?.FilePath == null || !File.Exists(project.FilePath))
            {
                _logger.LogWarning($"Project {project?.FilePath} not found!");
                return;
            }

            var document = new XmlDocument();
            document.Load(project.FilePath);
            var items = document.GetElementsByTagName("ItemGroup");

            foreach (var item in items)
            {
                var node = item as XmlNode;

                if (node == null)
                    continue;

                foreach (var child in node.ChildNodes)
                {
                    var reference = child as XmlNode;
                    if (reference == null)
                        continue;

                    if (!reference.Name.Equals("PackageReference", StringComparison.CurrentCultureIgnoreCase))
                        continue;

                    string name = null;
                    Version version = null;
                    XmlAttribute versionAttr = null;

                    if (reference.Attributes == null)
                        continue;

                    foreach (var attribute in reference.Attributes)
                    {
                        var attr = attribute as XmlAttribute;

                        if (attr == null)
                            continue;

                        if (attr.Name.Equals("Include", StringComparison.InvariantCultureIgnoreCase))
                        {
                            name = attr.Value;
                            continue;
                        }

                        if (attr.Name.Equals("Version", StringComparison.InvariantCultureIgnoreCase))
                        {
                            var parsed = Version.TryParse(attr.Value, out version);

                            if (!parsed)
                                continue;

                            versionAttr = attr;
                        }
                    }

                    if (versionAttr != null)
                        reference.Attributes.Remove(versionAttr);

                    if (name != null && version != null)
                    {
                        _logger.LogDebug($"Found {name} {version}");
                        references.AddOrUpdate(name, version, (k, v) => v > version ? v : version);
                    }
                }
            }

            document.Save(project.FilePath);
        }

        private void ValidatePath()
        {
            if (!_configuration.Solution.EndsWith(".sln"))
                throw new ArgumentException($"Path {_configuration.Solution} is not a solution!");

            if (!File.Exists(_configuration.Solution))
                throw new ArgumentException($"Solution {_configuration.Solution} not found!");
        }

        private Task<Solution> LoadSolutionAsync(CancellationToken cancellationToken = default)
        {
            var workspace = MSBuildWorkspace.Create();
            workspace.LoadMetadataForReferencedProjects = true;
            return workspace.OpenSolutionAsync(_configuration.Solution, new Progress<ProjectLoadProgress>(progress => _logger.LogDebug($"{progress.Operation} {progress.FilePath}")), cancellationToken);
        }
    }
}