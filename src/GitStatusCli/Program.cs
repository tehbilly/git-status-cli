using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using LibGit2Sharp;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;

namespace GitStatusCli
{
    [Command(
        Name = "git-status", 
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
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            if (Paths == null || Paths.Count == 0)
                Paths = new List<string> { _fileSystem.Directory.GetCurrentDirectory() };
            
            Console.WriteLine("Searching for Git repositories...");
            Console.WriteLine();

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
                        if (Repository.IsValid(subDirectory))
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
            bool hasError = false;

            void FlagError()
            {
                if (!hasError)
                {
                    hasError = true;

                    console.Write(" ...has issues", ConsoleColor.Red);
                    console.WriteLine();
                }
            }
            
            var repository = new LibGit2Sharp.Repository(path);

            console.Write(repository.Info.WorkingDirectory.TrimEnd(new[] { '\\', '/' }));

            var status = repository.RetrieveStatus();

            if (status.IsDirty)
            {
                FlagError();
                
                console.Write("\u00b1 ", ConsoleColor.Red);
                console.WriteLine("has uncommitted changes");
            }
            var branches = repository.Branches;
            foreach (var branch in branches.Where(b => b.IsRemote == false))
            {
                if (branch.IsTracking)
                {
                    if (branch.TrackingDetails?.AheadBy != null && branch.TrackingDetails.AheadBy.Value > 0)
                    {
                        FlagError();
                        
                        console.Write("\u2191 ", ConsoleColor.Green);
                        console.Write(branch.FriendlyName, ConsoleColor.Cyan);
                        console.Write(" is ");
                        console.Write(branch.TrackingDetails.AheadBy.Value, ConsoleColor.Green);
                        if (branch.TrackingDetails.AheadBy.Value == 1)
                            console.Write(" commit");
                        else
                            console.Write(" commits");
                        console.WriteLine(" ahead of remote tracking branch");
                    }
                }
                else
                {
                    FlagError();
                    
                    console.Write("\u0021 ", ConsoleColor.Yellow);
                    console.Write(branch.FriendlyName, ConsoleColor.Cyan);
                    console.WriteLine($" is non-tracking");
                }
            }

            if (!hasError)
            {
                console.Write(" ...ok", ConsoleColor.Green);
                console.WriteLine();
            }
        }
    }
}
