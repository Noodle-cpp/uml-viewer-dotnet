# UML Viewer (.NET 8)

Interactive UML companion **without Java**. Reads the same `.metrics/` and IR **`.edn`** as Clojure `uml-viewer`.

## Projects

| Project | Role |
|---------|------|
| `UmlViewer.Core` | IR (JSON/EDN), metrics overlay (`owners-by-ns`), grades |
| `UmlViewer.Engine` | Namespace `view-at`, layout, edge routing |
| `UmlViewer.Scan` | Roslyn C# scan → JSON IR |
| `UmlViewer.Cli` | `scan`, `overlay-check`, `view` |
| `UmlViewer.App` | Avalonia canvas + sidebar (drill-down, CRAP/M dots) |

## Quick start (GLPI)

Use the same IR as Clojure (`clj -M:ir …`) or JSON from `scan`:

```powershell
cd uml-viewer-dotnet
dotnet build

# EDN from Clojure uml-viewer (recommended parity)
dotnet run --project src/UmlViewer.App -- `
  --root "D:\Users\Noodle\Desktop\work_tp\TP.Cmdb.Glpi.Sync.Service" `
  --ir "D:\Users\Noodle\Desktop\ub\uml-viewer\target\glpi-scan.edn"
```

Or JSON:

```powershell
dotnet run --project src/UmlViewer.Cli -- scan examples/glpi.policy.json out/glpi.json
dotnet run --project src/UmlViewer.Cli -- view `
  --root "D:\...\TP.Cmdb.Glpi.Sync.Service" `
  --ir out/glpi.json
```

## UI (aligned with Clojure viewer)

- **Namespace tree** at root; double-click a package box to drill (`Application` → `Application.Abstractions` …).
- **Edges** between visible boxes (implements / dependency), foreign targets as ovals when present.
- **CRAP / mutation** overlay with C/M grade dots and heat colors (same thresholds as `domain/config.clj`).
- **Pan** Ctrl+drag or middle mouse; **zoom** wheel. Sidebar inspector + open source file.
- **Declutter** toolbar button: same modes as Clojure (`none → arrows → triangles → elements → classes`), recompiles the diagram (`DeclutterEngine` before layout).
- **Sidebar diagrams**: real IR title, named proposals from `:proposals` in EDN (with `:layers` / nested `:nses` groups), **+ New proposal**, double-click `proposal.*` package or nested group for **layer-view**.

## Agent / companion (C# viewer)

Uses the same `.uml-viewer/` mailbox as Clojure (`to-agent.edn`, `to-viewer.edn`, `session.edn`):

| UI | Agent `:op` |
|----|-------------|
| **Regen** | `:regen` |
| Real diagram / proposal click | `:context` (`:real` or `:proposal`) |
| Right-click on diagram | `:refresh-crap`, `:refresh-mutate`, `:refresh-mutate-all`, `:omit` |
| Close window | writes `session.edn` (restore with `--restart`) |

Wake-up uses **tmux** when `.uml-viewer/companion.edn` lists a session (same as Clojure); otherwise commands are still queued.

**Proposals** (+ New / rename / delete) persist to IR `:proposals` and sibling `.policy.edn` when present.

IR reload: file mtime + `.metrics` stamp (overlay refresh).

## Parity with Clojure `uml-viewer`

| Area | Status |
|------|--------|
| Hierarchical IR + proposals + declutter | Done |
| Stacked `:diagrams` (`compile-document`) | Done |
| Agent mailbox + Regen + context + element ops | Done |
| Proposal persist / rename / delete | Done |
| Session `--restart` | Done |
| Detail card (CRAP / CC / Cov / mutation columns) | Done |
| Edge / dep hover popup | Done |
| Open source (+ member line in detail) | Done |
| Companion auto-launch (tmux / Terminal) | Clojure scripts only |
| Pixel golden tests | Basic engine tests only |
| Multi-language `clj -M:ir` | Use Clojure IR or Roslyn `scan` |

## Engine parity (C#)

| Module | Status |
|--------|--------|
| `geom`, `curve`, `compose`, `hit` | Ported |
| `layout` | Ports, BFS, barycenter, packages, foreign ovals |
| `route` | Full candidate routing + `separate-ports` |
| `hierarchy` | `view-at`, `apply-declutter`, `proposal-view`, `layer-view`, remapped edges |
| `detail` | Summary window (ops + rels) |

## Notes

- Overlay: crapper `:namespace`, mutator `defn/` / `defn-/`, multi-namespace **claim-rank** ownership.
- Policy: JSON or EDN for `scan` only; viewer IR should match Clojure scan shape (`:classes`, `:edges`, hierarchical ids).
