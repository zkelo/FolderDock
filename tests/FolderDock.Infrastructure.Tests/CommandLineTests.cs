using FolderDock.Infrastructure;
using Xunit;

namespace FolderDock.Infrastructure.Tests;

public sealed class CommandLineTests
{
    [Fact]
    public void ParsingReturnsOneUnambiguousLaunchIntent()
    {
        Assert.Equal(LaunchKind.Manager, CommandLine.Parse("").Kind);
        Assert.Equal(LaunchKind.Background, CommandLine.Parse("--background").Kind);
        var folder = CommandLine.Parse("--background --folder \"C:\\My Photos\"");
        Assert.Equal(LaunchKind.Folder, folder.Kind);
        Assert.Equal(@"C:\My Photos", folder.FolderPath);
    }

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
