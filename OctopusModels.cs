using System.Text.Json;

namespace OctopusDeployVariableViewer;

public sealed record Project(
    string Id,
    string Name,
    string VariableSetId,
    string? DeploymentProcessId,
    bool IsVersionControlled,
    PersistenceSettings? PersistenceSettings);

public sealed record PersistenceSettings(string? DefaultBranch);

public sealed record Environment(string Id, string Name, int SortOrder);

public sealed record Machine(string Id, string Name, List<string> EnvironmentIds, List<string> Roles);

public sealed record ResourceCollection<T>(List<T> Items);

public sealed record ReferenceDataItem(string Id, string Name);

public sealed record VariableScopeValues(List<ReferenceDataItem> Environments);

public sealed record Variable(string Name, string? Value, bool IsSensitive, Dictionary<string, List<string>> Scope);

public sealed record VariableSet(List<Variable> Variables, VariableScopeValues ScopeValues);

/// <summary>A project's variables; GitBranch is set when they were read from a version-controlled project's branch.</summary>
public sealed record ProjectVariables(List<Variable> Variables, List<string> EnvironmentIds, string? GitBranch);

public sealed record DeploymentStep(Dictionary<string, JsonElement> Properties);

public sealed record DeploymentProcess(List<DeploymentStep> Steps);
