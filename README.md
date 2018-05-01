# git-status-cli

[![AppVeyor build status][appveyor-badge]](https://ci.appveyor.com/project/jerriep/git-status-cli/branch/master)

[appveyor-badge]: https://img.shields.io/appveyor/ci/jerriep/git-status-cli/master.svg?label=appveyor&style=flat-square

[![NuGet][main-nuget-badge]][main-nuget] [![MyGet][main-myget-badge]][main-myget]

[main-nuget]: https://www.nuget.org/packages/git-status-cli/
[main-nuget-badge]: https://img.shields.io/nuget/v/git-status-cli.svg?style=flat-square&label=nuget
[main-myget]: https://www.myget.org/feed/git-status-cli/package/nuget/git-status-cli
[main-myget-badge]: https://img.shields.io/www.myget/git-status-cli/vpre/git-status-cli.svg?style=flat-square&label=myget

A simple command-line utility to determine status of all Git repositories in a directory structure.

![](screenshot.png)

## Usage

```text
Usage: git-status [options]

Options:
  --version         Show version information
  -?|-h|--help      Show help information
  -p|--path <PATH>  The path to scan.
```

By default, **git-status** will scan for git repositories in the current directory and its sub-directories. You can specify the directory (and sub-directories) to scan by passing the `-p|--path` option. This option can be passed multiple times.