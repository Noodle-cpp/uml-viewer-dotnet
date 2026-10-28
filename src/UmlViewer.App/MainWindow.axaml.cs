using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using UmlViewer.App.Rendering;
using UmlViewer.App.Services;
using UmlViewer.Core.Mailbox;
using UmlViewer.Core.Models;
using UmlViewer.Engine;

namespace UmlViewer.App;

public partial class MainWindow : Window
{
    private readonly List<string> _focusPath = [];
    private string _declutterMode = "full";
    private string? _proposalId;
    private string? _openLayerId;
    private IrDocument _doc = new();
    private CompiledScene? _scene;
    private string? _selectedId;
    private readonly ViewerDocumentService _docs;
    private long _knownIrMtime;
    private string _knownMetricsStamp = "";
    private readonly DispatcherTimer _pollTimer;

    public MainWindow()
    {
        InitializeComponent();
        _docs = StartupOptions.DocumentService
                ?? throw new InvalidOperationException("DocumentService not initialized.");
        _doc = _docs.Document;
        _knownIrMtime = _docs.IrMtime;
        _knownMetricsStamp = _docs.MetricsStamp;

        RealDiagramButton.Click += (_, _) => ShowRealDiagram();
        NewProposalButton.Click += (_, _) => AddProposal();
        DeclutterButton.Click += (_, _) => CycleDeclutter();
        RegenButton.Click += (_, _) => OnRegenClick();

        Canvas.SelectionChanged += (_, id) =>
        {
            _selectedId = id;
            UpdateInspector();
        };
        Canvas.DrillRequested += (_, id) => Drill(id);
        Canvas.DetailRequested += (_, id) => OpenDetail(id);
        Canvas.BackRequested += (_, _) => NavigateUp();
        Canvas.ContextMenuRequested += OnCanvasContextMenu;

        RefreshProposalList();
        ApplySession(StartupOptions.RestartSession ? ViewerSessionStore.Read(_docs.MetricsRoot) : null);
        RebuildScene();

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _pollTimer.Tick += (_, _) => OnPollTick();
        _pollTimer.Start();

        Closing += (_, _) => SaveSession();
    }

    private ViewerCompileOptions CompileOptions() => new()
    {
        DeclutterMode = _declutterMode,
        ProposalId = _proposalId,
        OpenLayerId = _openLayerId,
        OpenLayer = !string.IsNullOrEmpty(_openLayerId),
    };

    private void OnPollTick()
    {
        PollViewerMail();
        MaybeReloadIr();
    }

    private void PollViewerMail()
    {
        _docs.PollViewerMail(cmd =>
        {
            switch (cmd.Op)
            {
                case "display":
                    if (cmd.Extra.TryGetValue("path", out var pathObj) && pathObj is string rel)
                    {
                        var full = Path.IsPathRooted(rel)
                            ? rel
                            : Path.Combine(_docs.MetricsRoot, rel);
                        if (File.Exists(full))
                        {
                            _doc = _docs.ReloadFrom(full);
                            StartupOptions.IrPath = _docs.IrPath;
                            _knownIrMtime = _docs.IrMtime;
                            _knownMetricsStamp = _docs.MetricsStamp;
                            RefreshProposalList();
                            RebuildScene();
                        }
                    }

                    return true;
                case "quit-for-restart":
                    SaveSession();
                    Close();
                    return true;
                default:
                    return false;
            }
        });
    }

    private void MaybeReloadIr()
    {
        _docs.RefreshStamps();
        if (!_docs.ShouldReload(_knownIrMtime, _knownMetricsStamp))
            return;
        _doc = _docs.ReloadDocument();
        _knownIrMtime = _docs.IrMtime;
        _knownMetricsStamp = _docs.MetricsStamp;
        RefreshProposalList();
        RebuildScene();
    }

