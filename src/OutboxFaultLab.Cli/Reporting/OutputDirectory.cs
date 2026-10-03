using System.Text.Json;
namespace OutboxFaultLab;

public sealed class OutputPathException(string message) : Exception(message);
public static class OutputDirectory
{
    public const string Manifest = ".outbox-lab.json";
    public static readonly string[] CaseFiles = ["summary.json", "trace.json", "snapshots.json", "results.csv", "timeline.svg"];
    public static string Prepare(string path, IReadOnlyList<string> files, bool overwrite)
    {
        try
        {
            var full = Path.GetFullPath(path); var manifest = Path.Combine(full, Manifest);
            var owned = new HashSet<string>(StringComparer.Ordinal);
            if (File.Exists(full)) throw new OutputPathException("Output path is a file.");
            if (Directory.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                throw new OutputPathException("Output directory must not be a link.");
            if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any())
            {
                if (!File.Exists(manifest)) throw new OutputPathException("Output must be empty or owned by this lab.");
                using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
                if (doc.RootElement.GetProperty("owner").GetString() != "OutboxFaultLab" || doc.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
                    throw new OutputPathException("Invalid lab ownership manifest.");
                foreach (var registered in doc.RootElement.GetProperty("files").EnumerateArray())
                    owned.Add(registered.GetString() ?? throw new OutputPathException("Invalid registered output name."));
            }
            foreach (var file in files)
            {
                if (Path.IsPathRooted(file) || file.Split('/', '\\').Any(p => p is ".." or "." or "")) throw new OutputPathException("Unsafe report name.");
                var target = Path.Combine(full, file);
                if (Directory.Exists(target) || File.Exists(target) && !overwrite) throw new OutputPathException("Conflicting output: " + file + "; use --overwrite for known reports.");
                if (File.Exists(target) && overwrite && !owned.Contains(file))
                    throw new OutputPathException("Existing file is not registered as lab-owned: " + file);
                if (File.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                    throw new OutputPathException("Report file must not be a link.");
                var parent = Path.GetDirectoryName(target)!;
                if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0) throw new OutputPathException("Output directory must not be a link.");
            }
            Directory.CreateDirectory(full);
            JsonReportWriter.Write(manifest, new { schemaVersion = 1, owner = "OutboxFaultLab", files = owned.Union(files, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() });
            foreach (var file in files) Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(full, file))!);
            return full;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or ArgumentException or InvalidOperationException)
        { throw new OutputPathException("Unusable output path: " + e.Message); }
    }
}
