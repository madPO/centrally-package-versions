namespace CentrallyPackageVersions
{
    using CommandLine;
    using System;

    public class Configuration
    {
        [Option('p', "project", Required = true, HelpText = "Project name or path")]
        public string Solution { get; set; }
        
        [Option('v', "verbose", Required = false, Default = false)]
        public bool Verbose { get; set; }
        
        [Option('t', "take-version", Required = false, Default = TakeVersion.Max)]
        public TakeVersion ConflictResolve { get; set; }
        
        [Option('t', "timeout", Required = false)]
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);
    }

    public enum TakeVersion
    {
        Max,
        
        Min
    }
}