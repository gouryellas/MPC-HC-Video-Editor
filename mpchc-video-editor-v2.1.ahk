Process, Priority, , High
#SingleInstance, force
SetWorkingDir, %a_temp%
Menu, Tray, NoStandard
Menu, Tray, Add, Show, show
Menu, Tray, Default, Show
Menu, Tray, Add, Reload, reload_script
Menu, Tray, Add, Edit, edit_script
Menu, Tray, Add, Variables, view_variables
Menu, Tray, Add
Menu, Tray, Add, Exit, Exit_script
IniDir := A_AppData "\MPC-HC Video Editor"
FileCreateDir, %IniDir%
IniFile := IniDir "\compile_data.ini"
If A_IsCompiled {
	if !fileexist(A_AppData . "\MPC-HC Video Editor\compile_data.ini")
		FileInstall, compile_data.ini, %A_AppData%\MPC-HC Video Editor\compile_data.ini
}
FileInstall, ffmpeg.exe, %a_temp%\ffmpeg.exe, 1
FileInstall, ffprobe.exe, %a_Temp%\ffprobe.exe, 1
FileInstall, compile_merge.bat, %a_temp%\compile_merge.bat, 1
FileCreateDir, %A_AppData%\MPC-HC Video Editor\icons\
FileInstall, icons\Location and Other Sensors.ico, %A_AppData%\MPC-HC Video Editor\icons\Location and Other Sensors.ico, 1
FileInstall, icons\Bookmarks.ico, %A_AppData%\MPC-HC Video Editor\icons\Bookmarks.ico, 1
FileInstall, icons\Power - Lock.ico, %A_AppData%\MPC-HC Video Editor\icons\Power - Lock.ico, 1
FileInstall, icons\Google Tasks.ico, %A_AppData%\MPC-HC Video Editor\icons\Google Tasks.ico, 1
FileInstall, icons\InfoPath alt 2.ico, %A_AppData%\MPC-HC Video Editor\icons\InfoPath alt 2.ico, 1
FileInstall, icons\Notifications.ico, %A_AppData%\MPC-HC Video Editor\icons\Notifications.ico, 1
FileInstall, icons\Share.ico, %A_AppData%\MPC-HC Video Editor\icons\Share.ico, 1
FileInstall, icons\AutoPlay.ico, %A_AppData%\MPC-HC Video Editor\icons\AutoPlay.ico, 1
FileInstall, icons\Excel alt 1.ico, %A_AppData%\MPC-HC Video Editor\icons\Excel alt 1.ico, 1
FileInstall, icons\Windows Media Player.ico, %A_AppData%\MPC-HC Video Editor\icons\Windows Media Player.ico, 1
FileInstall, icons\MS Office Upload Center.ico, %A_AppData%\MPC-HC Video Editor\icons\MS Office Upload Center.ico, 1
FileInstall, icons\Power - Standby.ico, %A_AppData%\MPC-HC Video Editor\icons\Power - Standby.ico, 1
FileInstall, icons\Aperture.ico, %A_AppData%\MPC-HC Video Editor\icons\Aperture.ico, 1
FileInstall, icons\Screen Resolution.ico, %A_AppData%\MPC-HC Video Editor\icons\Screen Resolution.ico, 1
FileInstall, icons\Network Drive Offline.ico, %A_AppData%\MPC-HC Video Editor\icons\Network Drive Offline.ico, 1
FileInstall, icons\Recycle Bin Full.ico, %A_AppData%\MPC-HC Video Editor\icons\Recycle Bin Full.ico, 1
FileInstall, icons\Power - Shut Down.ico, %A_AppData%\MPC-HC Video Editor\icons\Power - Shut Down.ico, 1
FileInstall, icons\Fraps.ico, %A_AppData%\MPC-HC Video Editor\icons\Fraps.ico, 1
FileInstall, icons\Wikipedia alt 1.ico, %A_AppData%\MPC-HC Video Editor\icons\Wikipedia alt 1.ico, 1
FileInstall, icons\Visual Studio 2012.ico, %A_AppData%\MPC-HC Video Editor\icons\Visual Studio 2012.ico, 1
FileInstall, icons\Calendar.ico, %A_AppData%\MPC-HC Video Editor\icons\Calendar.ico, 1
FileInstall, icons\InfoPath.ico, %A_AppData%\MPC-HC Video Editor\icons\InfoPath.ico, 1
FileInstall, icons\InfoPath alt 2.ico, %A_AppData%\MPC-HC Video Editor\icons\InfoPath alt 2.ico, 1
#Include compile_execute.ahk
#Include compile_globals.ahk
#Include compile_timers.ahk
#Include compile_library.ahk
#Include compile_main.ahk