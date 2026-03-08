using System.Text.Json;
using System.Text.Json.Serialization;
using TokenSqueeze.Models;

namespace TokenSqueeze.Storage;

internal sealed class IndexStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public IndexStore()
    {
        StoragePaths.EnsureRootExists();
    }

    public void Save(CodeIndex index)
    {
        var projectDir = StoragePaths.GetProjectDir(index.ProjectName);
        Directory.CreateDirectory(projectDir);

        var targetPath = StoragePaths.GetIndexPath(index.ProjectName);
        var tempPath = targetPath + ".tmp";

        var json = JsonSerializer.Serialize(index, SerializerOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, targetPath, overwrite: true);
    }

    public CodeIndex? Load(string projectName)
    {
        var indexPath = StoragePaths.GetIndexPath(projectName);
        if (!File.Exists(indexPath))
            return null;

        var json = File.ReadAllText(indexPath);
        return JsonSerializer.Deserialize<CodeIndex>(json, SerializerOptions);
    }

    public List<string> ListProjects()
    {
        if (!Directory.Exists(StoragePaths.RootDir))
            return [];

        return Directory.GetDirectories(StoragePaths.RootDir)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToList();
    }

    public void Delete(string projectName)
    {
        var projectDir = StoragePaths.GetProjectDir(projectName);
        if (Directory.Exists(projectDir))
            Directory.Delete(projectDir, recursive: true);
    }
}
