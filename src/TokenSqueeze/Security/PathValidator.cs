using System.Security;

namespace TokenSqueeze.Security;

internal static class PathValidator
{
    public static string ValidateWithinRoot(string path, string rootDir)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(rootDir);

        // Ensure root ends with separator for accurate prefix check
        if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
            fullRoot += Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fullPath, fullRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException($"Path '{path}' escapes root directory '{rootDir}'.");
        }

        return fullPath;
    }

    public static bool IsSymlinkEscape(string filePath, string rootDir)
    {
        try
        {
            var target = File.ResolveLinkTarget(filePath, returnFinalTarget: true);
            if (target is null)
                return false; // Not a symlink

            var resolvedPath = target.FullName;
            var fullRoot = Path.GetFullPath(rootDir);

            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
                fullRoot += Path.DirectorySeparatorChar;

            return !resolvedPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(resolvedPath, fullRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Fail safe: treat permission errors or any exception as escape
            return true;
        }
    }
}
