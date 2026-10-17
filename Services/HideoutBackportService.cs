using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;

namespace HideoutBackport.Services;

public sealed class BackportConfig
{
    public bool Enabled { get; set; } = true;
    public bool UpgradeRequirements { get; set; } = true;
    public bool LiveFoundInRaidRules { get; set; } = true;
    public bool ConstructionTimes { get; set; } = true;
    public bool UpgradeBonuses { get; set; } = true;
    public bool CraftTimes { get; set; } = true;
    public bool CraftRecipes { get; set; } = true;
    public bool NewCrafts { get; set; } = true;
    public bool RemoveRetiredCrafts { get; set; } = true;
    public bool ScavCaseTimes { get; set; } = true;
    public bool Debug { get; set; } = false;
}

/// <summary>
/// Runs right at the start of Preload, before mods that scale or strip hideout values
/// (SVM runs at Preload + 5), so their settings apply on top of the live data
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 1)]
public sealed class EarlyPass(HideoutBackportService service) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken) => service.ApplyAsync();
}

/// <summary>Item mods may add their items later in Preload, so new crafts that were waiting on them get a second chance</summary>
[Injectable(TypePriority = OnLoadOrder.PostLoad - 1)]
public sealed class LatePass(HideoutBackportService service) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken) => service.AddPendingCraftsAsync();
}

