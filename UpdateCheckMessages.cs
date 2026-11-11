using System;
using FolderDock.Core;

namespace FolderDock;

internal static class UpdateCheckMessages
{
    public static string Format(UpdateCheckResult.Failure failure) => failure.Error switch
    {
        UpdateError.InvalidReleaseVersion => Loc.Format("UpdateErr_BadTag", failure.Details),
        UpdateError.InvalidCurrentVersion => Loc.Format("UpdateErr_BadCurrent", failure.Details),
        UpdateError.InstallerUnavailable => Loc.Format("UpdateErr_NoInstaller",
            failure.Details, failure.Architecture ?? string.Empty),
        UpdateError.ReleaseUnavailable => Loc.Format("UpdateErr_Network", failure.Details),
        UpdateError.HttpFailure => failure.Details,
        _ => throw new ArgumentOutOfRangeException(nameof(failure))
    };
}
