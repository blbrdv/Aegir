using System.IO;
using System.Linq;
using System.Text;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tools.DotNet;
using Fallout.Common.Utilities.Collections;
using Fallout.Solutions;
using Serilog;
using Tomlyn;
using Tomlyn.Model;
using static Fallout.Common.Tools.DotNet.DotNetTasks;
using Solution = Fallout.Common.ProjectModel.Solution;

// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable AllUnderscoreLocalParameterName
// ReSharper disable UnusedMember.Local

// ReSharper disable once ArrangeTypeModifiers
class Build : FalloutBuild
{
    // ReSharper disable once ConvertToConstant.Local
    // ReSharper disable once InconsistentNaming
    [Parameter("Path to the BepInEx plugin folder, default is 'BepInEx\\plugins'")]
    readonly string BepinexPath = @"BepInEx\plugins";

    // ReSharper disable once InconsistentNaming
    [Parameter("Configuration to build, default is 'Debug'")]
    readonly Configuration BuildConfig = Configuration.Debug;

    // ReSharper disable once InconsistentNaming
    [Parameter("Path to the root of game folder")] 
    readonly string GamePath;

    // ReSharper disable once InconsistentNaming
    [Fallout.Common.ProjectModel.Solution(GenerateProjects = true)] 
    readonly Solution Solution;

    readonly Encoding _utf8NoBom = new UTF8Encoding(false);

    Project AegirProject => Solution.GetProject("Aegir");
    string ProjectTargetFramework => AegirProject.GetTargetFrameworks()!.First();
    string CompiledFileName => AegirProject.Name + ".dll";
    string PackedFileName => AegirProject.Name + "-" + AegirProject.GetProperty("version") + ".zip";
    
    static AbsolutePath MainChangelog => RootDirectory / "CHANGELOG.md";
    static AbsolutePath SourceDirectory => RootDirectory / "src";
    static AbsolutePath OutputDirectory => RootDirectory / "output";
    static AbsolutePath NexusModsOutputDirectory => OutputDirectory / "NexusMods";
    static AbsolutePath ThunderStoreOutputDirectory => OutputDirectory / "ThunderStore";
    static AbsolutePath ArchiveDirectory => RootDirectory / "_tmp";
    static AbsolutePath NexusModsDistDirectory => RootDirectory / "dist" / "NexusMods";
    static AbsolutePath NexusModsChangelog => NexusModsDistDirectory / "CHANGELOG.txt";
    static AbsolutePath NexusModsPartChangelog => NexusModsDistDirectory / "CHANGELOG.part.txt";
    static AbsolutePath ThunderStoreDistDirectory => RootDirectory / "dist" / "ThunderStore";
    static AbsolutePath ThunderStoreChangelog => ThunderStoreDistDirectory / "CHANGELOG.md";
    static AbsolutePath ThunderStorePartConfigFile => ThunderStoreDistDirectory / "thunderstore.part.toml";
    static AbsolutePath ThunderStoreConfigFile => ThunderStoreDistDirectory / "thunderstore.toml";

    AbsolutePath PluginsDirectory =>
        AbsolutePath.Create(Path.Combine(GamePath, BepinexPath));
    
    AbsolutePath BuildDirectory => SourceDirectory / "bin" / BuildConfig / ProjectTargetFramework;
    AbsolutePath CompiledFilePath => BuildDirectory / CompiledFileName;
    AbsolutePath CompiledPluginPath => PluginsDirectory / CompiledFileName;

    Target Clean => _ => _
        .Description("Clean build and output directories")
        .Before(Restore)
        .Executes(() =>
        {
            var buildDirs = SourceDirectory.GlobDirectories("**/bin", "**/obj");
            if (buildDirs.Count > 0)
            {
                Log.Information("Removing dirs [{Dirs}]", string.Join(", ", buildDirs));
                buildDirs.DeleteDirectories();
            }

            Log.Information("Removing {Directory}", OutputDirectory);
            OutputDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .Description("Look for and download project dependencies")
        .Requires(() => BuildConfig)
        .Before(Compile)
        .After(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(AegirProject));
        });

