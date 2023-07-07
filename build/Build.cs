using System;
using System.Linq;
using System.IO;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using SharpConfig;
using Cfg = SharpConfig.Configuration;

class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build, default is 'Debug'")]
    readonly Configuration BuildConfig = Configuration.Debug;

    [Solution(GenerateProjects = true)]
    readonly Solution Solution;

    readonly Section GamePaths = Cfg.LoadFromFile("build.cfg")["Paths"];
    
    Project AegirProject => Solution.Aegir;
    string ProjectTargetFramework => AegirProject.GetTargetFrameworks().First();
    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath OutputDirectory => RootDirectory / "output";
    AbsolutePath NexusModsOutputDirectory => OutputDirectory / "NexusMods";
    AbsolutePath ThunderStoreOutputDirectory => OutputDirectory / "ThunderStore";
    AbsolutePath ArchiveDirectory => RootDirectory / "_tmp";
    AbsolutePath NexusModsDistDirectory => RootDirectory / "dist" / "NexusMods";
    AbsolutePath ThunderStoreDistDirectory => RootDirectory / "dist" / "ThunderStore";
    AbsolutePath GameDirectory => AbsolutePath.Create(GamePaths["game"].StringValue);
    AbsolutePath PluginsDirectory => 
        AbsolutePath.Create(Path.Combine(GameDirectory, GamePaths["bepinex"].StringValue));
    AbsolutePath BuildDirectory => SourceDirectory / "bin" / BuildConfig / ProjectTargetFramework;
    string CompiledFileName => AegirProject.Name + ".dll";
    AbsolutePath CompiledFilePath => BuildDirectory / CompiledFileName;
    string PackedFileName => AegirProject.Name + "-" + AegirProject.GetProperty("version") + ".zip";


    /// <summary>Clean build and output directories</summary>
    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").DeleteDirectories();
            OutputDirectory.CreateOrCleanDirectory();
        });


    /// <summary>Look for and download project dependencies</summary>
    Target Restore => _ => _
        .Requires(() => BuildConfig)
        .Before(Compile)
        .DependsOn(Clean)
        .Executes(() =>
        {
            Environment.SetEnvironmentVariable("GAME_PATH", GameDirectory);

            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });


    /// <summary>Build project</summary>
    Target Compile => _ => _
        .Requires(() => BuildConfig)
        .Before(PackForNexusmods)
        .Before(PackForThunderstore)
        .DependsOn(Restore)
        .Executes(() =>
        {
            Environment.SetEnvironmentVariable("GAME_PATH", GameDirectory);

            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(BuildConfig)
                .EnableNoRestore());
        });


    /// <summary>Copy compiled file to game plugins directory</summary>
    Target Install => _ => _
        .DependsOn(Compile)
        .Executes(() => {
            (PluginsDirectory / CompiledFileName).DeleteFile();
            CopyFileToDirectory(CompiledFilePath, PluginsDirectory);
        });


    /// <summary>Pack compiled and other necassery files to zip for NexusMods in output directory</summary>
    Target PackForNexusmods => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            ArchiveDirectory.CreateOrCleanDirectory();

            CopyFileToDirectory(CompiledFilePath, ArchiveDirectory);
            ArchiveDirectory.ZipTo(NexusModsOutputDirectory / PackedFileName);
            NexusModsDistDirectory
                .GetFiles()
                .ForEach(file => CopyFileToDirectory(file, NexusModsOutputDirectory));

            ArchiveDirectory.DeleteDirectory();
        });


    /// <summary>Pack compiled and other necassery files to zip for ThunderStore in output directory</summary>
    Target PackForThunderstore => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            ArchiveDirectory.CreateOrCleanDirectory();

            CopyFileToDirectory(CompiledFilePath, ArchiveDirectory);
            ThunderStoreDistDirectory.GetFiles().ForEach(file => CopyFileToDirectory(file, ArchiveDirectory));
            ArchiveDirectory.ZipTo(ThunderStoreOutputDirectory / PackedFileName);

            ArchiveDirectory.DeleteDirectory();
        });


    /// <summary>Pack compiled and other necassery files to zip in output directory</summary>
    Target Pack => _ => _
        .DependsOn(PackForNexusmods)
        .DependsOn(PackForThunderstore)
        .Executes(() => {});


}
