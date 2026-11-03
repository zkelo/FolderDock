using FolderDock.Core;
using Xunit;

namespace FolderDock.Core.Tests;

public sealed class CommandLineTests
{
    [Theory]
    [InlineData("\"C:\\Program Files\\FolderDock\\FolderDock.exe\" --background", true)]
    [InlineData("--BACKGROUND", true)]
    [InlineData("", false)]
    [InlineData("\"C:\\Apps\\--background\\FolderDock.exe\"", false)]
    [InlineData("--folder \"C:\\Photos\"", false)]
    [InlineData("--folder \"C:\\--background\"", false)]
    [InlineData("--folder --background", false)]
    [InlineData("--background --folder \"C:\\Photos\"", false)]
    public void BackgroundOnlyAppliesToBackgroundLaunches(string arguments, bool expected)
    {
        Assert.Equal(expected, CommandLine.IsBackground(arguments));
    }

    [Theory]
    [InlineData("--folder \"C:\\My Photos\"", "C:\\My Photos")]
    [InlineData("--FOLDER D:", "D:\\")]
    [InlineData("--folder \"\\\\server\\shared photos\"", "\\\\server\\shared photos")]
    [InlineData("\"C:\\--folder\\FolderDock.exe\" --background", null)]
    [InlineData("--folder", null)]
    [InlineData("--folder \"\"", null)]
    public void ExistingFolderShortcutsStillParse(string arguments, string? expected)
    {
        Assert.Equal(expected, CommandLine.FolderFrom(arguments));
    }

    [Fact]
    public void MainArgumentsAndRedirectedCommandLineAgree()
    {
        string[] args = ["--background", "--folder", @"C:\My Photos"];
        const string redirected = "--background --folder \"C:\\My Photos\"";
        Assert.Equal(CommandLine.FolderFrom(args), CommandLine.FolderFrom(redirected));
        Assert.Equal(CommandLine.IsBackground(args), CommandLine.IsBackground(redirected));
    }
}
