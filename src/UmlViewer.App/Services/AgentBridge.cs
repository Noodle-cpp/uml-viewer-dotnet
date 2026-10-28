using System.Diagnostics;
using UmlViewer.Core.Mailbox;
using UmlViewer.Core.Models;

namespace UmlViewer.App.Services;

public static class AgentBridge
{
    public const string WakeMessage =
        "You have mail from the viewer. If idle, read .uml-viewer/to-agent.edn.";

    public static (MailboxCommand Cmd, bool Woke) RequestAgent(
        string metricsRoot,
        string op,
        IReadOnlyDictionary<string, object?>? extra = null)
    {
        var cmd = MailboxService.WriteCommand(MailboxPaths.ToAgent(metricsRoot), op, extra);
        return (cmd, NotifyAgent(metricsRoot));
    }

    public static (MailboxCommand Cmd, bool Woke) RequestRegen(string metricsRoot) =>
        RequestAgent(metricsRoot, "regen");

    public static void MailContext(string metricsRoot, IrDocument doc, string? proposalId)
    {
        if (!string.IsNullOrEmpty(proposalId))
        {
            var name = PolicyProposals.ProposalById(doc, proposalId)?.Name ?? proposalId;
            RequestAgent(metricsRoot, "context", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["context"] = "proposal",
                ["proposal-id"] = proposalId.TrimStart(':'),
                ["name"] = name,
            });
            return;
        }

        RequestAgent(metricsRoot, "context", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["context"] = "real",
        });
    }

    public static void RequestElementOp(
        string metricsRoot,
        string op,
        AgentTarget target)
    {
        RequestAgent(metricsRoot, op, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["target"] = target.ToEdnMap(),
        });
    }

    public static bool NotifyAgent(string metricsRoot)
    {
        var session = ReadCompanionSession(metricsRoot);
        if (string.IsNullOrEmpty(session))
            return false;
        try
        {
            RunTmux("send-keys", "-t", session, "-l", WakeMessage);
            Thread.Sleep(150);
            RunTmux("send-keys", "-t", session, "C-m");
            Thread.Sleep(50);
            RunTmux("send-keys", "-t", session, "C-j");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? ReadCompanionSession(string metricsRoot)
    {
        var path = MailboxPaths.CompanionFile(metricsRoot);
        if (!File.Exists(path))
            return null;
        try
        {
            var text = File.ReadAllText(path);
            var m = System.Text.RegularExpressions.Regex.Match(text, @":session\s+""([^""]+)""");
            if (m.Success)
                return m.Groups[1].Value;
            m = System.Text.RegularExpressions.Regex.Match(text, @":session\s+:(\S+)");
            return m.Success ? m.Groups[1].Value : null;
        }
        catch
        {
            return null;
        }
    }

    private static void RunTmux(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "tmux",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("tmux not found.");
        p.WaitForExit(5000);
    }
}

public sealed class AgentTarget
{
    public string? Id { get; init; }
    public string Kind { get; init; } = "class";
    public string? Ns { get; init; }
    public string? ProposalId { get; init; }

    public Dictionary<string, object?> ToEdnMap()
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["kind"] = Kind,
        };
        if (!string.IsNullOrEmpty(Id))
            map["id"] = Id.TrimStart(':');
        if (!string.IsNullOrEmpty(Ns))
            map["ns"] = Ns;
        if (!string.IsNullOrEmpty(ProposalId))
            map["proposal-id"] = ProposalId.TrimStart(':');
        return map;
    }
}
