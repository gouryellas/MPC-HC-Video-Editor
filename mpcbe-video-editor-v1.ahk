SetWorkingDir, %a_scriptdir%
FileCreateDir, % A_ScriptDir "\data"
FileCreateDir, % A_ScriptDir "\images"
FileCreateDir, % A_ScriptDir "\ffmpeg"

FileInstall, ffmpeg\ffmpeg.exe, %A_ScriptDir%\ffmpeg\ffmpeg.exe, 1
FileInstall, compile_merge.bat, %A_ScriptDir%\compile_merge.bat, 1
FileInstall, data\compile_data.ini, %A_ScriptDir%\data\compile_data.ini, 1
FileInstall, images\undo.png, %A_ScriptDir%\images\undo.png, 1
FileInstall, images\csv.png, %A_ScriptDir%\images\csv.png,  1
FileInstall, images\close.png, %A_ScriptDir%\images\close.png, 1
FileInstall, images\clear.png, %A_ScriptDir%\images\clear.png, 1
FileInstall, images\bar.png, %A_ScriptDir%\images\bar.png, 1
FileInstall, images\reload.png, %A_ScriptDir%\images\reload.png, 1

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

