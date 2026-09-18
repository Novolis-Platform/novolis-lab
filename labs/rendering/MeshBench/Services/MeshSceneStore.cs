using System.IO;
using System.IO.Abstractions;
using System.Numerics;
using System.Text.Json;
using MeshBench.Models;
using Novolis.Rendering.Compile;
using Novolis.Rendering.Runtime;
using Novolis.Rendering.Scene;
using Novolis.Workspaces;
using Novolis.Workspaces.FileSystem;

namespace MeshBench.Services;

internal sealed class MeshSceneStore
{
    private readonly IFileSystem _fileSystem;
    private readonly object _writeLock = new();
    private readonly object _compileLock = new();
    private readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private CompiledScene? _cachedScene;
    private int _cachedRevision = -1;

    public MeshSceneStore(IFileSystem fileSystem) => _fileSystem = fileSystem;

    public string ScenePath(IProject project) =>
        _fileSystem.Path.Combine(project.Root.FullName, WorkspaceLayout.DocumentsFolder, "scene.json");

    public bool HasPersistedScene(IProject project) => _fileSystem.File.Exists(ScenePath(project));

    public MeshSceneDocument Load(IProject project)
    {
        var path = ScenePath(project);
        if (!_fileSystem.File.Exists(path))
            return CreateDefault();

        var json = ReadAllTextShared(path);
        return JsonSerializer.Deserialize<MeshSceneDocument>(json, _json) ?? CreateDefault();
    }

    public void Save(IProject project, MeshSceneDocument document)
    {
        var path = ScenePath(project);
        var json = JsonSerializer.Serialize(document, _json);
        lock (_writeLock)
        {
            WriteAllTextAtomic(path, json);
        }
    }

    public CompiledScene Compile(MeshSceneDocument document, int revision)
    {
        lock (_compileLock)
        {
            if (_cachedScene is not null && _cachedRevision == revision)
                return _cachedScene;

            _cachedScene = CompileCore(document);
            _cachedRevision = revision;
            return _cachedScene;
        }
    }

    public void InvalidateCompileCache()
    {
        lock (_compileLock)
        {
            _cachedRevision = -1;
            _cachedScene = null;
        }
    }

    private static CompiledScene CompileCore(MeshSceneDocument document)
    {
        var builder = new SceneBuilder();
        MeshPrimitives.AddGroundAndLight(builder);
        foreach (var part in document.Parts)
        {
            var center = ToVector3(part.Center);
            var color = ToVector3(part.Color);
            if (part.Kind.Equals("sphere", StringComparison.OrdinalIgnoreCase))
                MeshPrimitives.AddSphere(builder, center, part.Radius, color, segments: 16);
            else
                MeshPrimitives.AddBox(builder, center, ToVector3(part.HalfExtents), color);
        }

        return SceneCompiler.Compile(builder.Build());
    }

    private string ReadAllTextShared(string path)
    {
        using var stream = _fileSystem.FileStream.New(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void WriteAllTextAtomic(string path, string contents)
    {
        var directory = _fileSystem.Path.GetDirectoryName(path)!;
        _fileSystem.Directory.CreateDirectory(directory);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            WriteAllTextWithRetry(temp, contents);
            if (_fileSystem.File.Exists(path))
                _fileSystem.File.Delete(path);
            _fileSystem.File.Move(temp, path);
        }
        finally
        {
            if (_fileSystem.File.Exists(temp))
            {
                try
                {
                    _fileSystem.File.Delete(temp);
                }
                catch (IOException)
                {
                    // Best effort cleanup.
                }
            }
        }
    }

    private void WriteAllTextWithRetry(string path, string contents, int attempts = 6)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using var stream = _fileSystem.FileStream.New(
                    path,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read);
                using var writer = new StreamWriter(stream);
                writer.Write(contents);
                return;
            }
            catch (IOException) when (attempt < attempts - 1)
            {
                Thread.Sleep(20 * (attempt + 1));
            }
        }
    }

    private static MeshSceneDocument CreateDefault()
    {
        var doc = new MeshSceneDocument();
        doc.Parts.Add(new MeshPartRecord
        {
            Name = "Box 1",
            Kind = "box",
            Center = [0f, 0.5f, 0f],
            HalfExtents = [0.5f, 0.5f, 0.5f],
            Color = [0.72f, 0.35f, 0.28f],
        });
        doc.Parts.Add(new MeshPartRecord
        {
            Name = "Sphere 1",
            Kind = "sphere",
            Center = [1.4f, 0.55f, 0.2f],
            Radius = 0.45f,
            Color = [0.28f, 0.55f, 0.82f],
        });
        return doc;
    }

    private static Vector3 ToVector3(float[] values) =>
        values.Length >= 3 ? new Vector3(values[0], values[1], values[2]) : Vector3.Zero;
}
