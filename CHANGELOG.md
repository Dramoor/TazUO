# Changelog
All notable changes to TazUO will be recorded here.

## In Development
`Dev channel / branch`

### Misc
- This changelog
- Added auto-loot priority tiers (High/Normal/Low) - Coryigon
- Removed integrated Discord features
- Added ToggleAutoLoot macro to quickly enable/disable autolooting

### Legion
- Added sound API endpoints to LegionScripts - fpw
- Added `API.ScriptName` and `API.ScriptPath`
- Updated PSL browser UI and backend
- Added `.IsHidden` to PyMobile in API
- Added `API.PickUpToCursor`, `API.DropFromCursor` and `API.GetHeldItem`
- Added `IsGargoyle`, `IsMounted`, `IsDrivingBoat`, and `IsRunning` to PyMobile

### Assistant
- Added skills tab to Legion Assistant - Coryigon
- Organizer tab now shows graphic when hovering over the graphic art
- Added Mobile outline option - Highlighting mobiles by notoriety
- Added TazUO chat(Top menu -> More -> TazUO Chat)
- ItemDatabase search now defaults to not only "this character"

### Other
- Move automatic py doc gen to tool usage
- Added ibm-plex font to embedded fonts
- Clean up a bunch of compile-time warnings

### Bugs
- Fixed healthbar collector occasionally becoming unresponsive to targeting/clicks
- Fix rare crash when removing messages from system chat
- Various other bugs fixed