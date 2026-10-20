using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace OctopusDeployVariableViewer;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var (settings, display) = LoadSettings();
            if (args.Contains("--full", StringComparer.OrdinalIgnoreCase))
                display.MaxValueWidth = 0;

            using var client = new OctopusClient(settings);

            var (projects, environments) = await AnsiConsole.Status().StartAsync("Loading projects...", async _ =>
            {
                var projectsTask = client.GetProjectsAsync();
                var environmentsTask = client.GetEnvironmentsAsync();
                await Task.WhenAll(projectsTask, environmentsTask);
                return (projectsTask.Result, environmentsTask.Result);
            });

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select a [green]project[/]:")
                    .PageSize(30)
                    .EnableSearch()
                    .AddChoices(projects.Select(p => p.Name)));

            await ShowProjectAsync(client, settings, display, projects.First(p => p.Name == choice), environments);
            return 0;
        }
        catch (Exception ex) when (IsKnownError(ex))
        {
            WriteError(ex);
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Unexpected error:[/]");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            return 1;
        }
    }

    private static (OctopusSettings Octopus, DisplaySettings Display) LoadSettings()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var settings = configuration.GetSection("Octopus").Get<OctopusSettings>() ?? new OctopusSettings();
        var display = configuration.GetSection("Display").Get<DisplaySettings>() ?? new DisplaySettings();

        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new ConfigurationException("Octopus:Host and Octopus:ApiKey must be set in appsettings.json.");

        if (!Uri.TryCreate(settings.Host, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ConfigurationException($"Octopus:Host '{settings.Host}' is not a valid http(s) URL.");

        return (settings, display);
    }

    private static bool IsKnownError(Exception ex) =>
        ex is ConfigurationException or HttpRequestException or TaskCanceledException or JsonException or InvalidDataException;

    private static void WriteError(Exception ex)
    {
        var message = ex switch
        {
            HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } =>
                "Octopus rejected the API key. Check Octopus:ApiKey in appsettings.json.",
            HttpRequestException { StatusCode: HttpStatusCode.NotFound } h =>
                $"Not found: {h.Message}. Check Octopus:Host and Octopus:Space in appsettings.json.",
            HttpRequestException { StatusCode: null } h =>
                $"Could not reach Octopus: {h.Message}",
            TaskCanceledException =>
                "The request to Octopus timed out.",
            JsonException or InvalidDataException =>
                $"Unexpected response from Octopus: {ex.Message}",
            _ => ex.Message
        };

        AnsiConsole.MarkupLineInterpolated($"[red]{message}[/]");
    }

    private static async Task ShowProjectAsync(OctopusClient client, OctopusSettings settings, DisplaySettings display, Project project, List<Environment> allEnvironments)
    {
        var (variables, machines, targetRoles) = await AnsiConsole.Status().StartAsync($"Loading {project.Name.EscapeMarkup()}...", async _ =>
        {
            // The roles don't depend on the variables, so fetch them in parallel; machines need the environment ids.
            var variablesTask = client.GetProjectVariablesAsync(project);
            var rolesTask = client.GetTargetRolesAsync(project);

            var variables = await variablesTask;
            return (variables, await client.GetMachinesAsync(variables.EnvironmentIds), await rolesTask);
        });

        var projectEnvIds = variables.EnvironmentIds.ToHashSet();
        var environments = allEnvironments
            .Where(e => projectEnvIds.Contains(e.Id))
            .OrderBy(e => e.SortOrder)
            .ToList();

        var rows = BuildRows(environments, variables.Variables, display.MaxValueWidth);
        var differing = rows.Where(r => !r.IsSameEverywhere).ToList();
        var same = rows.Where(r => r.IsSameEverywhere).ToList();

        var summary = new List<string> { settings.Space, $"{rows.Count} variables", $"{environments.Count} environments" };
        if (variables.GitBranch is not null)
            summary.Add($"Git: {variables.GitBranch}");

        AnsiConsole.Write(new Panel(new Markup($"[grey]{string.Join(" · ", summary).EscapeMarkup()}[/]"))
            .Header($"[bold yellow] {project.Name.EscapeMarkup()} [/]")
            .Border(BoxBorder.Rounded)
            .Expand());

        if (differing.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]Differs per environment[/] [grey]({differing.Count})[/]");
            AnsiConsole.Write(BuildPerEnvironmentTable(environments, machines, targetRoles, differing));
        }

        if (same.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]Same in every environment[/] [grey]({same.Count})[/]");
            AnsiConsole.Write(BuildSameEverywhereTable(same));
        }
    }

    private sealed record VariableRow(string Name, List<string> Cells, bool IsSameEverywhere);

    private static List<VariableRow> BuildRows(List<Environment> environments, List<Variable> variables, int maxWidth)
    {
        var rows = new List<VariableRow>();

        foreach (var group in variables.GroupBy(v => v.Name).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var name = group.Key.EscapeMarkup();

            if (environments.Count == 0)
            {
                rows.Add(new VariableRow(name, [FormatValues(group.ToList(), maxWidth)], true));
                continue;
            }

            var unscoped = group.Where(v => EnvironmentScope(v).Count == 0).ToList();
            var values = environments
                .Select(env => group.Where(v => EnvironmentScope(v).Contains(env.Id)).ToList())
                .Select(scoped => scoped.Count > 0
                    ? (Value: FormatValues(scoped, maxWidth), IsFallback: false)
                    : (Value: FormatValues(unscoped, maxWidth), IsFallback: true))
                .ToList();

            if (values.All(v => v.Value == values[0].Value))
            {
                rows.Add(new VariableRow(name, [values[0].Value], true));
                continue;
            }

            // Values that fall back to the unscoped variable are greyed out to set them apart from environment-specific ones.
            var cells = values.Select(v => v.IsFallback && v.Value.Length > 0 ? $"[grey]{v.Value}[/]" : v.Value).ToList();
            rows.Add(new VariableRow(name, cells, false));
        }

        return rows;
    }

    private static Table BuildPerEnvironmentTable(List<Environment> environments, List<Machine> machines, HashSet<string>? targetRoles, List<VariableRow> rows)
    {
        var table = new Table().Border(TableBorder.Rounded).ShowRowSeparators().AddColumn("[bold]Variable[/]");

        foreach (var env in environments)
        {
            // Only show targets the project deploys to, when the deployment process tells us which roles it uses.
            var hosts = machines
                .Where(m => m.EnvironmentIds.Contains(env.Id))
                .Where(m => targetRoles is null || m.Roles.Any(targetRoles.Contains))
                .Select(m => m.Name.EscapeMarkup())
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var hostLines = hosts.Count > 0 ? string.Join("\n", hosts) : "-";
            table.AddColumn(new TableColumn(new Markup($"[bold]{env.Name.EscapeMarkup()}[/]\n[grey]{hostLines}[/]")));
        }

        foreach (var row in rows)
            table.AddRow([row.Name, .. row.Cells]);

        return table;
    }

    private static Table BuildSameEverywhereTable(List<VariableRow> rows)
    {
        var table = new Table().Border(TableBorder.Rounded).AddColumn("[bold]Variable[/]").AddColumn("[bold]Value[/]");

        foreach (var row in rows)
            table.AddRow(row.Name, row.Cells[0]);

        return table;
    }

    private static List<string> EnvironmentScope(Variable v) =>
        v.Scope.GetValueOrDefault("Environment") ?? [];

    private static string FormatValues(List<Variable> values, int maxWidth) =>
        string.Join("\n", values.Select(v =>
        {
            var value = v.IsSensitive ? "*****" : Truncate(v.Value ?? "", maxWidth).EscapeMarkup();
            var otherScopes = v.Scope.Where(s => s.Key != "Environment").Select(s => s.Key).ToList();
            return otherScopes.Count > 0 ? $"{value} [dim]({string.Join(", ", otherScopes)})[/]" : value;
        }));

    private static string Truncate(string value, int maxWidth) =>
        maxWidth > 0 && value.Length > maxWidth ? value[..(maxWidth - 1)] + "…" : value;
}

public sealed class ConfigurationException(string message) : Exception(message);