    Target Compile => _ => _
        .Description("Build project")
        .Requires(() => BuildConfig)
        .Requires(() => GamePath)
        .Before(PackForNexusmods)
        .Before(PackForThunderstore)
        .After(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(AegirProject)
                .SetConfiguration(BuildConfig)
                .AddProperty("GamePath", GamePath)
                .EnableNoRestore());
        });

    Target Install => _ => _
        .Description("Copy compiled file to game plugins directory")
        .After(Compile)
        .Executes(() =>
        {
            CompiledPluginPath.DeleteFile();
            Log.Information("Deleting {Path}", CompiledPluginPath);

            CompiledFilePath.CopyToDirectory(PluginsDirectory);
            Log.Information("Plugin copied to {Target}", PluginsDirectory);
        });

    Target PackForNexusmods => _ => _
        .Description("Archive compiled and other necessary files for NexusMods in output directory")
        .After(Compile)
        .Executes(() =>
        {
            NexusModsOutputDirectory.CreateOrCleanDirectory();
            ArchiveDirectory.CreateOrCleanDirectory();
            
            var currentVersion = AegirProject.GetProperty("version");
            var changelog = MainChangelog.ReadAllLines()[1..];

            var packedFile = NexusModsOutputDirectory / PackedFileName;
            var currentChangelogFile = NexusModsDistDirectory / $"CHANGELOG.{currentVersion}.txt";
            
            var changelogCurrent = changelog[3..]
                .TakeUntil(line => line.StartsWith("## "))
                .Select(line => line.TrimStart('#', ' '))
                .Select(line => line.Replace("[", "").Replace("]", ""));
            
            var nexusChangelog = NexusModsPartChangelog.ReadAllLines()
                .Concat(changelog
                    .Select(line => line.TrimStart('#', ' '))
                    .Select(line => line.Replace("[", "").Replace("]", "")))
                .SkipLast(2);

            NexusModsChangelog
                .TouchFile()
                .WriteAllLines(nexusChangelog, _utf8NoBom);
            
            currentChangelogFile
                .TouchFile()
                .WriteAllLines(changelogCurrent, _utf8NoBom);
            
            CompiledFilePath.CopyToDirectory(ArchiveDirectory);
            
            ArchiveDirectory.ZipTo(packedFile);
            NexusModsDistDirectory
                .GetFiles()
                .WhereNot(file => file.Name.Equals("CHANGELOG.part.txt"))
                .ForEach(file => file.CopyToDirectory(NexusModsOutputDirectory));
            
            currentChangelogFile.DeleteFile();
            NexusModsChangelog.DeleteFile();
            ArchiveDirectory.DeleteDirectory();

            Log.Information("Packed files to {Target}", NexusModsOutputDirectory);
        });

    Target PackForThunderstore => _ => _
        .Description("Archive compiled and other necessary files for ThunderStore in output directory")
        .After(Compile)
        .Executes(() =>
        {
            var changelog = MainChangelog.ReadAllLines()
                .TakeUntil(line => line.StartsWith('['))
                .Select(line => line.Replace("[", "").Replace("]", ""))
                .SkipLast(2);

            ThunderStoreChangelog
                .TouchFile()
                .WriteAllLines(changelog, _utf8NoBom);

            var currentVersion = AegirProject.GetProperty("version");
            
            {
                using var input = File.OpenRead(ThunderStorePartConfigFile);
                var table = TomlSerializer.Deserialize<TomlTable>(input)!;
                var package = (TomlTable)table["package"]!;
                var build = (TomlTableArray)((TomlTable)table["build"]!)["copy"]!;

                package.Add("namespace", AegirProject.GetProperty("authors"));
                package.Add("name", AegirProject.GetProperty("product"));
                package.Add("version", currentVersion);
                package.Add("description", AegirProject.GetProperty("description"));

                build.Add(new TomlTable
                {
                    ["source"] = Path.Combine("../..",
                        Path.GetRelativePath(RootDirectory.ToString(), CompiledFilePath.ToString())),
                    ["target"] = "./Aegir.dll"
                });

                if (File.Exists(ThunderStoreConfigFile))
                    File.Delete(ThunderStoreConfigFile);

                using var config = File.Create(ThunderStoreConfigFile);
                TomlSerializer.Serialize(config, table);
            }

            DotNet($"tcli build --config-path {ThunderStoreConfigFile} --package-version {currentVersion}");
            
            ThunderStoreChangelog.DeleteFile();
            ThunderStoreConfigFile.DeleteFile();
            
            Log.Information("Packed files to {Target}", ThunderStoreOutputDirectory);
        });

    Target Pack => _ => _
        .Description("Archive compiled and other necessary files for mod stores in output directory")
        .DependsOn(PackForNexusmods)
        .DependsOn(PackForThunderstore)
        .Executes(() =>
        {
            Log.Information("Packed files to {Target}", OutputDirectory);
        });

    public static int Main() => Execute<Build>(x => x.Compile);
}
