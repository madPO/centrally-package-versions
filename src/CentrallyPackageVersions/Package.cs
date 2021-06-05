namespace CentrallyPackageVersions
{
    using Microsoft.Build.Construction;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Package reference
    /// </summary>
    public class Package : IComparable<Package>, IEquatable<Package>
    {
        /// <summary>
        /// Name of package
        /// </summary>
        public string Name { get; private set; }
        
        /// <summary>
        /// Version of package
        /// </summary>
        public Version Version { get; private set; }
        
        /// <summary>
        /// Other attributes
        /// </summary>
        public IReadOnlyCollection<ProjectMetadataElement> Attributes { get; private set; }

        /// <summary>
        /// Parse <see cref="ProjectItemElement"/> to <see cref="Package"/>
        /// </summary>
        public static Package Parse(ProjectItemElement element)
        {
            var package = new Package();
            try
            {
                package.Name = element.Include;
                var metadata = new List<ProjectMetadataElement>();
                foreach (var item in element.Metadata)
                {
                    if (!item.ExpressedAsAttribute)
                    {
                        continue;
                    }
                    
                    if (item.ElementName.Equals("Version", StringComparison.CurrentCultureIgnoreCase))
                    {
                        package.Version = Version.Parse(item.Value);
                    }
                    else
                    {
                        metadata.Add(item);
                    }
                }

                package.Attributes = metadata;
            }
            catch
            {
                package = null;
            }
            
            return package;
        }

        /// <inheritdoc />
        public int CompareTo(Package other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            if (ReferenceEquals(null, other))
            {
                return 1;
            }

            return Comparer<Version>.Default.Compare(Version, other.Version);
        }

        /// <inheritdoc />
        public bool Equals(Package other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Name == other.Name && Equals(Version, other.Version);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((Package) obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Version);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Name} ({Version})";
        }
    }
}