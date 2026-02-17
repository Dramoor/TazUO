using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Async.IRC;
using ClassicUO.Configuration;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers;

public class TazUOChatManager
{
    public static TazUOChatManager Instance { get; private set; } = new();

    private IrcClient _client;

    private readonly Lock _messagesLock = new();
    private readonly Lock _usersLock = new();

    /// <summary>Messages received, keyed by source (nick/channel).</summary>
    private Dictionary<string, List<string>> ReceivedMessages { get; } = [];

    /// <summary>Users present in each channel, keyed by channel name.</summary>
    private Dictionary<string, HashSet<string>> ChannelUsers { get; } = [];

    /// <summary>Incremented each time a message is stored. Used by the UI to detect new messages.</summary>
    public volatile int TotalMessageCount = 0;

    /// <summary>Incremented each time a channel is joined/left. Used by the UI to detect new channels.</summary>
    public volatile int TotalChannelCount = 0;

    public bool IsConnected => _client != null && _client.IsConnected;

    private TazUOChatManager(){}

    public void Init()
    {
        if (IsConnected) return;

        if (_client != null)
            Dispose();

        _client = new();
        _client.Connected += OnConnected;
        _client.ChannelJoined += ChannelJoined;
        _client.ChannelParted += ChannelParted;
        _client.UserQuit += UserQuit;
        _client.NamesReceived += NamesReceived;
        _client.ChannelMessageReceived += ChannelMessageReceived;
        _client.PrivateMessageReceived += PrivateMessageReceived;
        _client.Disconnected += OnDisconnected;
        _client.ConnectionFailed += OnConnectionFailed;

        string nick = new(ProfileManager.CurrentProfile.CharacterName.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
        if (string.IsNullOrEmpty(nick)) nick = "User";

        _ = _client.ConnectAsync("irc.tazuo.org", 6697, nick, useSsl: true);
        Log.TraceDebug($"Connecting to TazUO chat...");
    }

    public string[] GetMessages(string channel)
    {
        lock (_messagesLock)
        {
            if (ReceivedMessages.TryGetValue(channel, out List<string> msgs))
                return msgs.ToArray();
            return Array.Empty<string>();
        }
    }

    public string[] GetChannels()
    {
        lock (_messagesLock)
            return ReceivedMessages.Keys.ToArray();
    }

    public string[] GetUsers(string channel)
    {
        lock (_usersLock)
        {
            if (ChannelUsers.TryGetValue(channel, out HashSet<string> users))
                return users.ToArray();
            return Array.Empty<string>();
        }
    }

    private void OnConnectionFailed(object sender, IrcConnectionFailedEventArgs e) => Log.TraceDebug($"TazUO chat connection failed: {e.Exception.Message}");

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
        lock (_messagesLock)
        {
            if (!ReceivedMessages.TryGetValue(source, out List<string> list))
            {
                list = [];
                ReceivedMessages[source] = list;
            }
            list.Add(message);
            TotalMessageCount++;

            while (list.Count > 200)
                list.RemoveAt(0);
        }
    }

    public void JoinChannel(string channel)
    {
        if (_client == null || string.IsNullOrEmpty(channel)) return;
        _ = _client.JoinChannelAsync(channel);
    }

    public void LeaveChannel(string channel)
    {
        if (_client == null || string.IsNullOrEmpty(channel)) return;
        _ = _client.LeaveChannelAsync(channel);
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
        lock (_messagesLock)
        {
            if (!ReceivedMessages.ContainsKey(e.Channel))
                ReceivedMessages[e.Channel] = [];
        }
        lock (_usersLock)
            GetOrCreateUsers(e.Channel).Add(e.Nick);
        StoreMessage(e.Channel, $"*** {e.Nick} has joined {e.Channel}");
        TotalChannelCount++;
    }

    private void ChannelParted(object sender, IrcChannelPartedEventArgs e)
    {
        lock (_usersLock)
            GetOrCreateUsers(e.Channel).Remove(e.Nick);

        if (string.Equals(e.Nick, _client?.Nickname, StringComparison.OrdinalIgnoreCase))
        {
            lock (_messagesLock)
                ReceivedMessages.Remove(e.Channel);
        }
        else
            StoreMessage(e.Channel, $"*** {e.Nick} has left {e.Channel}");

        TotalChannelCount--;
    }

    private void UserQuit(object sender, IrcUserQuitEventArgs e)
    {
        // Collect affected channels under the users lock, then store messages outside it
        // to avoid nesting _usersLock → _messagesLock.
        List<string> affectedChannels = null;
        lock (_usersLock)
        {
            foreach (KeyValuePair<string, HashSet<string>> kvp in ChannelUsers)
            {
                if (kvp.Value.Remove(e.Nick))
                    (affectedChannels ??= []).Add(kvp.Key);
            }
        }

        if (affectedChannels != null)
        {
            string msg = string.IsNullOrEmpty(e.Reason)
                ? $"*** {e.Nick} has quit"
                : $"*** {e.Nick} has quit ({e.Reason})";
            foreach (string channel in affectedChannels)
                StoreMessage(channel, msg);
        }
    }

    private void NamesReceived(object sender, IrcNamesEventArgs e)
    {
        lock (_usersLock)
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
        _ = _client.JoinChannelAsync("#tazuo");
    }

    private void OnDisconnected(object sender, EventArgs e)
    {
        Log.TraceDebug("Disconnected");
        UnSubEvents();
        ClearMessages();
        _client = null;
    }

    private void UnSubEvents()
    {
        _client.Connected -= OnConnected;
        _client.ChannelJoined -= ChannelJoined;
        _client.ChannelParted -= ChannelParted;
        _client.UserQuit -= UserQuit;
        _client.NamesReceived -= NamesReceived;
        _client.ChannelMessageReceived -= ChannelMessageReceived;
        _client.PrivateMessageReceived -= PrivateMessageReceived;
        _client.Disconnected -= OnDisconnected;
        _client.ConnectionFailed -= OnConnectionFailed;
    }

    private void ClearMessages()
    {
        lock (_messagesLock)
        {
            ReceivedMessages.Clear();
            TotalMessageCount = 0;
        }
        lock (_usersLock)
            ChannelUsers.Clear();
    }

    public void Dispose()
    {
        if (_client == null) return;

        UnSubEvents();
        _ = _client.DisposeAsync();
        ClearMessages();
        _client = null;
    }
}
