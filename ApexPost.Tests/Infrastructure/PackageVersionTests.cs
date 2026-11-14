using System.Xml.Linq;

namespace ApexPost.Tests.Infrastructure;

/// <summary>Reproducible builds: every PackageReference in the repository uses an exact version.</summary>
public sealed class PackageVersionTests
{
    [Fact]
    public void Every_package_reference_has_an_exact_version_without_wildcards_or_ranges()
    {
        var root = FindRepositoryRoot();
        var projectFiles = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(root, "*.props", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path))
            .ToArray();
        Assert.NotEmpty(projectFiles);

        var problems = new List<string>();
        foreach (var file in projectFiles)
        {
            foreach (var reference in XDocument.Load(file).Descendants("PackageReference"))
            {
                var name = (string?)reference.Attribute("Include") ?? (string?)reference.Attribute("Update") ?? "?";
                var version = (string?)reference.Attribute("Version") ?? reference.Element("Version")?.Value;

                // $(MauiVersion) is supplied by the installed MAUI workload, which is pinned by the SDK workload set.
                if (version is null || version.StartsWith("$(", StringComparison.Ordinal))
                {
                    continue;
                }

                if (version.Contains('*') || version.Contains('[') || version.Contains('(') || version.Contains(','))
                {
                    problems.Add($"{Path.GetRelativePath(root, file)}: {name} {version}");
                }
            }
        }

        Assert.True(problems.Count == 0, "Floating package versions found:" + Environment.NewLine +
                                         string.Join(Environment.NewLine, problems));
    }

    private static bool IsBuildOutput(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}bin{separator}", StringComparison.Ordinal) ||
               path.Contains($"{separator}obj{separator}", StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApexPost.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("ApexPost.slnx was not found above the test output folder.");
    }
}