[Injectable(InjectionType.Singleton)]
public sealed class HideoutBackportService(
    HideoutTable hideoutTable,
    TemplateTable templateTable,
    JsonUtil jsonUtil,
    ICloner cloner,
    ModHelper modHelper,
    ISptLogger<HideoutBackportService> logger
)
{
    private const string Prefix = "[HideoutBackport]";

    private static readonly JsonSerializerOptions ConfigJson = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private string modPath = string.Empty;
    private BackportConfig config = new();
    private readonly List<HideoutProduction> pendingCrafts = [];
    private Dictionary<string, JsonObject> recipeNodes = new(StringComparer.OrdinalIgnoreCase);

    public Task ApplyAsync()
    {
        modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        config = LoadConfig();
        if (!config.Enabled)
        {
            logger.Info($"{Prefix} Disabled in config.json");
            return Task.CompletedTask;
        }

        try
        {
            ApplyAreas(config);
        }
        catch (Exception ex)
        {
            logger.Error($"{Prefix} Hideout upgrades were not changed: {ex.Message}");
        }

        try
        {
            ApplyCrafts(config);
        }
        catch (Exception ex)
        {
            logger.Error($"{Prefix} Crafts were not changed: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private BackportConfig LoadConfig()
    {
        string path = Path.Combine(modPath, "config.json");
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<BackportConfig>(File.ReadAllText(path), ConfigJson) ?? new BackportConfig()
                : new BackportConfig();
        }
        catch (Exception ex)
        {
            logger.Warning($"{Prefix} config.json could not be read, using defaults: {ex.Message}");
            return new BackportConfig();
        }
    }

    private string ReadData(string name) => File.ReadAllText(Path.Combine(modPath, "data", name));

    private bool ItemExists(string? tpl) => tpl is not null && MongoId.IsValidMongoId(tpl) && templateTable.Items.ContainsKey(new MongoId(tpl));

    /// <summary>Every item template a JSON fragment points at (templateId / endProduct / _tpl)</summary>
    private static IEnumerable<string> TemplatesIn(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (key is "templateId" or "endProduct" or "_tpl" && value is JsonValue v && v.TryGetValue(out string? s) && !string.IsNullOrEmpty(s))
                    {
                        yield return s;
                    }
                    else
                    {
                        foreach (var t in TemplatesIn(value))
                        {
                            yield return t;
                        }
                    }
                }

                break;
            case JsonArray arr:
                foreach (var item in arr)
                {
                    foreach (var t in TemplatesIn(item))
                    {
                        yield return t;
                    }
                }

                break;
        }
    }

    private List<string> MissingTemplates(JsonNode? node) => TemplatesIn(node).Where(t => !ItemExists(t)).Distinct().ToList();

    private void ApplyAreas(BackportConfig config)
    {
        string raw = ReadData("areas.json");
        var liveAreas = jsonUtil.Deserialize<List<HideoutArea>>(raw) ?? [];
        var liveNodes = JsonNode.Parse(raw)!.AsArray()
            .OfType<JsonObject>()
            .ToDictionary(a => a["_id"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase);

        int requirements = 0, firCleared = 0, firAdded = 0, times = 0, bonuses = 0, skipped = 0;

        foreach (var area in hideoutTable.Areas)
        {
            var live = liveAreas.FirstOrDefault(a => a.Id == area.Id);
            if (live?.Stages is null || area.Stages is null || !liveNodes.TryGetValue(area.Id.ToString(), out var liveNode))
            {
                continue;
            }

            foreach (var (stageKey, stage) in area.Stages)
            {
                if (!live.Stages.TryGetValue(stageKey, out var liveStage))
                {
                    continue;
                }

                var stageNode = liveNode["stages"]?[stageKey];

                if (config.UpgradeRequirements && liveStage.Requirements is not null)
                {
                    var missing = MissingTemplates(stageNode?["requirements"]);
                    if (missing.Count > 0)
                    {
                        skipped++;
                        if (config.Debug)
                        {
                            logger.Info($"{Prefix} {area.Type} level {stageKey}: kept old requirements, unknown items {string.Join(", ", missing)}");
                        }
                    }
                    else
                    {
                        var replacement = cloner.Clone(liveStage.Requirements)!;
                        if (!config.LiveFoundInRaidRules)
                        {
                            KeepOldFoundInRaid(stage.Requirements, replacement);
                        }

                        CountFoundInRaid(stage.Requirements, replacement, ref firCleared, ref firAdded);
                        if (!SameJson(stage.Requirements, replacement))
                        {
                            requirements++;
                        }

                        stage.Requirements = replacement;
                    }
                }
                else if (config.LiveFoundInRaidRules && stage.Requirements is not null && liveStage.Requirements is not null)
                {
                    var before = cloner.Clone(stage.Requirements)!;
                    foreach (var req in stage.Requirements.Where(r => r.Type == "Item"))
                    {
                        var match = liveStage.Requirements.FirstOrDefault(l => l.Type == "Item" && l.TemplateId == req.TemplateId);
                        if (match is not null)
                        {
                            req.IsSpawnedInSession = match.IsSpawnedInSession;
                        }
                    }

                    CountFoundInRaid(before, stage.Requirements, ref firCleared, ref firAdded);
                }

                if (config.ConstructionTimes && liveStage.ConstructionTime is not null && stage.ConstructionTime != liveStage.ConstructionTime)
                {
                    stage.ConstructionTime = liveStage.ConstructionTime;
                    times++;
                }

                if (config.UpgradeBonuses && liveStage.Bonuses is not null && !SameJson(stage.Bonuses, liveStage.Bonuses))
                {
                    if (MissingTemplates(stageNode?["bonuses"]).Count == 0)
                    {
                        stage.Bonuses = cloner.Clone(liveStage.Bonuses);
                        bonuses++;
                    }
                }
            }
        }

        logger.Success($"{Prefix} Hideout upgrades: {requirements} levels with live requirements, {firCleared} items no longer need found in raid"
                       + (firAdded > 0 ? $", {firAdded} now need it" : "")
                       + $", {times} construction times, {bonuses} bonus changes"
                       + (skipped > 0 ? $", {skipped} levels kept (items missing on this server)" : ""));
    }

    private static void KeepOldFoundInRaid(List<StageRequirement>? old, List<StageRequirement> replacement)
    {
        foreach (var req in replacement.Where(r => r.Type == "Item"))
        {
            var previous = old?.FirstOrDefault(o => o.Type == "Item" && o.TemplateId == req.TemplateId);
            if (previous is not null)
            {
                req.IsSpawnedInSession = previous.IsSpawnedInSession;
            }
        }
    }

    private static void CountFoundInRaid(List<StageRequirement>? old, List<StageRequirement> current, ref int cleared, ref int added)
    {
        foreach (var req in current.Where(r => r.Type == "Item"))
        {
            var previous = old?.FirstOrDefault(o => o.Type == "Item" && o.TemplateId == req.TemplateId);
            bool was = previous?.IsSpawnedInSession == true, now = req.IsSpawnedInSession == true;
            if (previous is not null && was && !now)
            {
                cleared++;
            }
            else if (now && !was)
            {
                added++;
            }
        }
    }

    private bool SameJson<T>(T? a, T? b) => jsonUtil.Serialize(a) == jsonUtil.Serialize(b);

    private void ApplyCrafts(BackportConfig config)
    {
        string raw = ReadData("production.json");
        var live = jsonUtil.Deserialize<HideoutProductionData>(raw);
        var root = JsonNode.Parse(raw)!;
        recipeNodes = root["recipes"]!.AsArray().OfType<JsonObject>()
            .ToDictionary(r => r["_id"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase);
        var retired = JsonSerializer.Deserialize<List<string>>(ReadData("retired-crafts.json")) ?? [];

        var recipes = hideoutTable.Production.Recipes ??= [];
        var liveRecipes = live?.Recipes ?? [];
        var current = recipes.Select(r => r.Id.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        int changed = 0, timed = 0, added = 0, skippedMissing = 0, skippedLocked = 0, removed = 0;

        for (int i = 0; i < recipes.Count; i++)
        {
            var mine = recipes[i];
            var theirs = liveRecipes.FirstOrDefault(r => r.Id == mine.Id);
            if (theirs is null)
            {
                continue;
            }

            if (config.CraftRecipes && MissingTemplates(recipeNodes.GetValueOrDefault(mine.Id.ToString())).Count == 0)
            {
                var replacement = cloner.Clone(theirs)!;
                if (!config.CraftTimes)
                {
                    replacement.ProductionTime = mine.ProductionTime;
                }
                else if (replacement.ProductionTime != mine.ProductionTime)
                {
                    timed++;
                }

                if (!SameJson(mine with { ProductionTime = replacement.ProductionTime }, replacement))
                {
                    changed++;
                }

                recipes[i] = replacement;
            }
            else if (config.CraftTimes && theirs.ProductionTime is not null && mine.ProductionTime != theirs.ProductionTime)
            {
                mine.ProductionTime = theirs.ProductionTime;
                timed++;
            }
        }

        if (config.NewCrafts)
        {
            foreach (var theirs in liveRecipes.Where(r => !current.Contains(r.Id.ToString())))
            {
                // Locked crafts are unlocked by quest rewards this game version does not have
                if (theirs.Locked == true)
                {
                    skippedLocked++;
                    continue;
                }

                var missing = MissingTemplates(recipeNodes.GetValueOrDefault(theirs.Id.ToString()));
                if (missing.Count > 0)
                {
                    skippedMissing++;
                    pendingCrafts.Add(theirs);
                    if (config.Debug)
                    {
                        logger.Info($"{Prefix} New craft {theirs.Id} skipped, unknown items {string.Join(", ", missing)}");
                    }

                    continue;
                }

                recipes.Add(cloner.Clone(theirs)!);
                added++;
            }
        }

        if (config.RemoveRetiredCrafts)
        {
            removed = recipes.RemoveAll(r => retired.Contains(r.Id.ToString(), StringComparer.OrdinalIgnoreCase));
        }

        int scav = 0;
        if (config.ScavCaseTimes && hideoutTable.Production.ScavRecipes is { } scavRecipes)
        {
            foreach (var mine in scavRecipes)
            {
                var theirs = live?.ScavRecipes?.FirstOrDefault(r => r.Id == mine.Id);
                if (theirs?.ProductionTime is not null && mine.ProductionTime != theirs.ProductionTime)
                {
                    mine.ProductionTime = theirs.ProductionTime;
                    scav++;
                }
            }
        }

        logger.Success($"{Prefix} Crafts: {changed} recipes updated, {timed} new craft times, {added} new crafts, {removed} retired"
                       + $", {scav} scav case times"
                       + (skippedMissing + skippedLocked > 0
                           ? $" ({skippedMissing} new crafts wait for items, {skippedLocked} need quests from newer versions)"
                           : ""));
    }

    public Task AddPendingCraftsAsync()
    {
        if (!config.Enabled || pendingCrafts.Count == 0)
        {
            return Task.CompletedTask;
        }

        var recipes = hideoutTable.Production.Recipes ??= [];
        var ids = recipes.Select(r => r.Id).ToHashSet();
        int added = 0;
        foreach (var theirs in pendingCrafts.Where(r => !ids.Contains(r.Id)))
        {
            if (MissingTemplates(recipeNodes.GetValueOrDefault(theirs.Id.ToString())).Count == 0)
            {
                recipes.Add(cloner.Clone(theirs)!);
                added++;
            }
        }

        int left = pendingCrafts.Count - added;
        logger.Info($"{Prefix} {added} more new crafts added after other mods loaded their items"
                    + (left > 0 ? $", {left} skipped (items not on this server)" : ""));
        return Task.CompletedTask;
    }
}
