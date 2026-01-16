using System.Runtime.InteropServices;
using FluentResults;

namespace KoshCLI.System;

internal class SystemHelper
{
    public static Result<OSPlatform> GetOsPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return OSPlatform.Windows;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return OSPlatform.Linux;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return OSPlatform.OSX;

        return Result.Fail("This OS is not supported");
    }
}
