namespace IntegrationTest.utils
{
    using Microsoft.Build.Construction;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public static class Validate
    {
        public static void BuildProps(string solutionPath)
        {
            var path = Path.Combine(PreparePath(solutionPath), "Directory.Build.props");

            if (!File.Exists(path))
                throw new InvalidOperationException("Directory.Build.props not found!");

            var project = ProjectRootElement.Open(path);

            var exists = project.PropertyGroups.SelectMany(x => x.Children)
                .Any(x => x.ElementName == "ManagePackageVersionsCentrally");

            if (!exists)
                throw new InvalidOperationException("Directory.Build.props invalid format!");
        }

        public static void PackagesProps(string solutionPath, Dictionary<string, string> packages)
        {
            var path = Path.Combine(PreparePath(solutionPath), "Directory.Packages.props");

            if (!File.Exists(path))
                throw new InvalidOperationException("Directory.Packages.props not found!");

            var project = ProjectRootElement.Open(path);

            var exists = project.ItemGroups.SelectMany(x => x.Children)
                .Any(x => x.ElementName == "PackageVersion");

            if (!exists)
                throw new InvalidOperationException("Directory.Packages.props invalid format!");

            var references = project.ItemGroups.SelectMany(x => x.Children).Select(x => x as ProjectItemElement)
                .ToArray();

            foreach (var package in packages)
            {
                var reference = references.Where(x => x.Include == package.Key).Single();
                
                if(reference.Metadata.Where(x => x.Name == "Version").Single().Value != package.Value)
                    throw new InvalidOperationException($"Package {packages.Keys} not contains version {package.Value}"); 
            }
        }

        public static void Projects(string solutionPath)
        {
            if (!File.Exists(solutionPath))
                throw new InvalidOperationException("Directory.Packages.props not found!");

            var solution = SolutionFile.Parse(solutionPath);
            var projects = solution.ProjectsInOrder.Select(x => ProjectRootElement.Open(x.AbsolutePath)).ToArray();

            var noVersion = projects.SelectMany(x => x.ItemGroups).SelectMany(x => x.Items)
                .Where(x => x.ElementName == "PackageReference")
                .All(x => !x.Metadata.Any(x => x.Name == "Version"));

            if (!noVersion)
                throw new InvalidOperationException("Project contains PackageReference with version attribute!");
        }

        private static string PreparePath(string solutionPath)
        {
            var path = Path.IsPathFullyQualified(solutionPath)
                ? solutionPath
                : Path.GetFullPath(solutionPath, Environment.CurrentDirectory);

            return Path.GetDirectoryName(path);
        }
    }
}