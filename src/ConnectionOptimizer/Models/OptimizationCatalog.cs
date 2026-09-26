namespace ConnectionOptimizer.Models;

/// <summary>
/// The optimizations the app offers, in the order ACTIVATE ALL runs them.
/// Descriptions and warnings only restate what each script already does.
/// </summary>
public static class OptimizationCatalog
{
    public static IReadOnlyList<OptimizationDefinition> All { get; } =
    [
        new()
        {
            Id = "network-latency",
            Name = "NETWORK LATENCY",
            Description = "System network optimizations",
            Category = "NETWORK",
            Section = OptimizationSection.Network,
            ScriptFile = "NetworkLatencyOptimizer.bat",
            RestartRecommended = true,
            Warnings =
            [
                "Changes network adapter settings: the connection drops briefly.",
            ],
        },
        new()
        {
            Id = "minecraft-pvp",
            Name = "MINECRAFT PVP",
            Description = "PvP network optimizations",
            Category = "PVP",
            Section = OptimizationSection.Network,
            ScriptFile = "MinecraftPvPOptimizer.bat",
            RestartRecommended = true,
        },
        new()
        {
            Id = "registry-tweaks",
            Name = "REGISTRY TWEAKS",
            Description = "TCP/IP values on the active interface",
            Category = "REGISTRY",
            Section = OptimizationSection.Network,
            ScriptFile = "Network_Tweaks.cmd",
            VerifierScriptFile = "Verificar_Network_Tweaks.bat",
            RestartRecommended = true,
        },
        new()
        {
            Id = "windows-cleanup",
            Name = "WINDOWS CLEANUP",
            Description = "Temporary files, DNS and browser caches",
            Category = "CLEANUP",
            Section = OptimizationSection.Cleanup,
            ScriptFile = "AdvancedWindowsCleanup.bat",
            Warnings =
            [
                "Force-closes Chrome, Edge and Firefox.",
                "Releases and renews the IP address: the connection drops briefly.",
                "Empties the Recycle Bin.",
            ],
        },
        new()
        {
            Id = "anti-input-lag",
            Name = "ANTI INPUT LAG",
            Description = "System repair, cleanup and disk optimization",
            Category = "INPUT LAG",
            Section = OptimizationSection.Cleanup,
            ScriptFile = "AntiInputLag.bat",
            RestartRecommended = true,
            Warnings =
            [
                "Runs SFC and DISM: this can take a long time.",
                "Opens the Disk Cleanup settings window, which needs your input.",
                "Opens the Microsoft Store (cache reset).",
                "Deletes pending print jobs.",
                "Empties the Recycle Bin.",
            ],
            RunningHint = "Disk Cleanup may open a window that needs your input.",
        },
    ];
}
