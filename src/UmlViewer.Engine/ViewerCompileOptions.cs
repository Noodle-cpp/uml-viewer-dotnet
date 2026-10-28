namespace UmlViewer.Engine;

public sealed class ViewerCompileOptions
{
    public string DeclutterMode { get; set; } = "full";
    public string? ProposalId { get; set; }
    public bool OpenLayer { get; set; }
    public string? OpenLayerId { get; set; }
}
