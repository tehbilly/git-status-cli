using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using LibGit2Sharp;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;

namespace GitStatusCli
{
    [Command(
        Name = "git-status-cli", 
        FullName = "A simple command-line utility to report status of Git repositories.")]
    [VersionOptionFromMember(MemberName = nameof(GetVersion))]
    class Program : CommandBase
    {
        private readonly IFileSystem _fileSystem;

        [Option(CommandOptionType.MultipleValue, Description = "The path to scan.", ShortName = "p", LongName = "path", ValueName = "PATH")]
        public List<string> Paths { get; set; }

        public static int Main(string[] args)
        {
            var services = new ServiceCollection()
                .AddSingleton<IConsole, PhysicalConsole>()
                .AddSingleton<IFileSystem, FileSystem>() 
                .AddSingleton<IReporter>(provider => new ConsoleReporter(provider.GetService<IConsole>()))
                .BuildServiceProvider();

            var app = new CommandLineApplication<Program>
            {
                ThrowOnUnexpectedArgument = false
            };
            app.Conventions
                .UseDefaultConventions()
                .UseConstructorInjection(services);
         
            return app.Execute(args);
        }
        
        public static string GetVersion() => typeof(Program)
            .Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            .InformationalVersion;

        public Program(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }
        
        public async Task<int> OnExecute(CommandLineApplication app, IConsole console)
        {
            if (Paths == null || Paths.Count == 0)
                Paths = new List<string> { _fileSystem.Directory.GetCurrentDirectory() };
            
            foreach (var path in Paths)
            {
                string repoPath = LibGit2Sharp.Repository.Discover(path);
                
                if (!string.IsNullOrEmpty(repoPath))
                {
                    await ScanDirectory(repoPath, console);
                }
                else
                {
                    var subDirectories = _fileSystem.Directory.GetDirectories(path, ".git", SearchOption.AllDirectories);

                    foreach (var subDirectory in subDirectories)
                    {
                        if (LibGit2Sharp.Repository.IsValid(subDirectory))
                        {
                            await ScanDirectory(subDirectory, console);
                        }
                    }
                }
            }            

            return 0;
        }

        private async Task ScanDirectory(string path, IConsole console)
        {
            
            var repository = new LibGit2Sharp.Repository(path);
            console.WriteLine(repository.Info.WorkingDirectory);

            var status = repository.RetrieveStatus();
            if (status.IsDirty)
                Console.WriteLine("- Has uncommitted changes");
            var branches = repository.Branches;
            foreach (var branch in branches.Where(b => b.IsRemote == false))
            {
                if (branch.IsTracking)
                {
                    if (branch.TrackingDetails?.AheadBy != null && branch.TrackingDetails.AheadBy.Value > 0)
                    {
                        console.WriteLine($"- Branch {branch.FriendlyName} has {branch.TrackingDetails.AheadBy.Value} non-pushed commits");
                    }
                }
                else
                    console.WriteLine($"- Branch {branch.FriendlyName} is non tracking");
            }
        }
    }
}
