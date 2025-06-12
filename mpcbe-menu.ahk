#NoEnv
#SingleInstance, Ignore

; only launch if the menu isn't already visible
if WinExist("MPC-BE Video Editor")
{
    WinActivate
    ExitApp
}
SetWorkingDir, %A_ScriptDir%

; Include existing modules
#Include compile_execute.ahk
#Include compile_globals.ahk
#Include compile_timers.ahk
#Include compile_hotkeys.ahk
#Include compile_functions.ahk
#Include compile_control.ahk
#Include compile_csv.ahk
#Include compile_error.ahk
#Include compile_file.ahk
#Include compile_mpc.ahk
#Include compile_msg.ahk
#Include compile_number.ahk
#Include compile_path.ahk
#Include compile_run.ahk
#Include compile_send.ahk
#Include compile_string.ahk
#Include compile_time.ahk
#Include compile_wait.ahk
#Include compile_window.ahk

; --- Menu Definitions ---
Menu, FileMenu, Add, Load..., MenuLoad
Menu, FileMenu, Add, Unload, MenuUnload
Menu, FileMenu, Add, Close, MenuClose

Menu, EditMenu, Add, Reload, MenuReload
Menu, EditMenu, Add, Settings, MenuSettings
Menu, EditMenu, Add, Clear, MenuClear

Menu, AboutMenu, Add, GitHub, MenuGitHub
Menu, AboutMenu, Add, Version, MenuVersion

Menu, MyMenuBar, Add, File, :FileMenu
Menu, MyMenuBar, Add, Edit, :EditMenu
Menu, MyMenuBar, Add, About, :AboutMenu

Gui, New, +Resize
Gui, Menu, MyMenuBar
Gui, Show, w600 h400, MPC-BE Video Editor
return

MenuLoad:
    FileSelectFile, selected,,,% "Select Video", "Video Files (*.mp4;*.mkv;*.avi)"
    if (selected != "")
        Run, %selected%
return

MenuUnload:
    window_kill("MPC-BE")
return

MenuClose:
    ExitApp
return

MenuReload:
    Reload
return

MenuSettings:
    Run, notepad.exe %A_ScriptDir%\data\compile_data.ini
return

MenuClear:
    edit_bookmarks("erase")
return

MenuGitHub:
    Run, https://github.com/example/mpcbe-video-editor
return

MenuVersion:
    MsgBox, 64, Version, MPCBE Video Editor v1.0
return

