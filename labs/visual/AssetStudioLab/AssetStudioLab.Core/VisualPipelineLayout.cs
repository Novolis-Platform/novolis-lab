using Novolis.CodeGen.Pipeline;

namespace AssetStudioLab;

/// <summary>CodeGen.Pipeline layout for an Asset Studio bake.</summary>
public sealed class VisualPipelineLayout : IPipelineLayout
{
    public VisualPipelineLayout(string repoRoot)
    {
        RepoRoot = repoRoot;
    }

    public string RepoRoot { get; }

    public string StepsRoot => Path.Combine(RepoRoot, "steps");

    public string ManifestDir => Path.Combine(RepoRoot, "source");

    public string StepDir(string stepId) => Path.Combine(StepsRoot, stepId);

    public string StepArtifactsDir(string stepId) => Path.Combine(StepDir(stepId), "artifacts");
}
