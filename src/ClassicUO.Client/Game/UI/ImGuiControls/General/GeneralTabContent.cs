using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Processes;
using ClassicUO.Game.UI.Gumps.SpellBar;
using ClassicUO.Network;

namespace ClassicUO.Game.UI.ImGuiControls
{
    public class GeneralTabContent : TabContent
    {
        private readonly Profile _profile = ProfileManager.CurrentProfile;
        private int _objectMoveDelay;
        private bool _highlightObjects, _petScaling, _disableWeather, _useLongDistancePathing, _showNames, _autoOpenOwnCorpse, _autoUnequipForActions;
        private ushort _turnDelay;
        private float _imguiWindowAlpha, _lastImguiWindowAlpha;
        private float _cameraSmoothingFactor;
        private int _currentThemeIndex, _minGumpMoveDist, _gameScale, _minScale, _maxScale;
        private string[] _themeNames;
        private int _pathfindingGenerationTimeMs;
        private string _quickHealSpell, _quickCureSpell;

        // Child tab contents
        private HudTabContent _hudTab;
        private SpellBarTabContent _spellBarTab;
        private TitleBarTabContent _titleBarTab;
        private SpellIndicatorTabContent _spellIndicatorTab;
        private FriendsListTabContent _friendsListTab;

        // Info tab cached values
        private readonly string _version = "TazUO Version: " + CUOEnviroment.Version;
        private uint _lastObject = 0;
        private string _lastObjectString = "Last Object: 0x00000000";

        public GeneralTabContent()
        {
            _highlightObjects = _profile.HighlightGameObjects;
            _showNames = _profile.NameOverheadToggled;
            _autoOpenOwnCorpse = _profile.AutoOpenOwnCorpse;
            _autoUnequipForActions = _profile.AutoUnequipForActions;
            _disableWeather = _profile.DisableWeather;
            _turnDelay = _profile.TurnDelay;
            _imguiWindowAlpha = _lastImguiWindowAlpha = Client.Settings.Get(SettingsScope.Global, Constants.SqlSettings.IMGUI_ALPHA, 1.0f);
            _cameraSmoothingFactor = _profile.CameraSmoothingFactor;
            _useLongDistancePathing = World.Instance?.Player?.Pathfinder.UseLongDistancePathfinding ?? false;
            _pathfindingGenerationTimeMs = Client.Settings.Get(SettingsScope.Global, Constants.SqlSettings.LONG_DISTANCE_PATHING_SPEED, 2);
            _petScaling = _profile.EnablePetScaling;
            _minGumpMoveDist = _profile.MinGumpMoveDistance;
            _gameScale = (int)(100 * Client.Game.RenderScale);
            _minScale = Math.Abs((int)(100 * Constants.MIN_GAME_SCALE));
            _maxScale = (int)(100 * Constants.MAX_GAME_SCALE);

            var heal = SpellDefinition.FullIndexGetSpell(_profile.QuickHealSpell);
            var cure = SpellDefinition.FullIndexGetSpell(_profile.QuickCureSpell);
            _quickHealSpell = heal != null ? heal.Name : _profile.QuickHealSpell.ToString();
            _quickCureSpell = cure != null ? cure.Name : _profile.QuickCureSpell.ToString();

            // Initialize theme selector
            _themeNames = ImGuiTheme.GetThemes();
            string currentTheme = Client.Settings.Get(SettingsScope.Global, Constants.SqlSettings.IMGUI_THEME, "Default");
            _currentThemeIndex = Array.IndexOf(_themeNames, currentTheme);
            if (_currentThemeIndex < 0) _currentThemeIndex = 0;

            // Initialize child tabs
            _hudTab = new HudTabContent();
            _spellBarTab = new SpellBarTabContent();
            _titleBarTab = new TitleBarTabContent();
            _spellIndicatorTab = new SpellIndicatorTabContent();
            _friendsListTab = new FriendsListTabContent();
        }

