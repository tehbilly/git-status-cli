using System;
using McMaster.Extensions.CommandLineUtils;

namespace GitStatusCli
{
    public static class ConsoleExtensions
    {
        public static void Write(this IConsole console, object value, ConsoleColor foregroundColor)
        {
            ConsoleColor currentColor = console.ForegroundColor;
            
            console.ForegroundColor = foregroundColor;
            console.Write(value);
            console.ForegroundColor = currentColor;
        }

        public static void Write(this IConsole console, object value, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
        {
            ConsoleColor currentForegroundColor = console.ForegroundColor;
            ConsoleColor currentBackgroundColor = console.BackgroundColor;
            
            console.ForegroundColor = foregroundColor;
            console.BackgroundColor = backgroundColor;
            console.Write(value);
            console.ForegroundColor = currentForegroundColor;
            console.BackgroundColor = currentBackgroundColor;
        }

    }
}