namespace OctopusDeployVariableViewer;

public sealed class DisplaySettings
{
    /// <summary>Values longer than this are cut off with an ellipsis. 0 disables truncation.</summary>
    public int MaxValueWidth { get; set; } = 40;
}
