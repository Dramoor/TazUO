using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ClassicUO.Configuration;
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
    { }

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

            return;
        }

        RefreshChannelList(manager);

        Vector2 available = ImGui.GetContentRegionAvail();
        float childHeight = Math.Max(available.Y - INPUT_ROW_HEIGHT, 300);

        // Left: channel list
        ImGui.BeginChild("##chat_channels", new Vector2(CHANNEL_PANEL_WIDTH, childHeight), ImGuiChildFlags.Borders);
        {
            ImGui.TextDisabled("Channels");
            ImGui.Separator();

            for (int i = 0; i < _channelSnapshot.Count; i++)
            {
                string channel = _channelSnapshot[i];
                bool selected = channel == _selectedChannel;

                string label = selected ? $"[{channel}]" : $"{channel}";
                if (ImGui.Selectable(label, selected, ImGuiSelectableFlags.None, new Vector2(CHANNEL_PANEL_WIDTH - 42f, 0)))
                {
                    _selectedChannel = channel;
                    _lastKnownMessageCount = -1; // force scroll on channel switch
                }

                ImGui.SameLine();
                if (ImGui.Button($"X##{channel}"))
                {
                    TazUOChatManager.Instance.LeaveChannel(channel);
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
            if (!string.IsNullOrEmpty(_selectedChannel))
            {
                string[] messages = manager.GetMessages(_selectedChannel);
                for (int i = 0; i < messages.Length; i++)
                {
                    ImGui.TextWrapped(messages[i]);
                }

                if (_lastKnownMessageCount != messages.Length)
                {
                    ImGui.SetScrollHereY(1.0f);
                    _lastKnownMessageCount = messages.Length;
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
            if (!string.IsNullOrEmpty(_selectedChannel))
            {
                string[] users = manager.GetUsers(_selectedChannel);
                ImGui.TextDisabled($"Users ({users.Length})");
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
        float menuButtonWidth = 28f;
        ImGui.SetNextItemWidth(available.X - sendButtonWidth - menuButtonWidth - 24f);

        bool enterPressed = ImGui.InputText("##chat_input", _inputBuffer, INPUT_BUF_SIZE, ImGuiInputTextFlags.EnterReturnsTrue);

        ImGui.SameLine();

        if ((enterPressed || ImGui.Button("Send", new Vector2(sendButtonWidth, 0))) && !string.IsNullOrEmpty(_selectedChannel))
        {
            TrySend(manager);
        }

        ImGui.SameLine();

        if (ImGui.Button("...", new Vector2(menuButtonWidth, 0)))
        {
            ImGui.OpenPopup("##chat_menu");
        }

        if (ImGui.BeginPopup("##chat_menu"))
        {
            Profile profile = ProfileManager.CurrentProfile;
            bool connectOnLogin = profile != null && profile.ConnectToIrcOnLogin;
            if (ImGui.MenuItem("Connect to chat on login", null, connectOnLogin) && profile != null)
            {
                profile.ConnectToIrcOnLogin = !connectOnLogin;
            }

            ImGui.EndPopup();
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
        if (manager.TotalChannelCount == _lastKnownChannelCount) return;

        _channelSnapshot.Clear();
        _channelSnapshot.AddRange(manager.GetChannels());

        _lastKnownChannelCount = manager.TotalChannelCount;

        // Auto-select first channel if none selected
        if (string.IsNullOrEmpty(_selectedChannel) && _channelSnapshot.Count > 0)
            _selectedChannel = _channelSnapshot[0];
    }
}
