using System;
using System.Linq;
using System.IO;
using System.Text;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using SharpConfig;
using Cfg = SharpConfig.Configuration;
// ReSharper disable AllUnderscoreLocalParameterName
// ReSharper disable UnusedMember.Local

class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build, default is 'Debug'")]
    readonly Configuration BuildConfig = Configuration.Release;

    [Solution(GenerateProjects = true)]
    readonly Solution Solution;

    readonly Section GamePaths = Cfg.LoadFromFile("build.cfg")["Paths"];
    
    readonly Encoding UTF8NoBOM = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    
    Project AegirProject => Solution.Aegir; // just ignore CS1061, it's fine
    string ProjectTargetFramework => AegirProject.GetTargetFrameworks()!.First();
    AbsolutePath MainChangelog => RootDirectory / "CHANGELOG.md";
    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath OutputDirectory => RootDirectory / "output";
    AbsolutePath NexusModsOutputDirectory => OutputDirectory / "NexusMods";
    AbsolutePath ThunderStoreOutputDirectory => OutputDirectory / "ThunderStore";
    AbsolutePath ArchiveDirectory => RootDirectory / "_tmp";
    AbsolutePath NexusModsDistDirectory => RootDirectory / "dist" / "NexusMods";
    AbsolutePath NexusModsChangelog => NexusModsDistDirectory / "CHANGELOG.txt";
    AbsolutePath ThunderStoreDistDirectory => RootDirectory / "dist" / "ThunderStore";
    AbsolutePath ThunderStoreChangelog => ThunderStoreDistDirectory / "CHANGELOG.md";
    AbsolutePath GameDirectory => AbsolutePath.Create(GamePaths["game"].StringValue);
    AbsolutePath PluginsDirectory => 
        AbsolutePath.Create(Path.Combine(GameDirectory, GamePaths["bepinex"].StringValue));
    AbsolutePath BuildDirectory => SourceDirectory / "bin" / BuildConfig / ProjectTargetFramework;
    string CompiledFileName => AegirProject.Name + ".dll";
    AbsolutePath CompiledFilePath => BuildDirectory / CompiledFileName;
    AbsolutePath CompiledPluginPath => PluginsDirectory / CompiledFileName; 
    string PackedFileName => AegirProject.Name + "-" + AegirProject.GetProperty("version") + ".zip";

    /// <summary>Clean build and output directories</summary>
    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            var buildDirs = SourceDirectory.GlobDirectories("**/bin", "**/obj");
            if (buildDirs.Count > 0)
            {
                Log.Information("Removing dirs [{0}]", string.Join(", ", buildDirs));
                buildDirs.DeleteDirectories();
            }

            Log.Information("Removing {0}", OutputDirectory);
            OutputDirectory.CreateOrCleanDirectory();
        });

    /// <summary>Look for and download project dependencies</summary>
    Target Restore => _ => _
        .Requires(() => BuildConfig)
        .Before(Compile)
        .After(Clean)
        .Executes(() =>
        {
            Environment.SetEnvironmentVariable("GAME_PATH", GameDirectory);
            Log.Debug("Env GAME_PATH set to {Path}", GameDirectory);

            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    /// <summary>Build project</summary>
    Target Compile => _ => _
        .Requires(() => BuildConfig)
        .Before(PackForNexusmods)
        .Before(PackForThunderstore)
        .After(Restore)
        .Executes(() =>
        {
            Environment.SetEnvironmentVariable("GAME_PATH", GameDirectory);
            Log.Debug("Env GAME_PATH set to {Path}", GameDirectory);

            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(BuildConfig)
                .EnableNoRestore());
        });

    /// <summary>Copy compiled file to game plugins directory</summary>
    Target Install => _ => _
        .After(Compile)
        .Executes(() => {
            CompiledPluginPath.DeleteFile();
            Log.Information("Deleting {Path}", CompiledPluginPath);

            CompiledFilePath.CopyToDirectory(PluginsDirectory);
            Log.Information("Plugin copied to {Target}", PluginsDirectory);
        });

    /// <summary>Pack compiled and other necessary files to zip for NexusMods in output directory</summary>
    Target PackForNexusmods => _ => _
        .After(Compile)
        .Executes(() =>
        {
            var changelog = new []
                {
                    "-------------------------------------------------------------------------------",
                    "                                Changelog",
                    "-------------------------------------------------------------------------------"
                }
                .Concat(
                    MainChangelog.ReadAllLines()[1..]
                        .TakeUntil(line => line.StartsWith('['))
                        .Select(line => line.TrimStart('#', ' '))
                )
                .Select(line => line.Replace("[", "").Replace("]", ""))
                .SkipLast(2);
            
            NexusModsChangelog
                .TouchFile()
                .WriteAllLines(changelog, UTF8NoBOM);
            
            ArchiveDirectory.CreateOrCleanDirectory();

            CompiledFilePath.CopyToDirectory(ArchiveDirectory);
            ArchiveDirectory.ZipTo(NexusModsOutputDirectory / PackedFileName);
            NexusModsDistDirectory
                .GetFiles()
                .ForEach(file => file.CopyToDirectory(NexusModsOutputDirectory));
            
            NexusModsChangelog.DeleteFile();
            ArchiveDirectory.DeleteDirectory();
            
            Log.Information("Packed files to {Target}", NexusModsOutputDirectory);
        });

    /// <summary>Pack compiled and other necessary files to zip for ThunderStore in output directory</summary>
    Target PackForThunderstore => _ => _
        .After(Compile)
        .Executes(() =>
        {
            var changelog = MainChangelog.ReadAllLines()
                .TakeUntil(line => line.StartsWith('['))
                .Select(line => line.Replace("[", "").Replace("]", ""))
                .SkipLast(2);

            ThunderStoreChangelog
                .TouchFile()
                .WriteAllLines(changelog, UTF8NoBOM);
            
            ArchiveDirectory.CreateOrCleanDirectory();

            CompiledFilePath.CopyToDirectory(ArchiveDirectory);
            ThunderStoreDistDirectory.GetFiles().ForEach(file => file.CopyToDirectory(ArchiveDirectory));
            ArchiveDirectory.ZipTo(ThunderStoreOutputDirectory / PackedFileName);
            
            ThunderStoreChangelog.DeleteFile();
            ArchiveDirectory.DeleteDirectory();
            
            Log.Information("Packed files to {Target}", ThunderStoreOutputDirectory);
        });

    /// <summary>Pack compiled and other necessary files to zip in output directory</summary>
    Target Pack => _ => _
        .After(PackForNexusmods)
        .After(PackForThunderstore)
        .Executes(() =>
        {
            Log.Information("Packed files to {Target}", OutputDirectory);
        });
}
