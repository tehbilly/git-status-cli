﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Schema;
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
        private const ConsoleColor BranchReportingColor = ConsoleColor.Blue;

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
                string repoPath = Repository.Discover(path);
                
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

                    //console.Write(" Outdated", ConsoleColor.Red);
                    console.WriteLine();
                }
            }
            
            var repository = new Repository(path);

            console.Write(repository.Info.WorkingDirectory.TrimEnd(new[] { '\\', '/' }));

            var status = repository.RetrieveStatus();

            if (status.IsDirty)
            {
                FlagError();

                if (status.HasStagedChanges())
                {
                    console.WriteIndent(1);
                    console.Write("- Staged changes: ");
                    WriteChanges(console, status.Added.Count(), status.Staged.Count(), status.Removed.Count(), status.RenamedInIndex.Count());
                }

                if (status.HasUnstagedChanges())
                {
                    console.WriteIndent(1);
                    console.Write("- Unstaged changes: ");
                    WriteChanges(console, status.Untracked.Count(), status.Modified.Count(), status.Missing.Count(), status.RenamedInWorkDir.Count());
                }
            }
            
            var branches = repository.Branches;
            foreach (var branch in branches.Where(b => b.IsRemote == false))
            {
                if (branch.IsTracking)
                {
                    if (branch.TrackingDetails?.AheadBy != null && branch.TrackingDetails.AheadBy.Value > 0 ||
                        branch.TrackingDetails?.BehindBy != null && branch.TrackingDetails.BehindBy.Value > 0)
                    {
                        bool hasAheadCommits = false;
                        
                        FlagError();
                        
                        console.WriteIndent(1);
                        console.Write($"- Branch: {branch.FriendlyName}...{branch.TrackedBranch.FriendlyName} ");
                        console.Write("[", BranchReportingColor);

                        if (branch.TrackingDetails?.AheadBy != null && branch.TrackingDetails.AheadBy.Value > 0)
                        {
                            hasAheadCommits = true;
                            console.Write($"{branch.TrackingDetails.AheadBy.Value} ahead", BranchReportingColor);
                        }
                        if (branch.TrackingDetails?.BehindBy != null && branch.TrackingDetails.BehindBy.Value > 0)
                        {
                            if (hasAheadCommits)
                                console.Write(", ",BranchReportingColor);
                            console.Write($"{branch.TrackingDetails.BehindBy.Value} behind", BranchReportingColor);
                        }

                        console.Write("]", BranchReportingColor);
                        console.WriteLine();
                    }
                }
                else
                {
                    FlagError();
                    
                    console.WriteIndent(1);
                    console.Write($"- Branch: {branch.FriendlyName} ");
                    console.Write("[non-tracking]", BranchReportingColor);
                    console.WriteLine();
                }
            }

            if (!hasError)
            {
                console.Write(" ...OK", ConsoleColor.Green);
                console.WriteLine();
            }
        }

        public void WriteChanges(IConsole console, int added, int modified, int deleted, int renamed)
        {
            bool hasWrittenYet = false;

            if (added > 0)
            {
                console.Write($"{added} added", ConsoleColor.Green);

                hasWrittenYet = true;
            }

            if (modified > 0)
            {
                if (hasWrittenYet)
                    console.Write(" · ");
                        
                console.Write($"{modified} modified", ConsoleColor.Yellow);

                hasWrittenYet = true;
            }

            if (deleted > 0)
            {
                if (hasWrittenYet)
                    console.Write(" · ");

                console.Write($"{deleted} deleted", ConsoleColor.Red);

                hasWrittenYet = true;
            }

            if (renamed > 0)
            {
                if (hasWrittenYet)
                    console.Write(" · ");

                console.Write($"{renamed} renamed", ConsoleColor.Blue);
            }

            console.WriteLine();

        }
    }
}