        public override void DrawContent()
        {
            if (_profile == null)
            {
                ImGui.Text("Profile not loaded");
                return;
            }

            ImGui.Spacing();

            if (ImGui.BeginTabBar("##GeneralTabs", ImGuiTabBarFlags.None))
            {
                if (ImGui.BeginTabItem("Options"))
                {
                    DrawOptionsTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Info"))
                {
                    DrawInfoTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("HUD"))
                {
                    _hudTab.DrawContent();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Spell Bar"))
                {
                    _spellBarTab.DrawContent();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Title Bar"))
                {
                    _titleBarTab.DrawContent();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Spell Indicators"))
                {
                    _spellIndicatorTab.DrawContent();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Friends List"))
                {
                    _friendsListTab.DrawContent();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Pathfinding"))
                {
                    DrawPathfindingTab();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }


        private void DrawOptionsTab()
        {
            // Group: Visual Config
            ImGui.BeginGroup();
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(ImGuiTheme.Current.BaseContent, "Visual Config");

            ImGui.SetNextItemWidth(125);
            if (ImGui.SliderFloat("Assistant Alpha", ref _imguiWindowAlpha, 0.2f, 1.0f, "%.2f"))
            {
                if(Math.Abs(_imguiWindowAlpha - _lastImguiWindowAlpha) > 0.05)
                {
                    _imguiWindowAlpha = Math.Clamp(_imguiWindowAlpha, 0.2f, 1.0f);
                    _ = Client.Settings.SetAsync(SettingsScope.Global, Constants.SqlSettings.IMGUI_ALPHA, _imguiWindowAlpha);
                    ImGuiManager.UpdateTheme(_imguiWindowAlpha);
                    _lastImguiWindowAlpha = _imguiWindowAlpha;
                }
            }
            ImGuiComponents.Tooltip("Adjust the background transparency of all ImGui windows.");

            ImGui.SetNextItemWidth(125);
            if (ImGui.Combo("Theme", ref _currentThemeIndex, _themeNames, _themeNames.Length))
            {
                string selectedTheme = _themeNames[_currentThemeIndex];
                if (ImGuiTheme.SetTheme(selectedTheme))
                {
                    _ = Client.Settings.SetAsync(SettingsScope.Global, Constants.SqlSettings.IMGUI_THEME, selectedTheme);
                    ImGuiManager.UpdateTheme(_imguiWindowAlpha);
                }
            }
            ImGuiComponents.Tooltip("Select the color theme for ImGui windows.");

            if(ImGui.Button("Open Theme Editor"))
                ImGuiThemeEditorWindow.Show();

            ImGui.SetNextItemWidth(125);
            if (ImGui.SliderFloat("Camera Smoothing", ref _cameraSmoothingFactor, 0f, 1f, "%.1f"))
            {
                _cameraSmoothingFactor = Math.Clamp(_cameraSmoothingFactor, 0f, 1f);
                _profile.CameraSmoothingFactor = _cameraSmoothingFactor;
            }
            ImGuiComponents.Tooltip("Smooth camera following when moving. 0 = instant (classic), 1 = very smooth/floaty.");

            if (ImGui.Checkbox("Highlight game objects", ref _highlightObjects))
            {
                _profile.HighlightGameObjects = _highlightObjects;
            }

            if (ImGui.Checkbox("Show Names", ref _showNames))
            {
                _profile.NameOverheadToggled = _showNames;
            }
            ImGuiComponents.Tooltip("Toggle the display of names above characters and NPCs in the game world.");

            if (ImGui.Checkbox("Enable pet scaling", ref _petScaling))
            {
                _profile.EnablePetScaling = _petScaling;

                Dictionary<uint, Mobile>.ValueCollection mobs = World.Instance.Mobiles.Values;
                foreach (Mobile mob in mobs)
                {
                    if (mob != null && mob.IsRenamable)
                        mob.Scale = _petScaling ? 0.6f : 1f;
                }
            }

            ImGui.SetNextItemWidth(125);
            if (ImGui.SliderInt("Min Gump Drag Distance", ref _minGumpMoveDist, 0, 20))
            {
                _minGumpMoveDist = Math.Clamp(_minGumpMoveDist, 0, 20);
                _profile.MinGumpMoveDistance = _minGumpMoveDist;
            }
            ImGuiComponents.Tooltip("How far you need to drag before a gump will move, this helps prevent accidentally dragging instead of clicking.");

            ImGui.SetNextItemWidth(125);
            if (ImGui.SliderInt("Game Scale", ref _gameScale, _minScale, _maxScale))
            {
                _gameScale = Math.Clamp(_gameScale, _minScale, _maxScale);
            }
            ImGuiComponents.Tooltip("Adjust the scale of the entire game.");
            if (ImGui.Button("Apply scale"))
            {
                float scale = _gameScale / 100f;
                Client.Game.SetScale(scale);
                _ = Client.Settings.SetAsync(SettingsScope.Global, Constants.SqlSettings.GAME_SCALE, scale);
            }

            ImGui.EndGroup();

            ImGui.SameLine();

            // Group: Delay Config
            ImGui.BeginGroup();
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(ImGuiTheme.Current.BaseContent, "Delay Config");

            int tempTurnDelay = _turnDelay;

            ImGui.SetNextItemWidth(150);
            if (ImGui.SliderInt("Turn Delay", ref tempTurnDelay, 0, 150, " %d ms"))
            {
                if (tempTurnDelay < 0) tempTurnDelay = 0;
                if (tempTurnDelay > ushort.MaxValue) tempTurnDelay = 100;

                _turnDelay = (ushort)tempTurnDelay;
                _profile.TurnDelay = _turnDelay;
            }
            ImGui.SetNextItemWidth(150);
            _objectMoveDelay = ProfileManager.CurrentProfile.MoveMultiObjectDelay;
            if (ImGui.InputInt("Object Delay", ref _objectMoveDelay, 50, 100))
            {
                _objectMoveDelay = Math.Clamp(_objectMoveDelay, 0, 3000);

                _profile.MoveMultiObjectDelay = _objectMoveDelay;
            }

            if (ImGui.Button("Automated delay checker"))
            {
                AutomatedObjectDelay.Begin();
            }
            ImGuiComponents.Tooltip("Run a small test to try to determine the best object delay time.\nThis is an experimental feature, if it doesn't work for you just adjust your delay manually.");

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(ImGuiTheme.Current.BaseContent, "Misc");

            bool queueManualMoves = _profile.QueueManualItemMoves;
            if (ImGui.Checkbox("Queue item moves", ref queueManualMoves))
            {
                _profile.QueueManualItemMoves = queueManualMoves;
            }
            ImGuiComponents.Tooltip("Instead of instantly moving an item, put it in a queue to prevent \"You must wait\" messages.");

            bool queueManualUses = _profile.QueueManualItemUses;
            if (ImGui.Checkbox("Queue object uses", ref queueManualUses))
            {
                _profile.QueueManualItemUses = queueManualUses;
            }
            ImGuiComponents.Tooltip("Instead of instantly double clicking an item or mobile, put it in a queue to prevent \"You must wait\" messages.");

            if (ImGui.Checkbox("Auto open own corpse", ref _autoOpenOwnCorpse))
            {
                _profile.AutoOpenOwnCorpse = _autoOpenOwnCorpse;
            }
            ImGuiComponents.Tooltip("Automatically open your own corpse when you die, even if auto open corpses is disabled.");

            if (ImGui.Checkbox("Auto unequip for spells & potions", ref _autoUnequipForActions))
            {
                _profile.AutoUnequipForActions = _autoUnequipForActions;
            }
            ImGuiComponents.Tooltip("Automatically unequip weapons for spells & potions, then reequip them after.");

            if (ImGui.Checkbox("Disable weather", ref _disableWeather))
            {
                _profile.DisableWeather = _disableWeather;
                if (_disableWeather)
                {
                    World.Instance?.Weather.Reset();
                }
            }
            ImGuiComponents.Tooltip("Disable weather effects (rain, snow, storms).");

            const string QUICK_SPELL_TOOLTIP = "These are used on health-bars for party members/pets";
            if (ImGui.Button("Set quick heal spell"))
                UIManager.Add(new SpellQuickSearch(World.Instance, 0, 0, (s) =>
                {
                    if(s != null)
                    {
                        _quickHealSpell = s.Name;
                        _profile.QuickHealSpell = s.ID;
                    }
                }, true).CenterInViewPort());
            ImGui.SameLine();
            ImGui.Text(_quickHealSpell);
            ImGuiComponents.Tooltip(QUICK_SPELL_TOOLTIP);

            if (ImGui.Button("Set quick cure spell"))
                UIManager.Add(new SpellQuickSearch(World.Instance, 0, 0, (s) =>
                {
                    if(s != null)
                    {
                        _quickCureSpell = s.Name;
                        _profile.QuickCureSpell = s.ID;
                    }
                }, true).CenterInViewPort());
            ImGui.SameLine();
            ImGui.Text(_quickCureSpell);
            ImGuiComponents.Tooltip(QUICK_SPELL_TOOLTIP);

            ImGui.EndGroup();
        }

        private void DrawPathfindingTab()
        {
            ImGui.BeginGroup();

            ImGui.SetNextItemWidth(150);
            if (ImGui.Checkbox("Long-Distance Pathfinding", ref _useLongDistancePathing))
            {
                World.Instance.Player.Pathfinder.UseLongDistancePathfinding = _useLongDistancePathing;
                Client.Settings?.SetAsync(SettingsScope.Global, Constants.SqlSettings.USE_LONG_DISTANCE_PATHING,  _useLongDistancePathing);
            }
            ImGuiComponents.Tooltip("This is currently in beta.");


            ImGui.SetNextItemWidth(150);
            if (ImGui.SliderInt("Pathfinding Gen Time", ref _pathfindingGenerationTimeMs, 1, 50, "%d ms"))
            {
                _pathfindingGenerationTimeMs = Math.Clamp(_pathfindingGenerationTimeMs, 1, 50);
                Client.Settings?.SetAsync(SettingsScope.Global, Constants.SqlSettings.LONG_DISTANCE_PATHING_SPEED,  _pathfindingGenerationTimeMs);
                if (Managers.WalkableManager.Instance != null) Managers.WalkableManager.Instance.TargetGenerationTimeMs = _pathfindingGenerationTimeMs;
            }
            ImGuiComponents.Tooltip("Target time in milliseconds for pathfinding cache generation per cycle. Higher values generate cache faster but may cause performance issues.");

            // Display current map generation progress
            if (WalkableManager.Instance != null)
            {
                var (current, total) = WalkableManager.Instance.GetCurrentMapGenerationProgress();
                if (total > 0)
                {
                    float fraction = (float)current / total;
                    float percentage = fraction * 100f;
                    ImGui.SetNextItemWidth(150);
                    ImGui.ProgressBar(fraction, new Vector2(150, 0), $"{percentage:F1}%");
                    ImGuiComponents.Tooltip($"Current map cache generation progress: {current}/{total} chunks");
                }
            }

            ImGui.SetNextItemWidth(150);
            if (ImGui.Button("Reset current map cache"))
            {
                WalkableManager.Instance?.StartFreshGeneration(World.Instance.MapIndex);
            }
            ImGuiComponents.Tooltip("This will start regeneration of the current map cache.");


            ImGui.EndGroup();
        }

        private void DrawInfoTab()
        {
            if (World.Instance != null)
            {
                if (_lastObject != World.Instance.LastObject)
                {
                    _lastObject = World.Instance.LastObject;
                    _lastObjectString = $"Last Object: 0x{_lastObject:X8}";
                }
            }

            ImGui.Text("Ping: " + AsyncNetClient.Socket.Statistics.Ping + "ms");
            ImGui.Spacing();
            ImGui.Text("FPS: " + CUOEnviroment.CurrentRefreshRate);
            ImGui.Spacing();
            ImGui.Text(_lastObjectString);
            ClipboardOnClick(_lastObjectString);
            ImGui.Spacing();
            ImGui.Text(_version);
        }

        public override void Dispose()
        {
            _hudTab?.Dispose();
            _spellBarTab?.Dispose();
            _titleBarTab?.Dispose();
            _spellIndicatorTab?.Dispose();
            _friendsListTab?.Dispose();
            base.Dispose();
        }
    }
}
