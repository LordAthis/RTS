public class ServiceTask {
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ScriptPath { get; set; } = "";
    
    // Feltételek
    public int MinWinVersion { get; set; } = 0; // pl. 10
    public string RequiredComponent { get; set; } = ""; // pl. ".NET 4.8"
    
    public bool ShouldShow(int currentWin, bool componentInstalled) {
        if (currentWin < MinWinVersion) return false;
        if (!string.IsNullOrEmpty(RequiredComponent) && componentInstalled) return false;
        return true;
    }
}
