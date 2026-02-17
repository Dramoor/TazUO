using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Network;
using ImGuiNET;

namespace ClassicUO.Game.UI.ImGuiControls
{
    public class SkillsTabContent : TabContent
    {
        private int _sortColumnIndex = 1;
        private bool _sortAscending = true;
        private PlayerMobile _player;

        private bool _showGroups;

        private int[] _sortedIndices;

        public SkillsTabContent()
        {
            _player = World.Instance.Player;
            int count = Client.Game.UO.FileManager.Skills.SkillsCount;
            _sortedIndices = new int[count];

            for (int i = 0; i < count; i++) _sortedIndices[i] = i;
        }

        public override void DrawContent()
        {
            Skill[] skills = World.Instance?.Player?.Skills;

            if (skills == null)
            {
                ImGui.Text("Not connected");
                return;
            }

            DrawToolbar(skills);

            ImGuiTableFlags flags = ImGuiTableFlags.Sortable
                | ImGuiTableFlags.Borders
                | ImGuiTableFlags.RowBg
                | ImGuiTableFlags.ScrollY
                | ImGuiTableFlags.Resizable;

            float scrollHeight = ImGui.GetContentRegionAvail().Y;

            if (ImGui.BeginTable("SkillsTable", 7, flags, new Vector2(0, scrollHeight)))
            {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("Use", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, 30);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultSort);
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableSetupColumn("Base", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableSetupColumn("Cap", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableSetupColumn("+/-", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableSetupColumn("Lock", ImGuiTableColumnFlags.WidthFixed, 50);
                ImGui.TableHeadersRow();

                ImGuiTableSortSpecsPtr sortSpecs = ImGui.TableGetSortSpecs();

                if (sortSpecs.SpecsDirty)
                {
                    if (sortSpecs.SpecsCount > 0)
                    {
                        ImGuiTableColumnSortSpecsPtr spec = sortSpecs.Specs;
                        _sortColumnIndex = spec.ColumnIndex;
                        _sortAscending = spec.SortDirection == ImGuiSortDirection.Ascending;
                    }

                    SortSkills(skills);
                    sortSpecs.SpecsDirty = false;
                }

                if (_showGroups)
                    DrawGroupedRows(skills);
                else
                    for (int row = 0; row < _sortedIndices.Length; row++)
                        DrawSkillRow(skills, _sortedIndices[row]);

                ImGui.EndTable();
            }
        }

        private void DrawSkillRow(Skill[] skills, int idx)
        {
            if (idx >= skills.Length)
                return;

            Skill skill = skills[idx];

            if (skill == null)
                return;

            ImGui.TableNextRow();

            // Use
            ImGui.TableNextColumn();
            if (skill.IsClickable)
                if (ImGui.SmallButton("Use##" + idx))
                    GameActions.UseSkill(skill.Index);

            // Name
            ImGui.TableNextColumn();
            ImGui.Text(skill.Name);

            // Value
            ImGui.TableNextColumn();
            ImGui.Text(skill.Value.ToString("F1"));

            // Base
            ImGui.TableNextColumn();
            ImGui.Text(skill.Base.ToString("F1"));

            // Cap
            ImGui.TableNextColumn();
            ImGui.Text(skill.Cap.ToString("F1"));

            // +/-
            ImGui.TableNextColumn();
            float delta = skill.Base - skill.BaseAtLogin;

            if (delta > 0f)
                ImGui.TextColored(ImGuiTheme.Current.Success, $"+{delta:F1}");
            else if (delta < 0f)
                ImGui.TextColored(ImGuiTheme.Current.Error, $"{delta:F1}");
            else
                ImGui.Text("0.0");

            // Lock
            ImGui.TableNextColumn();
            string lockText = skill.Lock switch
            {
                Lock.Up => "Up",
                Lock.Down => "Dn",
                Lock.Locked => "==",
                _ => "?"
            };

            Vector4 lockColor = skill.Lock switch
            {
                Lock.Up => ImGuiTheme.Current.Success,
                Lock.Down => ImGuiTheme.Current.Error,
                _ => ImGuiTheme.Current.Warning
            };

            ImGui.PushStyleColor(ImGuiCol.Button, lockColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, lockColor * new Vector4(1.2f, 1.2f, 1.2f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, lockColor * new Vector4(0.8f, 0.8f, 0.8f, 1.0f));

            if (ImGui.SmallButton(lockText + "##lock" + skill.Index))
            {
                byte nextLock = (byte)(((byte)skill.Lock + 1) % 3);
                GameActions.ChangeSkillLockStatus((ushort)skill.Index, nextLock);
                AsyncNetClient.Socket.Send_SkillsRequest(_player.Serial);
            }

            ImGui.PopStyleColor(3);
        }

        private void DrawGroupedRows(Skill[] skills)
        {
            List<SkillsGroup> groups = World.Instance?.SkillsGroupManager?.Groups;

            if (groups == null)
                return;

            int skillsCount = skills.Length;

            for (int g = 0; g < groups.Count; g++)
            {
                SkillsGroup group = groups[g];

                // Collect valid skill indices for this group
                var groupIndices = new List<int>(group.Count);
                float groupBaseTotal = 0f;

                for (int i = 0; i < group.Count; i++)
                {
                    byte skillIdx = group.GetSkill(i);

                    if (skillIdx == 0xFF || skillIdx >= skillsCount)
                        continue;

                    Skill skill = skills[skillIdx];

                    if (skill == null)
                        continue;

                    groupIndices.Add(skillIdx);
                    groupBaseTotal += skill.Base;
                }

                // Sort indices within the group using the current sort settings
                SortGroupIndices(groupIndices, skills);

                // Render tree node header spanning all columns
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGuiTreeNodeFlags treeFlags = ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAllColumns;
                bool open = ImGui.TreeNodeEx($"{group.Name} ({groupBaseTotal:F1})##group{g}", treeFlags);

                if (open)
                {
                    for (int i = 0; i < groupIndices.Count; i++) DrawSkillRow(skills, groupIndices[i]);

                    ImGui.TreePop();
                }
            }
        }

        private void SortGroupIndices(List<int> indices, Skill[] skills) =>
            indices.Sort((a, b) =>
            {
                Skill sa = skills[a];
                Skill sb = skills[b];

                int cmp = _sortColumnIndex switch
                {
                    1 => string.Compare(sa.Name, sb.Name, StringComparison.OrdinalIgnoreCase),
                    2 => sa.Value.CompareTo(sb.Value),
                    3 => sa.Base.CompareTo(sb.Base),
                    4 => sa.Cap.CompareTo(sb.Cap),
                    5 =>
                        (sa.Base - sa.BaseAtLogin)
                        .CompareTo(sb.Base - sb.BaseAtLogin),
                    6 => ((byte)sa.Lock).CompareTo((byte)sb.Lock),
                    _ => 0
                };

                return _sortAscending ? cmp : -cmp;
            });

        private void DrawToolbar(Skill[] skills)
        {
            // Set All buttons
            if (ImGui.SmallButton("All Up"))
            {
                for (int i = 0; i < skills.Length; i++)
                    GameActions.ChangeSkillLockStatus((ushort)i, (byte)Lock.Up);
                AsyncNetClient.Socket.Send_SkillsRequest(_player.Serial);
            }
            ImGui.SameLine();

            if (ImGui.SmallButton("All Down"))
            {
                for (int i = 0; i < skills.Length; i++)
                    GameActions.ChangeSkillLockStatus((ushort)i, (byte)Lock.Down);
                AsyncNetClient.Socket.Send_SkillsRequest(_player.Serial);
            }
            ImGui.SameLine();

            if (ImGui.SmallButton("All Lock"))
            {
                for (int i = 0; i < skills.Length; i++)
                    GameActions.ChangeSkillLockStatus((ushort)i, (byte)Lock.Locked);
                AsyncNetClient.Socket.Send_SkillsRequest(_player.Serial);
            }
            ImGui.SameLine();
            ImGui.Text("|");
            ImGui.SameLine();

            // Reset +/-
            if (ImGui.SmallButton("Reset +/-"))
                for (int i = 0; i < skills.Length; i++)
                    skills[i].BaseAtLogin = skills[i].Base;

            ImGui.SameLine();

            // Copy All
            if (ImGui.SmallButton("Copy All"))
            {
                var sb = new StringBuilder();
                sb.AppendLine("Name\tValue\tBase\tCap\t+/-\tLock");

                for (int row = 0; row < _sortedIndices.Length; row++)
                {
                    int idx = _sortedIndices[row];

                    if (idx >= skills.Length)
                        continue;

                    Skill skill = skills[idx];

                    if (skill == null)
                        continue;

                    float delta = skill.Base - skill.BaseAtLogin;
                    string lockStr = skill.Lock switch
                    {
                        Lock.Up => "Up",
                        Lock.Down => "Down",
                        Lock.Locked => "Locked",
                        _ => "?"
                    };

                    sb.AppendLine($"{skill.Name}\t{skill.Value:F1}\t{skill.Base:F1}\t{skill.Cap:F1}\t{delta:F1}\t{lockStr}");
                }

                SDL3.SDL.SDL_SetClipboardText(sb.ToString());
                GameActions.Print("Skills copied to clipboard.", Constants.HUE_SUCCESS);
            }

            ImGui.SameLine();
            ImGui.Text("|");
            ImGui.SameLine();

            // Show Groups checkbox
            ImGui.Checkbox("Show Groups", ref _showGroups);

            ImGui.SameLine();
            ImGui.Text("|");
            ImGui.SameLine();

            // Total
            float baseSum = 0f;
            float capSum = 0f;

            for (int i = 0; i < skills.Length; i++)
                if (skills[i] != null)
                {
                    baseSum += skills[i].Base;
                    capSum += skills[i].Cap;
                }

            ImGui.Text($"Total: {baseSum:F1} / {capSum:F1}");
        }

        private void SortSkills(Skill[] skills) =>
            Array.Sort(_sortedIndices, (a, b) =>
            {
                if (a >= skills.Length || b >= skills.Length)
                    return 0;

                Skill sa = skills[a];
                Skill sb = skills[b];

                if (sa == null || sb == null)
                    return 0;

                int cmp = _sortColumnIndex switch
                {
                    1 => string.Compare(sa.Name, sb.Name, StringComparison.OrdinalIgnoreCase), // Name
                    2 => sa.Value.CompareTo(sb.Value),   // Value
                    3 => sa.Base.CompareTo(sb.Base),     // Base
                    4 => sa.Cap.CompareTo(sb.Cap),       // Cap
                    5 => // +/-
                        (sa.Base - sa.BaseAtLogin)
                        .CompareTo(sb.Base - sb.BaseAtLogin),
                    6 => ((byte)sa.Lock).CompareTo((byte)sb.Lock), // Lock
                    _ => 0
                };

                return _sortAscending ? cmp : -cmp;
            });
    }
}
