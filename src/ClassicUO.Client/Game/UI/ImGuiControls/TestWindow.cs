using ClassicUO.Game.Managers;
using ClassicUO.Utility;
using ImGuiNET;

namespace ClassicUO.Game.UI.ImGuiControls;

public class TestWindow : SingletonImGuiWindow<TestWindow>
{
    public static string TestMessage = string.Empty;

    private int _currency = 0;
    private string _formattedCurrency = string.Empty;
    private string _formattedAsInt = string.Empty;

    //This is intended for testing purposes, if you need a quick ui to debug something feel free to edit this as needed.
    public TestWindow() : base("Test Window")
    {
        WindowFlags = ImGuiWindowFlags.AlwaysAutoResize;
    }

    public override void DrawContent()
    {
        ImGui.Text(TestMessage);

        ImGui.Text("This window is not meant to be optimized, it *may* run very poorly.");

        ImGui.Text("Pending heals: " + BandageManager.Instance.PendingHealCount);
        ImGui.Text("Pending heals in global queue: " + BandageManager.Instance.PendingInGlobalQueueCount);

        ImGui.Text("Pending queue items: " + ObjectActionQueue.Instance.GetCurrentQueuedCount);
        if (ImGui.Button("Try mount"))
            GameActions.Mount();

    }
}