    private void RebuildScene()
    {
        _scene = DiagramCompiler.Compile(_doc, _focusPath, CompileOptions());
        Canvas.Scene = _scene;
        Canvas.NavLabel = BuildNavLabel();
        TitleText.Text = _scene.Title;
        HeaderText.Text =
            $"{_doc.Title}  ·  {_doc.Classes.Count(c => !c.Foreign)} classes  ·  {_docs.MetricsRoot}";
        UpdateProposalSelection();
        UpdateInspector();
    }

    private string? BuildNavLabel()
    {
        if (!string.IsNullOrEmpty(_openLayerId))
            return "← " + (_scene?.Title ?? _openLayerId);
        if (_focusPath.Count > 0)
            return "← " + string.Join('.', _focusPath);
        return null;
    }

    private void RefreshProposalList()
    {
        ProposalPanel.Children.Clear();
        foreach (var p in PolicyProposals.NamedProposals(_doc))
        {
            var btn = new Button
            {
                Content = p.Name,
                Tag = p.Id,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 6),
                Background = Brushes.Transparent,
            };
            btn.Click += (_, _) => ShowProposal(p.Id);
            btn.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(btn).Properties.IsRightButtonPressed)
                {
                    ShowProposalContextMenu(btn, p.Id, p.Name);
                    e.Handled = true;
                }
            };
            ProposalPanel.Children.Add(btn);
        }

        RealDiagramButton.Content = string.IsNullOrEmpty(_doc.Title) ? "Real diagram" : _doc.Title;
    }

    private void UpdateProposalSelection()
    {
        var realSelected = _proposalId == null;
        RealDiagramButton.Background = realSelected
            ? new SolidColorBrush(Color.Parse("#40E8C448"))
            : Brushes.Transparent;
        RealDiagramButton.Foreground = realSelected
            ? new SolidColorBrush(Color.Parse("#E8C448"))
            : new SolidColorBrush(Color.Parse("#EDECE4"));

        foreach (var child in ProposalPanel.Children)
        {
            if (child is not Button btn)
                continue;
            var on = btn.Tag as string == _proposalId;
            btn.Background = on
                ? new SolidColorBrush(Color.Parse("#40E8C448"))
                : Brushes.Transparent;
            btn.Foreground = on
                ? new SolidColorBrush(Color.Parse("#E8C448"))
                : new SolidColorBrush(Color.Parse("#EDECE4"));
        }
    }

    private void ShowRealDiagram()
    {
        _proposalId = null;
        _openLayerId = null;
        _focusPath.Clear();
        AgentBridge.MailContext(_docs.MetricsRoot, _doc, null);
        RebuildScene();
    }

    private void ShowProposal(string id)
    {
        _proposalId = id;
        _openLayerId = null;
        _focusPath.Clear();
        AgentBridge.MailContext(_docs.MetricsRoot, _doc, id);
        RebuildScene();
    }

    private void AddProposal()
    {
        var id = "p-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var proposal = new IrProposal
        {
            Id = id,
            Name = PolicyProposals.TimestampName(),
            Notice = PolicyProposals.ProposalNotice,
        };
        _doc.Proposals.Add(proposal);
        _docs.ReplaceDocument(_doc);
        _docs.PersistProposals();
        _knownIrMtime = _docs.IrMtime;
        RefreshProposalList();
        ShowProposal(id);
        AgentBridge.MailContext(_docs.MetricsRoot, _doc, id);
    }

    private void DeleteProposal(string id)
    {
        _doc.Proposals.RemoveAll(p => p.Id == id);
        if (_proposalId == id)
        {
            _proposalId = null;
            _openLayerId = null;
            _focusPath.Clear();
        }

        _docs.ReplaceDocument(_doc);
        _docs.PersistProposals();
        _knownIrMtime = _docs.IrMtime;
        RefreshProposalList();
        RebuildScene();
    }

    private void RenameProposal(string id, string newName)
    {
        var p = _doc.Proposals.FirstOrDefault(x => x.Id == id);
        if (p == null || string.IsNullOrWhiteSpace(newName))
            return;
        p.Name = newName.Trim();
        _docs.ReplaceDocument(_doc);
        _docs.PersistProposals();
        _knownIrMtime = _docs.IrMtime;
        RefreshProposalList();
        UpdateProposalSelection();
    }

    private void NavigateUp()
    {
        if (!string.IsNullOrEmpty(_openLayerId))
        {
            _openLayerId = null;
            RebuildScene();
            return;
        }

        if (_focusPath.Count == 0)
            return;
        _focusPath.RemoveAt(_focusPath.Count - 1);
        RebuildScene();
    }

    private void Drill(string id)
    {
        if (!string.IsNullOrEmpty(_proposalId) && _focusPath.Count == 0 && string.IsNullOrEmpty(_openLayerId))
        {
            if (ProposalHierarchy.ProposalPackageId(id))
            {
                OpenProposalLayer(id);
                return;
            }

            if (ProposalHierarchy.FindNestedGroup(_doc, _proposalId, id) is { Id: not null } group)
            {
                OpenProposalLayer(group.Id);
                return;
            }

            if (_scene?.ClassById.TryGetValue(id, out var inProposal) == true &&
                !string.IsNullOrEmpty(inProposal.Package))
            {
                OpenProposalLayer(inProposal.Package);
                return;
            }
        }

        var segs = id.Split('.');
        if (_focusPath.Count >= segs.Length)
            return;
        _focusPath.Add(segs[_focusPath.Count]);
        RebuildScene();
    }

    private void OpenProposalLayer(string layerId)
    {
        _openLayerId = layerId;
        _focusPath.Clear();
        RebuildScene();
    }

    private void UpdateInspector()
    {
        if (_selectedId == null || _scene == null ||
            !_scene.ClassById.TryGetValue(_selectedId, out var sc))
        {
            InspectorTitle.Text = "Inspector";
            InspectorBody.Text =
                "Select a box on the diagram.\nDouble-click a folder to drill down.\n" +
                "Right-click: CRAP / Mutation / Omit → agent.\n" +
                "Pan: arrow keys / Ctrl+drag · Back: ← on diagram or Esc.\n" +
                "Regen queues :regen for the companion Grok.";
            return;
        }

        var c = sc.Source;
        InspectorTitle.Text = c.Name;
        var crap = c.CrapSummary;
        var lines = new List<string> { c.Id };
        if (!string.IsNullOrEmpty(_proposalId) && string.IsNullOrEmpty(_openLayerId))
            lines.Add("(proposal: double-click opens that layer, not namespace tree)");
        if (!string.IsNullOrEmpty(c.Ns))
            lines.Add(c.Ns);
        if (crap != null)
            lines.Add($"CRAP μ {crap.Mu:F1}  max {crap.Max:F1}  σ {crap.Sigma:F1}");
        if (c.Killed is > 0 || c.Survived is > 0)
            lines.Add($"Mutants {c.Killed ?? 0} killed / {c.Survived ?? 0} survived");
        if (sc.Source.Drill && string.IsNullOrEmpty(_proposalId))
            lines.Add("(double-click to open namespace)");
        else if (sc.Source.Drill && !string.IsNullOrEmpty(_proposalId))
            lines.Add("(double-click to open this proposal layer)");
        InspectorBody.Text = string.Join('\n', lines);
    }

    private void CycleDeclutter()
    {
        _declutterMode = DeclutterEngine.NextMode(_declutterMode);
        DeclutterButton.Content = DeclutterEngine.Labels.GetValueOrDefault(_declutterMode, "Declutter: " + _declutterMode);
        RebuildScene();
    }

    private void OnRegenClick()
    {
        var (_, woke) = AgentBridge.RequestRegen(_docs.MetricsRoot);
        MailStatusText.Text = woke
            ? "Regen requested."
            : "Regen queued; companion session not attached (tmux).";
    }

    private void OpenDetail(string id)
    {
        if (_scene == null)
            return;
        var model = DetailEngine.Build(_scene, id);
        if (model == null)
            return;
        new DetailWindow(model).Show(this);
    }

    private void OnOpenDetailClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedId != null)
            OpenDetail(_selectedId);
    }

    private void OnOpenSourceClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedId == null || _scene == null ||
            !_scene.ClassById.TryGetValue(_selectedId, out var sc))
            return;
        var file = sc.Source.File;
        if (string.IsNullOrEmpty(file))
            return;
        SourceOpener.OpenFile(_docs.MetricsRoot, file, null);
    }

    private void OnCanvasContextMenu(object? sender, CanvasContextEventArgs e)
    {
        var hit = e.Hit;
        if (hit?.Id == null || _scene == null)
            return;
        var target = BuildAgentTarget(hit);
        var menu = new ContextMenu();
        AddAgentMenuItem(menu, "Refresh CRAP", "refresh-crap", target);
        AddAgentMenuItem(menu, "Refresh Mutation", "refresh-mutate", target);
        AddAgentMenuItem(menu, "Refresh All Mutation", "refresh-mutate-all", target);
        AddAgentMenuItem(menu, "Omit", "omit", target);
        menu.Open(Canvas);
    }

    private void ShowProposalContextMenu(Control anchor, string id, string name)
    {
        var menu = new ContextMenu();
        var rename = new MenuItem { Header = "Rename" };
        rename.Click += (_, _) =>
        {
            var dlg = new Window
            {
                Title = "Rename proposal",
                Width = 360,
                Height = 140,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = BuildRenameDialog(name, newName => RenameProposal(id, newName)),
            };
            dlg.ShowDialog(this);
        };
        var delete = new MenuItem { Header = "Delete" };
        delete.Click += (_, _) => DeleteProposal(id);
        menu.Items.Add(rename);
        menu.Items.Add(delete);
        menu.Open(anchor);
    }

    private static Control BuildRenameDialog(string current, Action<string> onOk)
    {
        var box = new TextBox { Text = current, Margin = new Thickness(12) };
        var ok = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12) };
        ok.Click += (_, _) => onOk(box.Text ?? "");
        return new StackPanel { Children = { box, ok } };
    }

    private AgentTarget BuildAgentTarget(HitResult hit)
    {
        _scene!.ClassById.TryGetValue(hit.Id ?? "", out var cls);
        var component = hit.Kind is "package" or "child" ||
                        hit.Drill ||
                        (cls?.Source.Contents?.Count ?? 0) > 0;
        return new AgentTarget
        {
            Id = hit.Id,
            Kind = component ? "component" : "class",
            Ns = cls?.Source.Ns,
            ProposalId = _proposalId,
        };
    }

    private void AddAgentMenuItem(ContextMenu menu, string label, string op, AgentTarget target)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) =>
        {
            AgentBridge.RequestElementOp(_docs.MetricsRoot, op, target);
            MailStatusText.Text = $"Queued :{op} for agent.";
        };
        menu.Items.Add(item);
    }

    private void ApplySession(ViewerSession? session)
    {
        if (session == null)
            return;
        _focusPath.Clear();
        _focusPath.AddRange(session.Focus);
        _proposalId = session.ProposalId;
        _openLayerId = session.OpenLayer;
        _declutterMode = string.IsNullOrEmpty(session.Declutter) ? "full" : session.Declutter!;
        _selectedId = session.Selected;
        DeclutterButton.Content = DeclutterEngine.Labels.GetValueOrDefault(_declutterMode, _declutterMode);
        Canvas.SetViewPanZoom(session.CamX, session.CamY, session.Zoom);
    }

    private void SaveSession()
    {
        var (px, py, zoom) = Canvas.GetViewPanZoom();
        ViewerSessionStore.Write(_docs.MetricsRoot, new ViewerSession
        {
            Focus = _focusPath.ToList(),
            ProposalId = _proposalId,
            Proposal = !string.IsNullOrEmpty(_proposalId),
            OpenLayer = _openLayerId,
            Declutter = _declutterMode,
            CamX = px,
            CamY = py,
            Zoom = zoom,
            Selected = _selectedId,
        });
    }
}
