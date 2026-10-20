using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OctopusDeployVariableViewer;

public sealed class OctopusClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _space;

    public OctopusClient(OctopusSettings settings)
    {
        _space = settings.Space;
        _http = new HttpClient { BaseAddress = new Uri(settings.Host.TrimEnd('/') + "/") };
        _http.DefaultRequestHeaders.Add("X-Octopus-ApiKey", settings.ApiKey);
    }

    public async Task<List<Project>> GetProjectsAsync() =>
        (await GetAsync<List<Project>>($"api/{_space}/projects/all"))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public Task<List<Environment>> GetEnvironmentsAsync() =>
        GetAsync<List<Environment>>($"api/{_space}/environments/all");

    /// <summary>
    /// Returns the project's variables. Version-controlled projects keep non-sensitive variables in Git and
    /// sensitive ones in the database, so both are read and merged.
    /// </summary>
    public async Task<ProjectVariables> GetProjectVariablesAsync(Project project)
    {
        var gitRef = GetGitRef(project);
        var databaseTask = GetAsync<VariableSet>($"api/{_space}/variables/{project.VariableSetId}");
        var gitTask = gitRef is null
            ? Task.FromResult<VariableSet?>(null)
            : GetOrNullAsync<VariableSet>($"api/{_space}/projects/{project.Id}/{gitRef}/variables");

        var database = await databaseTask;
        if (await gitTask is not { } git)
            return new ProjectVariables(database.Variables, ToIds(database.ScopeValues), null);

        return new ProjectVariables(
            git.Variables.Concat(database.Variables.Where(v => v.IsSensitive)).ToList(),
            ToIds(git.ScopeValues).Union(ToIds(database.ScopeValues)).ToList(),
            project.PersistenceSettings!.DefaultBranch);

        static List<string> ToIds(VariableScopeValues scopeValues) => scopeValues.Environments.Select(e => e.Id).ToList();
    }

    public async Task<List<Machine>> GetMachinesAsync(IEnumerable<string> environmentIds)
    {
        var ids = string.Join(",", environmentIds);
        if (ids.Length == 0)
            return [];

        var result = await GetAsync<ResourceCollection<Machine>>($"api/{_space}/machines?environmentIds={ids}&take={int.MaxValue}");
        return result.Items;
    }

    /// <summary>
    /// Returns the target roles used by the project's deployment process, or null when the process can't be read.
    /// </summary>
    public async Task<HashSet<string>?> GetTargetRolesAsync(Project project)
    {
        var gitRef = GetGitRef(project);
        var url = gitRef is not null
            ? $"api/{_space}/projects/{project.Id}/{gitRef}/deploymentprocesses"
            : !string.IsNullOrEmpty(project.DeploymentProcessId)
                ? $"api/{_space}/deploymentprocesses/{project.DeploymentProcessId}"
                : null;

        if (url is null)
            return null;

        try
        {
            var process = await GetAsync<DeploymentProcess>(url);
            return process.Steps
                .Select(s => s.Properties.GetValueOrDefault("Octopus.Action.TargetRoles"))
                .Where(p => p.ValueKind == JsonValueKind.String)
                .SelectMany(p => p.GetString()!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            // Roles are only used to narrow the host list, so fall back to showing all hosts.
            return null;
        }
    }

    private static string? GetGitRef(Project project)
    {
        var branch = project.PersistenceSettings?.DefaultBranch;
        if (!project.IsVersionControlled || string.IsNullOrEmpty(branch))
            return null;

        return Uri.EscapeDataString(branch.StartsWith("refs/", StringComparison.Ordinal) ? branch : $"refs/heads/{branch}");
    }

    private async Task<T?> GetOrNullAsync<T>(string url) where T : class
    {
        try
        {
            return await GetAsync<T>(url);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            // Older Octopus versions keep variables of version-controlled projects in the database.
            return null;
        }
    }

    private async Task<T> GetAsync<T>(string url)
    {
        using var response = await _http.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"GET {url} failed: {(int)response.StatusCode} {response.ReasonPhrase}", null, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions)
            ?? throw new InvalidDataException($"GET {url} returned an empty response.");
    }

    public void Dispose() => _http.Dispose();
}
