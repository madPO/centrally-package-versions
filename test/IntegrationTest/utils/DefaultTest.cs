namespace IntegrationTest.utils
{
    using Microsoft.Extensions.FileProviders;
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    public class DefaultTest
    {
        protected string PrepareSolution(string embeddedSolution, string testName)
        {
            var pathItems = embeddedSolution.Split("/");
            var solutionName = pathItems.Last();

            var provider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly(),
                $"IntegrationTest.projects.{string.Join('.', pathItems.Where(x => x != solutionName))}");
            var solutionFiles = provider.GetDirectoryContents(string.Empty);
            var path = Path.Combine(Environment.CurrentDirectory, "projects", testName);
            Directory.CreateDirectory(path);
            foreach (var file in solutionFiles)
            {
                using var original = file.CreateReadStream();
                var elementPath = Path.Combine(path, file.Name);

                if (File.Exists(elementPath))
                    File.Delete(elementPath);

                using var clone = File.Create(elementPath);
                original.CopyTo(clone);
            }

            return Path.Combine(path, solutionName);
        }
    }
}