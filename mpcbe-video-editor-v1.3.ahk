SetWorkingDir, %a_temp%

FileInstall, ffmpeg.exe, %a_temp%\ffmpeg.exe, 1
FileInstall, compile_merge.bat, %a_temp%\compile_merge.bat, 1
FileInstall, compile_data.ini, %a_temp%\compile_data.ini, 1

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

