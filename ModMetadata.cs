using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace HideoutBackport;

public record HideoutBackportMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.zezinho.hideoutbackport";
    public string Name { get; init; } = "HideoutBackport";
    public string Author { get; init; } = "zezinho";
    public List<string>? Contributors { get; init; } = [];
    public Version Version { get; init; } = new(typeof(HideoutBackportMetadata).Assembly.GetName().Version!.ToString(3));
    public Range SptVersion { get; init; } = new("~4.1.6");
    public List<string>? Incompatibilities { get; init; } = [];
    public Dictionary<string, Range>? ModDependencies { get; init; } = [];
    public string? Url { get; init; } = "https://github.com/fernandolirola/HideoutBackport";
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}
