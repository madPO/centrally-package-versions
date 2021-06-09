namespace CentrallyPackageVersions
{
    using CommandLine;
    using System;

    /// <summary>
    /// Command-line arguments
    /// </summary>
    public class Configuration
    {
        /// <summary>
        /// Solution path
        /// </summary>
        [Option('p', "project", Required = true, HelpText = "Project name or path")]
        public string Solution { get; set; }

        /// <summary>
        /// Show log info
        /// </summary>
        [Option('v', "verbose", Required = false, Default = false)]
        public bool Verbose { get; set; }

        /// <summary>
        /// Package selection rule
        /// </summary>
        [Option('r', "resolve", Required = false, Default = TakeVersion.Max)]
        public TakeVersion ConflictResolve { get; set; }

        /// <summary>
        /// Timeout
        /// </summary>
        [Option('t', "timeout", Required = false)]
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);
    }

    /// <summary>
    /// Package selection rule
    /// </summary>
    public enum TakeVersion
    {
        /// <summary>
        /// Select max version
        /// </summary>
        Max,

        /// <summary>
        /// Select min version
        /// </summary>
        Min
    }
}