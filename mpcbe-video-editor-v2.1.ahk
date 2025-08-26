

Process, Priority, , High
#SingleInstance, force
SetWorkingDir, %a_temp%
Menu, Tray, NoStandard
Menu, Tray, Add, Reload, reload_script
Menu, Tray, Add, Edit, edit_script
Menu, Tray, Add, Variables, view_variables
Menu, Tray, Add
Menu, Tray, Add, Exit, Exit_script

FileInstall, ffmpeg.exe, %a_temp%\ffmpeg.exe, 1
FileInstall, compile_merge.bat, %a_temp%\compile_merge.bat, 1
FileInstall, compile_data.ini, %a_temp%\compile_data.ini, 1
#Include compile_execute.ahk
#Include compile_globals.ahk
#Include compile_timers.ahk
#Include compile_library.ahk
#Include compile_main.ahk

;#Include compile_processor.ahk