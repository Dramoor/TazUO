using System;
using System.Collections.Generic;
using Async.IRC;
using ClassicUO.Configuration;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers;

public class TazUOChatManager
{
    public static TazUOChatManager Instance { get; private set; } = new();

    private IrcClient _client;

    /// <summary>Messages received, keyed by source (nick/channel).</summary>
    public Dictionary<string, List<string>> ReceivedMessages { get; } = [];

    /// <summary>Users present in each channel, keyed by channel name.</summary>
    public Dictionary<string, HashSet<string>> ChannelUsers { get; } = [];

    /// <summary>Incremented each time a message is stored. Used by the UI to detect new messages.</summary>
    public int TotalMessageCount { get; private set; }

    public bool IsConnected => _client != null && _client.IsConnected;

    private TazUOChatManager(){}

    public void Init()
    {
        _client = new();
        //_client.RawLineReceived += OnRawLine;
        _client.Connected += OnConnected;
        _client.ChannelJoined += ChannelJoined;
        _client.ChannelParted += ChannelParted;
        _client.UserQuit += UserQuit;
        _client.NamesReceived += NamesReceived;
        _client.ChannelMessageReceived += ChannelMessageReceived;
        _client.PrivateMessageReceived += PrivateMessageReceived;
        _client.Disconnected += OnDisconnected;
        _client.ConnectionFailed += OnConnectionFailed;

        _ = _client.ConnectAsync("irc.tazuo.org", 6697, ProfileManager.CurrentProfile.CharacterName, useSsl: true);
        Log.TraceDebug($"Connecting to TazUO chat...");
    }

    private void OnConnectionFailed(object sender, IrcConnectionFailedEventArgs e) => Log.TraceDebug($"TazUO chat connection failed: {e.Exception.Message}");

    private void OnRawLine(object sender, IrcRawLineEventArgs e)
    {
        if (!e.IsIncoming) return;
        if (!e.Line.Contains("PRIVMSG")) return;
        // Log raw bytes as hex so we can see exactly what delimiters arrive
        System.Text.StringBuilder hex = new();
        foreach (char c in e.Line)
            hex.Append(c < 32 ? $"[{(int)c:X2}]" : c.ToString());
        Log.TraceDebug($"[RAW] {hex}");
    }

    private void PrivateMessageReceived(object sender, IrcMessageEventArgs e)
    {
        string formatted = FormatMessage(e.Source, e.Message);
        Log.TraceDebug($"{e.Source}: {e.Message}");
        StoreMessage(e.Source, formatted);
    }

    private void ChannelMessageReceived(object sender, IrcMessageEventArgs e)
    {
        string formatted = FormatMessage(e.Source, e.Message);
        Log.TraceDebug($"{e.Target} | {e.Source}: {e.Message} => [{formatted}]");
        StoreMessage(e.Target, formatted);
    }

    private static string FormatMessage(string nick, string message)
    {        
        // CTCP ACTION with delimiters: \x01ACTION text\x01
        if (message.StartsWith("\u0001ACTION", StringComparison.Ordinal))
        {
            int textStart = 8; // past \x01ACTION + space
            int end = message.LastIndexOf('\u0001');
            if (end <= 0) end = message.Length;
            string action = message.Length > textStart ? message[textStart..end] : string.Empty;
            return $"* {nick} {action}";
        }

        return $"{nick}: {message}";
    }

    private void StoreMessage(string source, string message)
    {
        if (!ReceivedMessages.TryGetValue(source, out List<string> list))
        {
            list = [];
            ReceivedMessages[source] = list;
        }
        list.Add(message);
        TotalMessageCount++;

        while (list.Count > 200)
        {
            list.RemoveAt(0);
        }
    }

    public void JoinChannel(string channel)
    {
        if (_client == null || string.IsNullOrEmpty(channel)) return;
        _ = _client.JoinChannelAsync(channel);
    }

    public void SendMessage(string target, string message)
    {
        if (_client == null || string.IsNullOrEmpty(target) || string.IsNullOrEmpty(message)) return;
        _ = _client.SendMessageAsync(target, message);
        StoreMessage(target, $"<You>: {message}");
    }

    private void ChannelJoined(object sender, IrcChannelJoinedEventArgs e)
    {
        Log.TraceDebug($"Joined channel: {e.Channel}");
        if (!ReceivedMessages.ContainsKey(e.Channel))
            ReceivedMessages[e.Channel] = [];
        GetOrCreateUsers(e.Channel).Add(e.Nick);
        StoreMessage(e.Channel, $"*** {e.Nick} has joined {e.Channel}");
    }

    private void ChannelParted(object sender, IrcChannelPartedEventArgs e)
    {
        GetOrCreateUsers(e.Channel).Remove(e.Nick);
        string msg = string.IsNullOrEmpty(e.Reason)
            ? $"*** {e.Nick} has left {e.Channel}"
            : $"*** {e.Nick} has left {e.Channel} ({e.Reason})";
        StoreMessage(e.Channel, msg);
    }

    private void UserQuit(object sender, IrcUserQuitEventArgs e)
    {
        foreach (KeyValuePair<string, HashSet<string>> kvp in ChannelUsers)
        {
            if (kvp.Value.Remove(e.Nick))
            {
                string msg = string.IsNullOrEmpty(e.Reason)
                    ? $"*** {e.Nick} has quit"
                    : $"*** {e.Nick} has quit ({e.Reason})";
                StoreMessage(kvp.Key, msg);
            }
        }
    }

    private void NamesReceived(object sender, IrcNamesEventArgs e)
    {
        HashSet<string> users = GetOrCreateUsers(e.Channel);
        foreach (string nick in e.Nicks)
        {
            // Strip mode prefixes (@, +, %, ~, &)
            string clean = nick.TrimStart('@', '+', '%', '~', '&');
            if (!string.IsNullOrEmpty(clean))
                users.Add(clean);
        }
    }

    private HashSet<string> GetOrCreateUsers(string channel)
    {
        if (!ChannelUsers.TryGetValue(channel, out HashSet<string> users))
        {
            users = [];
            ChannelUsers[channel] = users;
        }
        return users;
    }

    private void OnConnected(object sender, EventArgs e)
    {
        Log.TraceDebug("Connected!");
        _client.JoinChannelAsync("#tazuo");
    }

    private void OnDisconnected(object sender, EventArgs e)
    {
        Log.TraceDebug("Disconnected");
        Dispose();
    }

    public async void Dispose()
    {        
        if (_client == null) return;

        //_client.RawLineReceived -= OnRawLine;
        _client.Connected -= OnConnected;
        _client.ChannelJoined -= ChannelJoined;
        _client.ChannelParted -= ChannelParted;
        _client.UserQuit -= UserQuit;
        _client.NamesReceived -= NamesReceived;
        _client.ChannelMessageReceived -= ChannelMessageReceived;
        _client.PrivateMessageReceived -= PrivateMessageReceived;
        _client.Disconnected -= OnDisconnected;
        _client.ConnectionFailed -= OnConnectionFailed;

        await _client.DisposeAsync();

        _client = null;
    }
}