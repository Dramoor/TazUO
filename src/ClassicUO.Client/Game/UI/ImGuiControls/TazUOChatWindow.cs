using System.Collections.Generic;
using System.Numerics;
using ClassicUO.Game.Managers;
using ImGuiNET;

namespace ClassicUO.Game.UI.ImGuiControls;

public class TazUOChatWindow : SingletonImGuiWindow<TazUOChatWindow>
{
    private const int INPUT_BUF_SIZE = 512;
    private const float CHANNEL_PANEL_WIDTH = 130f;
    private const float USER_PANEL_WIDTH = 120f;
    private const float INPUT_ROW_HEIGHT = 32f;

    private readonly byte[] _inputBuffer = new byte[INPUT_BUF_SIZE];
    private readonly byte[] _joinBuffer = new byte[64];
    private readonly List<string> _channelSnapshot = [];

    private string _selectedChannel = string.Empty;
    private int _lastKnownChannelCount = -1;
    private int _lastKnownMessageCount = -1;
    private TazUOChatManager manager = TazUOChatManager.Instance;

    private TazUOChatWindow() : base("TazUO Chat")
    {
        WindowFlags = ImGuiWindowFlags.NoCollapse;
    }

    public override void DrawContent()
    {
        if(!manager.IsConnected)
        {
            ImGui.Text("Not connected..");
            if (ImGui.Button("Try to connect"))
            {
                manager.Dispose();
                manager.Init();
            }
        }

        RefreshChannelList(manager);

        Vector2 available = ImGui.GetContentRegionAvail();
        float childHeight = available.Y - INPUT_ROW_HEIGHT;

        // Left: channel list
        ImGui.BeginChild("##chat_channels", new Vector2(CHANNEL_PANEL_WIDTH, childHeight), ImGuiChildFlags.Borders);
        {
            ImGui.TextDisabled("Channels");
            ImGui.Separator();

            for (int i = 0; i < _channelSnapshot.Count; i++)
            {
                string channel = _channelSnapshot[i];
                bool selected = channel == _selectedChannel;

                string label = selected ? $"> {channel}" : $"  {channel}";
                if (ImGui.Selectable(label, selected, ImGuiSelectableFlags.None, new Vector2(CHANNEL_PANEL_WIDTH - 8f, 0)))
                {
                    _selectedChannel = channel;
                    _lastKnownMessageCount = -1; // force scroll on channel switch
                }
            }

            if (ImGui.InputText("##join_input", _joinBuffer, (uint)_joinBuffer.Length, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                TryJoinChannel(manager);
            }
            SetTooltip("Join/Create a channel. Type it in and press enter.");
        }
        ImGui.EndChild();

        ImGui.SameLine();

        // Middle: messages
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float msgWidth = available.X - CHANNEL_PANEL_WIDTH - USER_PANEL_WIDTH - spacing * 2;
        ImGui.BeginChild("##chat_messages", new Vector2(msgWidth, childHeight), ImGuiChildFlags.Borders);
        {
            if (!string.IsNullOrEmpty(_selectedChannel) &&
                manager.ReceivedMessages.TryGetValue(_selectedChannel, out List<string> messages))
            {
                for (int i = 0; i < messages.Count; i++)
                {
                    ImGui.TextWrapped(messages[i]);
                }

                if (_lastKnownMessageCount != manager.TotalMessageCount)
                {
                    ImGui.SetScrollHereY(1.0f);
                    _lastKnownMessageCount = manager.TotalMessageCount;
                }
            }
            else if (!string.IsNullOrEmpty(_selectedChannel))
            {
                ImGui.TextDisabled("No messages yet.");
            }
            else
            {
                ImGui.TextDisabled("Select a channel.");
            }
        }
        ImGui.EndChild();

        ImGui.SameLine();

        // Right: user list
        ImGui.BeginChild("##chat_users", new Vector2(USER_PANEL_WIDTH, childHeight), ImGuiChildFlags.Borders);
        {
            if (!string.IsNullOrEmpty(_selectedChannel) &&
                manager.ChannelUsers.TryGetValue(_selectedChannel, out HashSet<string> users))
            {
                ImGui.TextDisabled($"Users ({users.Count})");
                ImGui.Separator();
                foreach (string user in users)
                    ImGui.TextUnformatted(user);
            }
            else
            {
                ImGui.TextDisabled("Users");
            }
        }
        ImGui.EndChild();

        // Input row
        float sendButtonWidth = 60f;
        ImGui.SetNextItemWidth(available.X - sendButtonWidth - 16f);

        bool enterPressed = ImGui.InputText("##chat_input", _inputBuffer, INPUT_BUF_SIZE, ImGuiInputTextFlags.EnterReturnsTrue);

        ImGui.SameLine();

        if ((enterPressed || ImGui.Button("Send", new Vector2(sendButtonWidth, 0))) && !string.IsNullOrEmpty(_selectedChannel))
        {
            TrySend(manager);
        }
    }

    private void TrySend(TazUOChatManager manager)
    {
        // Find the null terminator to get the actual string length
        int len = 0;
        while (len < _inputBuffer.Length && _inputBuffer[len] != 0)
            len++;

        if (len == 0) return;

        string text = System.Text.Encoding.UTF8.GetString(_inputBuffer, 0, len);
        manager.SendMessage(_selectedChannel, text);

        // Clear buffer
        System.Array.Clear(_inputBuffer, 0, _inputBuffer.Length);

        ImGui.SetKeyboardFocusHere(-1);
    }

    private void TryJoinChannel(TazUOChatManager manager)
    {
        int len = 0;
        while (len < _joinBuffer.Length && _joinBuffer[len] != 0)
            len++;

        if (len == 0) return;

        string channel = System.Text.Encoding.UTF8.GetString(_joinBuffer, 0, len).Trim();
        if (!string.IsNullOrEmpty(channel))
            manager.JoinChannel(channel);

        System.Array.Clear(_joinBuffer, 0, _joinBuffer.Length);
    }

    private void RefreshChannelList(TazUOChatManager manager)
    {
        if (manager.ReceivedMessages.Count == _lastKnownChannelCount) return;

        _channelSnapshot.Clear();
        foreach (string key in manager.ReceivedMessages.Keys)
            _channelSnapshot.Add(key);

        _lastKnownChannelCount = manager.ReceivedMessages.Count;

        // Auto-select first channel if none selected
        if (string.IsNullOrEmpty(_selectedChannel) && _channelSnapshot.Count > 0)
            _selectedChannel = _channelSnapshot[0];
    }
}
