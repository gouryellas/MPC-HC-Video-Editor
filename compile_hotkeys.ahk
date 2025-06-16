; Read hotkeys from settings file. Provide defaults if not found.
IniRead, mainHotkey,  %A_ScriptDir%\data\settings.ini, Hotkeys, main,  MButton
IniRead, eraseHotkey, %A_ScriptDir%\data\settings.ini, Hotkeys, erase, +space

; Ensure the hotkeys are active only when MPC-BE is focused.
Hotkey, IfWinActive, ahk_exe mpc-be64.exe
Hotkey, %mainHotkey%,  HotkeyCreateBookmark
Hotkey, %eraseHotkey%, HotkeyEraseBookmark
Hotkey, IfWinActive

return

HotkeyCreateBookmark:
        edit_bookmarks("create")
return

HotkeyEraseBookmark:
        edit_bookmarks("erase")
return
