Process, Priority, , High
#SingleInstance, force
SetWorkingDir, %A_AppData%
Menu, Tray, NoStandard
Menu, Tray, Add, Show, show
Menu, Tray, Default, Show
Menu, Tray, Add, Reload, reload_script
Menu, Tray, Add, Edit, edit_script
Menu, Tray, Add, Variables, view_variables
Menu, Tray, Add
Menu, Tray, Add, Exit, Exit_script
FileCreateDir, %A_AppData%\MPC-HC Video Editor\
FileCreateDir, %A_AppData%\MPC-HC Video Editor\icons\
If A_IsCompiled {
	if !fileexist(A_AppData . "\MPC-HC Video Editor\compile_data.ini")
		FileInstall, compile_data.ini, %A_AppData%\MPC-HC Video Editor\compile_data.ini
}
FileInstall, ffmpeg.exe, %A_AppData%\MPC-HC Video Editor\ffmpeg.exe, 1
FileInstall, ffprobe.exe, %A_AppData%\MPC-HC Video Editor\ffprobe.exe, 1
FileInstall, compile_merge.bat, %A_AppData%\MPC-HC Video Editor\compile_merge.bat, 1
FileInstall, compile_merge_files.bat, %A_AppData%\MPC-HC Video Editor\compile_merge_files.bat, 1
FileInstall, compile_convert.bat, %A_AppData%\MPC-HC Video Editor\compile_convert.bat, 1
FileInstall, icons\test.ico, %A_AppData%\MPC-HC Video Editor\icons\test.ico, 1
FileInstall, icons\test2.ico, %A_AppData%\MPC-HC Video Editor\icons\test2.ico, 1
FileInstall, icons\1.ico, %A_AppData%\MPC-HC Video Editor\icons\1.ico, 1
FileInstall, icons\2.ico, %A_AppData%\MPC-HC Video Editor\icons\2.ico, 1
FileInstall, icons\3.ico, %A_AppData%\MPC-HC Video Editor\icons\3.ico, 1
FileInstall, icons\4.ico, %A_AppData%\MPC-HC Video Editor\icons\4.ico, 1
FileInstall, icons\5.ico, %A_AppData%\MPC-HC Video Editor\icons\5.ico, 1
FileInstall, icons\6.ico, %A_AppData%\MPC-HC Video Editor\icons\6.ico, 1
FileInstall, icons\7.ico, %A_AppData%\MPC-HC Video Editor\icons\7.ico, 1
FileInstall, icons\8.ico, %A_AppData%\MPC-HC Video Editor\icons\8.ico, 1
FileInstall, icons\9.ico, %A_AppData%\MPC-HC Video Editor\icons\9.ico, 1
FileInstall, icons\10.ico, %A_AppData%\MPC-HC Video Editor\icons\10.ico, 1
FileInstall, icons\d.ico, %A_AppData%\MPC-HC Video Editor\icons\d.ico, 1
FileInstall, icons\save.ico, %A_AppData%\MPC-HC Video Editor\icons\save.ico, 1
FileInstall, icons\select.ico, %A_AppData%\MPC-HC Video Editor\icons\select.ico, 1
FileInstall, icons\clock.ico, %A_AppData%\MPC-HC Video Editor\icons\clock.ico, 1
FileInstall, icons\load.ico, %A_AppData%\MPC-HC Video Editor\icons\load.ico, 1
FileInstall, icons\unload.ico, %A_AppData%\MPC-HC Video Editor\icons\unload.ico, 1
FileInstall, icons\delete.ico, %A_AppData%\MPC-HC Video Editor\icons\delete.ico, 1
FileInstall, icons\edit.ico, %A_AppData%\MPC-HC Video Editor\icons\edit.ico, 1
FileInstall, icons\video.ico, %A_AppData%\MPC-HC Video Editor\icons\video.ico, 1
FileInstall, icons\bookmarks.ico, %A_AppData%\MPC-HC Video Editor\icons\bookmarks.ico, 1
FileInstall, icons\check.ico, %A_AppData%\MPC-HC Video Editor\icons\check.ico, 1
FileInstall, icons\key.ico, %A_AppData%\MPC-HC Video Editor\icons\key.ico, 1
FileInstall, icons\dash.ico, %A_AppData%\MPC-HC Video Editor\icons\dash.ico, 1
FileInstall, icons\add.ico, %A_AppData%\MPC-HC Video Editor\icons\add.ico, 1
FileInstall, icons\clear.ico, %A_AppData%\MPC-HC Video Editor\icons\clear.ico, 1
FileInstall, icons\error.ico, %A_AppData%\MPC-HC Video Editor\icons\error.ico, 1
FileInstall, icons\undo.ico, %A_AppData%\MPC-HC Video Editor\icons\undo.ico, 1
FileInstall, icons\merge.ico, %A_AppData%\MPC-HC Video Editor\icons\merge.ico, 1
FileInstall, icons\split.ico, %A_AppData%\MPC-HC Video Editor\icons\split.ico, 1
FileInstall, icons\convert.ico, %A_AppData%\MPC-HC Video Editor\icons\convert.ico, 1
FileInstall, icons\reset.ico, %A_AppData%\MPC-HC Video Editor\icons\reset.ico, 1
FileInstall, icons\question.ico, %A_AppData%\MPC-HC Video Editor\icons\question.ico, 1
#Include compile_execute.ahk
#Include compile_globals.ahk
#Include compile_timers.ahk
#Include compile_library.ahk
#Include compile_main.ahk