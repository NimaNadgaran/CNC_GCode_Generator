using System.Text;

namespace CNCGCodeGenerator.Core;

public sealed class PreviewSession
{
    public ValidatedProgram? Current { get; private set; }
    public ExportAudit? LastExport { get; private set; }
    public void Invalidate() => Current = null;
    public ValidatedProgram Generate(GrindingRequest request, MachineProfile profile)
    {
        Invalidate();
        Current = GenerationService.Generate(request, profile);
        return Current;
    }
    public void Export(string path, bool overwriteConfirmed)
    {
        var snapshot = Current ?? throw new ValidationException("Export", "Preview", "Generate and review the current inputs first.");
        if (!string.Equals(Path.GetFileName(path), snapshot.FileName, StringComparison.Ordinal))
            throw new ValidationException("Export", "File name", $"The validated filename is {snapshot.FileName}; change the program name and revalidate to rename it.");
        try
        {
            AtomicFile.Write(path, Encoding.ASCII.GetBytes(snapshot.GCode), overwriteConfirmed);
            LastExport = new(GenerationService.Version, snapshot.ProfileJson, snapshot.Request,
                snapshot.Report, snapshot.Sha256, Path.GetFullPath(path), true, null);
        }
        catch (Exception ex)
        {
            LastExport = new(GenerationService.Version, snapshot.ProfileJson, snapshot.Request,
                snapshot.Report, snapshot.Sha256, Path.GetFullPath(path), false, ex.Message);
            throw;
        }
    }
}

public sealed record ExportAudit(string ApplicationVersion, string ProfileJson, GrindingRequest InputSnapshot,
    string ValidationReport, string ProgramSha256, string ExportPath, bool Success, string? Failure);

public static class AtomicFile
{
    public static void Write(string path, byte[] bytes, bool overwriteConfirmed)
    {
        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath) && !overwriteConfirmed)
            throw new ValidationException("Export", "Overwrite", $"Explicit confirmation required for {fullPath}.");
        var directory = Path.GetDirectoryName(fullPath)!;
        var temporary = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(fullPath))
            {
                if (!overwriteConfirmed) throw new ValidationException("Export", "Overwrite", $"Destination appeared while exporting: {fullPath}.");
                File.Replace(temporary, fullPath, fullPath + $".{Guid.NewGuid():N}.bak");
            }
            else File.Move(temporary, fullPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
