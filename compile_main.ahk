file_show(file_path_csv)
#ifwinactive, ahk_class MPC-BE
	MButton::
		bookmark_started := "yes"
		timer := "off"
		file_path := window_title("ahk_class MPC-BE")
		file_path := file_info(file_path)
		file_path := checks(file_path)
		current_seconds := file_time()
		file_create(current_seconds)
		file_check(file_path_csv)
		file_show(file_path_csv)
		preserve_stats()
	return
#ifwinactive

checks(file_path) {
	file_path := file_rename(file_path)
	file_path := folder_rename(file_path)
	return file_path
}
	
clear_gui(file_path_csv:="") {
	if window_exist("MPC-BE Video Editor v2") {
		WinGetPos, current_x, current_y, current_w, current_h, MPC-BE Video Editor v2
		gui_existed := 1
	}
	data := file_read(file_path_csv)
	line_count := csv_linecount(file_path_csv)
	if (line_count = "" or data = ""){
		line_count := 1
	}
	loop, %line_count% {
		guicontrol, basic:hide, checkbox%a_index%
		guicontrol, basic:hide, radiobox%a_index%
		guicontrol, basic:hide, onecontrol
		guicontrol, basic:hide, twocontrol
		guicontrol, basic:hide, threecontrol
		guicontrol, basic:hide, fourcontrol
		guicontrol, basic:hide, EditedDuration
		guicontrol, basic:hide, TotalEditTime
		guicontrol, basic:hide, TimeTotal
		guicontrol, basic:hide, firstcontrol%a_index%
		guicontrol, basic:hide, secondcontrol%a_index%
		guicontrol, basic:hide, thirdcontrol%a_index%
		guicontrol, basic:hide, fourthcontrol%a_index%
		guicontrol, basic:hide, fifthcontrol%a_index%
		guicontrol, basic:hide, sixthcontrol%a_index%
		guicontrol, basic:hide, seventhcontrol%a_index%
		guicontrol, basic:hide, seventhcontrol%a_index%
		guicontrol, basic:hide, eighthcontrol%a_index%
		guicontrol, basic:hide, ninethcontrol%a_index%
		guicontrol, basic:hide, tenthcontrol%a_index%
		guicontrol, basic:hide, eleventhcontrol%a_index%
	}
	xpos := a_screenwidth - 1200
	if (gui_existed = 1) {
		Gui, basic:Show, NA Autosize x%current_x% y%current_y%, MPC-BE Video Editor v2
		ui_hide("basic")
		Gui, basic:Show, NA w400 x%current_x% y%current_y%, MPC-BE Video Editor v2
	} else {
			Gui, Show, NA w400 x%xpos% y140, MPC-BE Video Editor v2
	}
}

menu_add(menuName, var:="") {
	if (menuName = "load") {
		menu, loadmenu, deleteall
		if FileExist(file_path_csv) {
			Menu, LoadMenu, Add, Bookmarks: %file_path_csv%, load_bookmark
			Menu, LoadMenu, Icon, Bookmarks: %file_path_csv%, C:\Users\Chris\Desktop\ahk\photoshop\my icons\DOC.ico, 1
			Menu, LoadMenu, Add, Load, load_bookmark 
			Menu, LoadMenu, Icon, Load,	C:\Users\Chris\AppData\Local\Autodesk\webdeploy\production\12593c1ae56cc14e7d670f0d4833077eca3f3583\Neutron\UI\Base\Resources\API\addNewScript.png, 1
			Menu, LoadMenu, Add, Edit, edit_bookmark 
			Menu, LoadMenu, Icon, Edit, C:\ProgramData\Microsoft\Device Stage\Task\{07deb856-fc6e-4fb9-8add-d8f2cf8722c9}\settings.ico, 1
			Menu, LoadMenu, Add, Delete`t, delete_bookmark
			Menu, LoadMenu, Icon, Delete`t, C:\Program Files (x86)\K-Lite Codec Pack\Icons\delete.ico, 1
		} else {
			Menu, LoadMenu, Add, Unloaded: <click to load bookmark>, load_bookmark
			Menu, LoadMenu, Icon, Unloaded: <click to load bookmark>, C:\Program Files (x86)\Steam\steamapps\common\SteamVR\tools\steamvr_environments\game\core\tools\images\assetbrowser\appicon.ico, 1
		} 
		if FileExist(file_path) {	
			Menu, LoadMenu, Add, -----------------, no_action
			Menu, LoadMenu, Add, Video: %file_path%, load_video
			Menu, LoadMenu, Icon, Video: %file_path%, C:\Users\Chris\Desktop\ahk\photoshop\windows blinds\ICONS\ICONS Icon 10.ico, 1
			Menu, LoadMenu, Add, Load`t, load_video 
			Menu, LoadMenu, Icon, Load`t,	C:\Users\Chris\AppData\Local\Autodesk\webdeploy\production\12593c1ae56cc14e7d670f0d4833077eca3f3583\Neutron\UI\Base\Resources\API\addNewScript.png, 1			
			Menu, LoadMenu, Add, Delete, delete_video
			Menu, LoadMenu, Icon, Delete, C:\Program Files (x86)\K-Lite Codec Pack\Icons\delete.ico, 1
		} else {
			Menu, LoadMenu, Add, -----------------, no_action
			Menu, LoadMenu, Add, Unloaded: <click to load video>, load_video
			Menu, LoadMenu, Icon, Unloaded: <click to load video>, C:\Program Files (x86)\Steam\steamapps\common\SteamVR\tools\steamvr_environments\game\core\tools\images\assetbrowser\appicon.ico, 1
		}
	} else if (menuName = "actions") {
		Menu, ActionsMenu, Add, Merge, merge_split
		Menu, ActionsMenu, Icon, Merge, C:\Users\Chris\Desktop\ahk\backups\photoshop - 1.30.2022\New folder\icon_downloads.ico, 1
		Menu, ActionsMenu, Add, Split, merge_split
		Menu, ActionsMenu, Icon, Split, C:\users\Chris\Desktop\ahk\photoshop\NicePng_video-editing-icon-png_5009194.png, 1
		Menu, ActionsMenu, Add, -----------------, no_action
		Menu, ActionsMenu, Add, Undo last bookmark, erase
		Menu, ActionsMenu, Icon, Undo last bookmark, C:\Program Files\JDownloader\themes\standard\org\jdownloader\images\undo.png, 1
		Menu, ActionsMenu, Add, Clear all bookmarks, clear
		Menu, ActionsMenu, Icon, Clear all bookmarks, C:\Program Files\JDownloader\themes\standard\org\jdownloader\images\clear.png, 1
		Menu, ActionsMenu, Add, Reset everything, reset
		Menu, ActionsMenu, Icon, Reset everything, C:\Users\All Users\Microsoft\Device Stage\Task\{07deb856-fc6e-4fb9-8add-d8f2cf8722c9}\sync.ico, 1
		if FileExist(file_path_csv) {
			Menu, ActionsMenu, Enable, Undo last bookmark
			Menu, ActionsMenu, Enable, Clear all bookmarks
			Menu, ActionsMenu, Enable, Reset everything
		} else {
			Menu, ActionsMenu, Disable, Undo last bookmark
			Menu, ActionsMenu, Disable, Clear all bookmarks
			Menu, ActionsMenu, Enable, Reset everything
		}
	}
}

load_toolbar(gui_name) {
	Menu, SaveToMenu, DeleteAll
	Menu, LoadMenu, DeleteAll
	Menu, ActionsMenu, DeleteAll
	Menu, HotkeyMenu, DeleteAll
	Menu, StatsMenu, DeleteAll
	Menu, %gui_name%, Add, Save to, :SaveToMenu
	Menu, %gui_name%, Add, Load/Unload, :LoadMenu
	Menu, %gui_name%, Add, Actions, :ActionsMenu
	Menu, %gui_name%, Add, Hotkey, :HotkeyMenu
	Menu, %gui_name%, Add, Stats, :StatsMenu

	if !regexmatch(file_directory, "imO).+\\$")
		file_directory := file_directory . "\"
	Menu, SaveToMenu, Add, Select..., saveto_change
	Menu, SaveToMenu, Icon, Select..., C:\ProgramData\Microsoft\Windows\DeviceMetadataCache\dmrccache\en-US\d549d260-b60c-4738-909c-dee2f7270fc2\DeviceInformation\WheelMouseOptical.ico, 1
	Menu, SaveToMenu, Add, Current: %file_directory%, no_action
	Menu, SaveToMenu, Icon, Current: %file_directory%, C:\Users\Chris\Desktop\ahk\backups\photoshop - 1.30.2022\windows blinds\ICONS\ICONS Icon 12.ico, 1
	if (load_bookmark != "yes" or load_video = "yes") {
		menu_add("load", "file_path_csv")
		menu_add("load", "file_path")
		menu_add("actions", "file_path_csv")
		menu_add("actions", "file_path")
	}
	Menu, HotkeyMenu, Add, Select..., hotkey
	Menu, HotkeyMenu, Icon, Select..., C:\ProgramData\Microsoft\Windows\DeviceMetadataCache\dmrccache\en-US\d549d260-b60c-4738-909c-dee2f7270fc2\DeviceInformation\WheelMouseOptical.ico, 1
	if (activehotkey = "") 
		activehotkey := "MButton"
	Menu, HotkeyMenu, Add, Current: %activehotkey%, no_action
	Menu, HotkeyMenu, Icon, Current: %activehotkey%, C:\Windows\System32\KeyboardSystemToastIcon.png, 1
	Menu, StatsMenu, Add, On, on_stats
	Menu, StatsMenu, Add, Off, off_stats
	Menu, StatsMenu, Add, Advanced, advanced_stats
	Gui, Menu, %gui_name%
	return %gui_name%
}

file_info(file_path:="") {
	if regexmatch(file_path, "imO)MPC-BE\sx(32|64)\s(\d+\.\d+\.\d+)", num_match) {
		version := num_match[2]
		arch := num_match[1]
	}
	while (file_path = "MPC-BE x" . arch . " " . version . "" or file_path = "MPC-BE" or file_path = "MPC-BE Video Editor v2")
		file_path := window_title("ahk_class MPC-BE")
	file_path := regexreplace(file_path, "imO) - MPC-BE x" . arch . " " . version, "")
	splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
	if (folder_path_set = 1) 
		file_directory := folder_path
	if !regexmatch(file_directory, "imO).+\\$")
		file_directory := file_directory . "\"
	file_name := string_caseLower(file_name)
	file_extension_dot := "." . file_extension
	file_path_csv := strreplace(file_path, file_extension, "csv")
	if (load_bookmark = "yes")
		file_path_csv := select_file_path_csv
	data := file_read(file_path_csv)
	if instr(file_path, "MPC-BE")
		{
		file_path := regexreplace(file_path, "imO)(\s\-\s)?MPC-BE.+")
	}
	iniwrite, time1, %a_temp%\compile_data.ini, stored_data, file_path
	return file_path
}

file_rename(file_path) {
	splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
	old_file_path := file_path
	old_csv_path := strreplace(old_file_path,file_extension,"csv")
	invalidPattern := "[&\s',!\\\/@?:\.=%\*¡]"
	new_file_name := RegExReplace(file_name,invalidPattern,"-")
	new_file_path := path_join(file_directory,new_file_name,"." . file_extension)
	if (new_file_path != old_file_path) {
		window_kill("ahk_class MPC-BE")
		tooltip % "Renaming file"
		wait(2)
		FileMove, %old_file_path%, %new_file_path%
		file_path := new_file_path
		file_path_csv := strreplace(file_path,file_extension,"csv")
		new_csv_path := strreplace(file_path,file_extension,"csv")
		if (new_csv_path != old_csv_path) {
			FileMove, %old_csv_path%, %new_csv_path%
			file_path_csv := new_csv_path
		}
		wait(2)
		run, %file_path%
		wait(2)
		tooltip % ""
		splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
		file_directory := file_directory . "\"
		file_name := string_caseLower(file_name)
		file_extension_dot := "." . file_extension
	}
	iniwrite, time1, %a_temp%\compile_data.ini, stored_data, file_path
	return file_path
}
folder_rename(file_path) {
	splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
	old_folder_path := file_directory
	new_folder_path := RegExReplace(file_directory," ","-")
	if (new_folder_path != old_folder_path) {
		window_kill("ahk_class MPC-BE")
		tooltip % "Renaming folder"
		wait(2)
		FileMoveDir, %old_folder_path%, %new_folder_path%
		file_path := path_join(new_folder_path,file_both)
		file_path_csv := strreplace(file_path,file_extension,"csv")
		wait(2)
		run, %file_path%
		wait(2)
		tooltip % ""
		splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
		file_directory := file_directory . "\"
	}
	iniwrite, time1, %a_temp%\compile_data.ini, stored_data, file_path
	return file_path
}

file_check(file_path_csv) {
	data := file_read(file_path_csv)
    loop, parse, % data, `n
		{
		if a_loopfield =
			continue
        if (!regexmatch(a_loopfield,"imO)^\d+,\d+,Bookmark\d+$") and !regexmatch(a_loopfield,"imO)^\d+,$")) {
            msg("CSV file is corrupted")
            run(file_path_csv)
            return
        }
    }
	
}

file_time() {
	current_time := mpc_getTime()
	total_time := mpc_getTotalTime()
	time_total := time_longToAlt(total_time)
	current_seconds := time_longToSec(current_time)
	if (current_time = "") {
		msg("Failed to get the current time of the video.")
		reload
	} else {
		IniRead, erased, %a_temp%\compile_data.ini, stored_data, time_saved
		if (current_time <= time_saved) {
			msg("New bookmark must be greater than: " . time_secToAlt(previous_time))
			reload
		}
		current_time := time_secToLong(current_time)
	}
	if (current_seconds = 0) 
		current_seconds := 1
	previous_time := current_seconds
	set_time := current_time
	return previous_time
}

file_create(current_seconds) {
	data := file_read(file_path_csv)
	if (data = "") {
		line_num := 1
		pretty_print := "[1] " . time_secToShort(current_seconds)
		file_write(file_path_csv, current_seconds . ",")
		data := file_read(file_path_csv)
	} else {
		last_line := csv_lastline(file_path_csv)
		line_num := csv_linecount(file_path_csv)
		if regexmatch(last_line, "imO)(\d+)\,(\d+)\,Bookmark(\d+)", line_time) {
			file_write(file_path_csv, "`n" . current_seconds . ",")
			data := file_read(file_path_csv)
			line_num++
	;		pretty_print .= "[" . line_num . "] " . time_secToShort(current_seconds)
			bookmark_status := "incomplete"
		} else {
			file_write(file_path_csv, current_seconds . ",Bookmark" . line_num)
			data := file_read(file_path_csv)
			if regexmatch(a_loopfield, "imO)(\d+)\,(\d+)\,Bookmark(\d+)", line_time) {
				difference_seconds := line_time[2] - line_time[1]
;				pretty_print .= " - " . time_secToShort(current_seconds) . "(" . time_secToAlt(difference_seconds) . ")"
				bookmark_status := "complete"
			}
		}
	}
	return file_path_csv
}

edit_duration(file_path_csv:="") {
	data := file_read(file_path_csv)
	loop, parse, % data, `n	
		{
		if regexmatch(a_loopfield, "imO)(\d+)\,(\d+)\,Bookmark(\d+)", time) {
			time1 := time[1]
			time2 := time[2]
			duration%a_index% := time2 - time1
			duration_total+= duration%a_index%
		}
	}
	return time_secToAlt(duration_total)
}

file_show(file_path_csv) {
	global
	if window_exist("MPC-BE Video Editor v2") {
		WinGetPos, current_x, current_y, current_w, current_h, MPC-BE Video Editor v2
		gui_existed := 1
	}
	ui_destroy("basic")
	gui, basic:default
	gui, +owner +border -resize +maximizebox +sysmenu +caption +toolwindow +DPIScale +alwaysontop +lastfound
	gui, margin, 0, 0
	Gui, Color, 000000
	gui, Font, w1000 norm s10 cWhite, Segoe UI
	if (clear_timestamps != "yes") {
		line_num := csv_linecount(file_path_csv)
		loop, parse, % data, `n
			{
			gui, Font, w1000 norm s10 cWhite, Segoe UI
			if regexmatch(a_loopfield, "imO)(\d+)\,(\d+)\,Bookmark(\d+)", line_time) {
				ui_add_radio(, "r1 +w23 xs y+5 +gChecked +vRadiobox" . a_index)
				ui_add_checkbox(, "+w30 x+5 yp+0 +center +vCheckBox" . a_index)
				time1 := line_time[1]
				time2 := line_time[2]
				iniwrite, %time1%, %a_temp%\compile_data.ini, stored_data, time_saved
				line_num := line_time[3]
				duration_time := time_secToAlt(time2 - time1)
				bookmark_status := "complete"
				if ((time2 - time1) <= 0)  {
					msg("Duration time is invalid.")
					run(file_path_csv)
					reload
				}
				time1 := time_secToShort(time1)
				time2 := time_secToShort(time2)
				gui, add, text, x+ +center cWhite vFirstControl%a_index%, [
				gui, add, text, x+ +center cGreen vSecondControl%a_index%, %line_num%
				gui, add, text, x+ +center cWhite vThirdControl%a_index%, ]
				gui, add, text, x+ +center cWhite vFourthControl%a_index%, %a_space%
				gui, add, text, x+ +center cBlue  vFifthControl%a_index% gtime_split, %time1%
				gui, add, text, x+ +center cWhite vSixthControl%a_index%, %a_space%-%a_space%   
				gui, add, text, x+ +center cBlue  vSeventhControl%a_index% gtime_split, %time2%
				gui, add, text, x+ +center cWhite vEighthControl%a_index%, %a_space%
				gui, add, text, x+ +center cWhite vNinethControl%a_index%, (
				gui, add, text, x+ +center cYellow vTenthControl%a_index%, %duration_time%
				gui, add, text, x+ +center cWhite vEleventhControl%a_index%, )			
				button%a_index% := a_loopfield
				radiobox%a_index% := button%a_index%
			} else if regexmatch(a_loopfield, "imO)(\d+)\,", line_time) {
				line_num := csv_linecount(file_path_csv)
				ui_add_radio(, "r1 +w23 xs y+5 +gChecked +vRadiobox" . a_index)
				ui_add_checkbox(, "+w30 x+5 yp+0 +center +vCheckBox" . a_index)
				time1 := time_secToShort(line_time[1])
				bookmark_status := "incomplete"
				iniwrite, %time1%, %a_temp%\compile_data.ini, stored_data, time_saved
				gui, Font, w1000 norm s10 cWhite, Segoe UI
				gui, add, text, x+ +center cwhite vFirstControl%a_index%, [
				gui, add, text, x+ +center cRed vSecondControl%a_index%, %line_num%
				gui, add, text, x+ +center cwhite vThirdControl%a_index%, ] 
				gui, add, text, x+ +center cwhite vFourthControl%a_index%, %a_space%
				gui, add, text, x+ +center cBlue vFifthControl%a_index% gtime_split, %time1%
				gui, Font, w1000 norm s10 cWhite, Segoe UI
			}
		} 
		edited_duration := edit_duration(file_path_csv)
		if (edited_duration != "") {
			total_time := mpc_getTotalTime()
			time_total := time_longToAlt(total_time)
			gui, add, text, xs y+5 +center cWhite vTotalEditTime, Total edit time:
			gui, add, text, x+ +center cwhite vOneControl, %a_space%
			gui, add, text, x+ +center cyellow vEditedDuration, %edited_duration%
			gui, add, text, x+ +center cwhite vTwoControl, %a_space%
			gui, add, text, x+ +center cwhite vThreeControl, /
			gui, add, text, x+ +center cwhite vFourControl, %a_space%
			gui, add, text, x+ +center cYellow vTimeTotal, %time_total%
		}
		xpos := a_screenwidth - 1200
		load_toolbar("test")
		if (gui_existed = 1)
			Gui, Show, NA x%current_x% y%current_y% w400, MPC-BE Video Editor v2
		else
			Gui, Show, NA w400 x%xpos% y140, MPC-BE Video Editor v2
		if (timer = "on")
			seek_steps()
		stats := info("all")
		gui, color, 000000, 000000
		gui, Font, w1000 norm s10 cWhite, Segoe UI
		ui_add_edit(stats, "xs +left +vstats_data +hscroll +w400 +h150")
		advanced := info("advanced")
		ui_add_edit(advanced, "xs +left +vstats_advanced +hscroll +w400 +h520")
		guicontrol, hide, stats_data
		guicontrol, hide, stats_advanced 
		for index, value in checkbox_index
			GuiControl, , %value%, 1
	}
}

ffmpeg_time(file_path_csv) {
	last_line := csv_lastline(file_path_csv)
	loop, parse, % data, `n
		{
		if regexmatch(a_loopfield, "imO)(\d+)\,(\d+)\,Bookmark(\d+)", line_time) {
			time1 := line_time[1]
			time2 := line_time[2]
			time3 := line_time[3]
			if (time3 = 1)
				ffmpeg_time := time_secToLong(time1) . " " . time_secToLong(time2)
			else
				ffmpeg_time .= " " . time_secToLong(time1) . " " . time_secToLong(time2)
		}
	}
	if regexmatch(ffmpeg_time, "imO)(\d+:\d+:\d+)\s(\d+:\d+:\d+).?$", ffmpeg_time_last)
		ffmpeg_time_last := ffmpeg_time_last[1] . " " . ffmpeg_time_last[2]
	return ffmpeg_time
}

seek_steps(t_time:="") {
	iniread, previous_time, %a_temp%\compile_data.ini, stored_data, time_saved
	if (timer = "on") {
		if (previous_time > 59)
			previous_time := time_secToShort(previous_time)
		prefix := special(previous_time)
		get_clicked_time := previous_time
	} else {
		prefix := special(t_time)
		get_clicked_time := t_time
	}
	num := number_countdigits(get_clicked_time)
	if (num = 6)
		regexmatch(get_clicked_time, "imO)(..):(..):(..)", time)
	else if (num = 5)
		regexmatch(get_clicked_time, "imO)(.):(..):(..)", time)
	else if (num = 4)
		regexmatch(get_clicked_time, "imO)(..):(..)", time)
	else if (num = 3)
		regexmatch(get_clicked_time, "imO)(.):(..)", time)
	else if (num = 2)
		regexmatch(get_clicked_time, "imO)(..)", time)
	else if (num = 1)
		regexmatch(get_clicked_time, "imO)(.)", time)
	;msg("prefix = " . prefix . "`rnum = " . num . "`rt_time = " . t_time . "`rtime[1] = " . time[1] . "`rtime[2] = " . time[2] . "`rget_clicked_time = " . get_clicked_time)
	new_time := prefix . time[1] . time[2] . time[3] . ".000"
	WinActivate, ahk_class MPC-BE
	Send, ^g
	WinWaitActive, Go To...
	Controlfocus, MFCMaskedEdit1, Go To...
	sendraw % new_time
	send("{enter}")
}	

special(var1) {
	total_time := mpc_gettotaltime()
	if regexmatch(total_time, "imO)(\d{2}):(\d{2}):(\d{2})", time_seek) {
		num := number_countDigits(var1)
		if (num = 6)
			prefix :=
		else if (num = 5)
			prefix := 0
		else if (num = 4) 
			prefix := 00
		else if (num = 3) 
			prefix := 000
		else if (num = 2) 
			prefix := 0000
		else if (num = 1)
			prefix := 00000
	} else if regexmatch(total_time, "imO)(\d{2}):(\d{2})", time_seek) {
		num := number_countDigits(var1)
		if (num = 4) 
			prefix := 
		else if (num = 3) 
			prefix := 0
		else if (num = 2) 
			prefix := 00
		else if (num = 1)
			prefix := 000
	}
	return prefix
}

preserve_stats() {
	if window_exist("MPC-BE Video Editor v2") {
		WinGetPos, current_x, current_y, current_w, current_h, MPC-BE Video Editor v2
		gui_existed := 1
	}
	xpos := a_screenwidth - 1200
	if (stats_hidden = 0 && advanced_hidden = 0) {
		stats := info("all")
		advanced := info("advanced")
		guicontrol, , stats_data, %stats%
		guicontrol, show, stats_data
		guicontrol, , stats_advanced, %advanced%
		guicontrol, show, stats_advanced
		menu, statsmenu, check, advanced
		menu, statsmenu, check, on
	} else if (stats_hidden = 0 and advanced_hidden = 1) {
		stats := info("all")
		guicontrol, , stats_data, %stats%
		menu, statsmenu, uncheck, off
		menu, statsmenu, check, on
		menu, statsmenu, uncheck, advanced
		guicontrol, show, stats_data
		guicontrol, hide, stats_advanced
	} 
	if (gui_existed = 1) {
		Gui, Show, NA Autosize x%current_x% y%current_y%, MPC-BE Video Editor v2
		ui_hide("basic")
		Gui, Show, NA w400 x%current_x% y%current_y%, MPC-BE Video Editor v2
	} else
			Gui, Show, NA w400 x%xpos% y140, MPC-BE Video Editor v2
}

time_split:
    GuiControlGet, t_time,, %A_GuiControl%    ; grab the contents of the control you clicked
    seek_steps(t_time)    
return

basicGuiClose:
	Exitapp
return


advanced_stats:
    if window_exist("MPC-BE Video Editor v2") {
        WinGetPos, current_x, current_y, current_w, current_h, MPC-BE Video Editor v2
        gui_existed := 1
    }
    xpos := a_screenwidth - 1200
    if (checked_advanced = "on") {
        checked_advanced := "off"
        Menu, StatsMenu, Uncheck, Advanced
    } else {
        checked_advanced := "on"
        Menu, StatsMenu, Check, Advanced
    }
    if (checked_advanced = "on" && checked_on = "on") {
        stats := info("all")
        advanced := info("advanced")
        guicontrol, , stats_data, %stats%
        guicontrol, show, stats_data
        guicontrol, , stats_advanced, %advanced%
        guicontrol, show, stats_advanced
		stats_hidden := 0
		advanced_hidden := 0
    } else if (checked_advanced = "on" && checked_off = "on") {
        guicontrol, hide, stats_data
        guicontrol, hide, stats_advanced
		stats_hidden := 1
		advanced_hidden := 1
    } else if (checked_advanced = "off" && checked_on = "on") {
        stats := info("all")
        guicontrol, , stats_data, %stats%
        guicontrol, show, stats_data
        guicontrol, hide, stats_advanced
		stats_hidden := 0
		advanced_hidden := 1
    } else if (checked_advanced = "off" && checked_off = "on") {
        guicontrol, hide, stats_data
        guicontrol, hide, stats_advanced
		stats_hidden := 1
		advanced_hidden := 1
    } else if (checked_advanced = "on" && checked_off = "off") {
        guicontrol, hide, stats_data
        guicontrol, hide, stats_advanced
		stats_hidden := 1
		advanced_hidden := 1
    }
	if (gui_existed = 1) {
		Gui, Show, NA Autosize x%current_x% y%current_y%, MPC-BE Video Editor v2
		ui_hide("basic")
		Gui, Show, NA w400 x%current_x% y%current_y%, MPC-BE Video Editor v2
	} else
		Gui, Show, NA w400 x%xpos% y140, MPC-BE Video Editor v2
return

off_stats:
    if window_exist("MPC-BE Video Editor v2") {
        WinGetPos, current_x, current_y, current_w, current_h, MPC-BE Video Editor v2
        gui_existed := 1
    }
    xpos := a_screenwidth - 1200
    if (checked_off = "on") {
        checked_off := "off"
        Menu, StatsMenu, Uncheck, off
    } else {
        checked_off := "on"
        Menu, StatsMenu, Check, off
  ;      Menu, StatsMenu, Icon, off, C:\Program Files\NVIDIA Corporation\Installer2\installer.{2EFE8F79-83E9-4A18-A452-34FFB4906FB3}\check.png, 1
        checked_on := "off"
        Menu, StatsMenu, Uncheck, On
    }
    ; Update display based on off_stats state
    if (checked_off = "on") {
        guicontrol, hide, stats_data
        guicontrol, hide, stats_advanced
		stats_hidden := 1
		advanced_hidden := 1
    } else if (checked_on = "on" && checked_advanced = "on") {
        stats := info("all")
        advanced := info("advanced")
        guicontrol, , stats_data, %stats%
        guicontrol, show, stats_data
        guicontrol, , stats_advanced, %advanced%
        guicontrol, show, stats_advanced
		stats_hidden := 0
		advanced_hidden := 0
		if (gui_existed = 1) {
			Gui, Show, NA Autosize x%current_x% y%current_y%, MPC-BE Video Editor v2
			ui_hide("basic")
			Gui, Show, NA w400 x%current_x% y%current_y%, MPC-BE Video Editor v2
		} else
			Gui, Show, NA w400 x%xpos% y140, MPC-BE Video Editor v2
    } else if (checked_on = "on" && checked_advanced = "off") {
        stats := info("all")
        guicontrol, , stats_data, %stats%
        guicontrol, show, stats_data
        guicontrol, hide, stats_advanced
		stats_hidden := 0
		advanced_hidden := 1
    }
	if (gui_existed = 1) {
		Gui, Show, NA Autosize x%current_x% y%current_y%, MPC-BE Video Editor v2
		ui_hide("basic")
		Gui, Show, NA w400 x%current_x% y%current_y%, MPC-BE Video Editor v2
	} else
		Gui, Show, NA w400 x%xpos% y140, MPC-BE Video Editor v2
return

on_stats:
    if window_exist("MPC-BE Video Editor v2") {
        WinGetPos, current_x, current_y, current_w, current_h, MPC-BE Video Editor v2
        gui_existed := 1
    }
    xpos := a_screenwidth - 1200
    if (checked_on = "on") {
        checked_on := "off"
        Menu, StatsMenu, Uncheck, On
    } else {
        checked_on := "on"
        Menu, StatsMenu, Check, On
    ;    Menu, StatsMenu, Icon, On, C:\Program Files\NVIDIA Corporation\Installer2\installer.{2EFE8F79-83E9-4A18-A452-34FFB4906FB3}\check.png, 1
        checked_off := "off"
        Menu, StatsMenu, Uncheck, off
    }
    ; Update display based on on_stats state
    if (checked_on = "on" && checked_advanced = "on") {
        stats := info("all")
        advanced := info("advanced")
        guicontrol, , stats_data, %stats%
        guicontrol, show, stats_data
        guicontrol, , stats_advanced, %advanced%
        guicontrol, show, stats_advanced
		stats_hidden := 0
		advanced_hidden := 0
    } else if (checked_on = "on" && checked_advanced = "off") {
        stats := info("all")
        guicontrol, , stats_data, %stats%
        guicontrol, show, stats_data
        guicontrol, hide, stats_advanced
		stats_hidden := 0
		advanced_hidden := 1
    } else if (checked_on = "on" && checked_advanced = "") {
        stats := info("all")
        guicontrol, , stats_data, %stats%
        guicontrol, show, stats_data
        guicontrol, hide, stats_advanced
		stats_hidden := 0
		advanced_hidden := 1
    } else if (checked_off = "off" && checked_on = "off") {
        guicontrol, hide, stats_data
		guicontrol, hide, stats_advanced
		stats_hidden := 1
		advanced_hidden := 1
        guicontrol, hide, stats_advanced
    } else if (checked_off = "on") {
        guicontrol, hide, stats_data
		stats_hidden := 1
		advanced_hidden := 1
        guicontrol, hide, stats_advanced
    }
	if (gui_existed = 1) {
		Gui, Show, NA Autosize x%current_x% y%current_y%, MPC-BE Video Editor v2
		ui_hide("basic")
		Gui, Show, NA w400 x%current_x% y%current_y%, MPC-BE Video Editor v2
	} else
		Gui, Show, NA w400 x%xpos% y140, MPC-BE Video Editor v2
return

load_bookmark:
	fileselectfile, select_file_path_csv, 3,, Select a bookmark file, CSV files (*.csv)
	if (select_file_path_csv) {
		file_path_csv := select_file_path_csv
		menu_add("load", "file_path_csv")
		menu_add("load", "file_path")
		menu_add("actions", "file_path_csv")
		menu_add("actions", "file_path")
		file_show(file_path_csv)
		stats := info("all")
		guicontrol,, stats_data, %stats%
		advanced := info("advanced")
		guicontrol,, stats_advanced, %advanced%
		previous_time := ""
	}
return

edit_bookmark:
	run, %file_path_csv%
	window_waitactive("ahk_class MPC-BE")
	menu_add("load", "file_path_csv")
	menu_add("load", "file_path")
	menu_add("actions", "file_path_csv")
	menu_add("actions", "file_path")
	file_show(file_path_csv)
	stats := info("all")
	guicontrol,, stats_data, %stats%
	advanced := info("advanced")
	guicontrol,, stats_advanced, %advanced%
return

delete_bookmark:
	file_recycle(file_path_csv)
	file_path_csv := ""
	menu_add("load", "file_path_csv")
	file_show(file_path_csv)
	stats := info("all")
	guicontrol,, stats_data, %stats%
	advanced := info("advanced")
	guicontrol,, stats_advanced, %advanced%
	previous_time := ""
	clear_gui()
return

saveto_change:
	file_directory_old := file_directory
	FileSelectFolder, folder_path, , 3, Select a folder
	if (folder_path) {
		file_directory := folder_path
		if !regexmatch(file_directory, "imO).+\\$")
			file_directory := file_directory . "\"
		thismenuitempos := A_ThisMenuItemPos + 1 . "&"
		menu, savetomenu, rename, %thismenuitempos%, Current: %file_directory%
		stats := info("all")
		guicontrol,, stats_data, %stats%
		advanced := info("advanced")
		guicontrol,, stats_advanced, %advanced%
	}
return

load_video:
	FileSelectFile, select_file_path, 3,, Select a video file,(Video Files (*.3gp;*.3g2;*.3gpp;*.asf;*.avi;*.divx;*.dv;*.evo;*.f4v;*.flv;*.m2ts;*.m4v;*.mkv;*.mov;*.mp4;*.mpeg;*.mpg;*.mts;*.ogm;*.ogv;*.qt;*.rm;*.rmvb;*.swf;*.ts;*.vob;*.webm;*.wmv)|*.3gp;*.3g2;*.3gpp;*.asf;*.avi;*.divx;*.dv;*.evo;*.f4v;*.flv;*.m2ts;*.m4v;*.mkv;*.mov;*.mp4;*.mpeg;*.mpg;*.mts;*.ogm;*.ogv;*.qt;*.rm;*.rmvb;*.swf;*.ts;*.vob;*.webm;*.wmv|All Files (*.*)|*.*)
	if (select_file_path) {
		file_path := select_file_path
		menu_add("load", "file_path_csv")
		menu_add("load", "file_path")
		menu_add("actions", "file_path_csv")
		menu_add("actions", "file_path")
		run, %file_path%
		file_show(file_path_csv)
		stats := info("all")
		guicontrol,, stats_data, %stats%
		advanced := info("advanced")
		guicontrol,, stats_advanced, %advanced%
	}
return

delete_video:
	file_recycle(file_path)
	menu_add("actions", "file_path")
	file_show(file_path_csv)
	stats := info("all")
	guicontrol,, stats_data, %stats%
	advanced := info("advanced")
	guicontrol,, stats_advanced, %advanced%
	clear_gui()
return

merge_split:
	iniread, time1, %a_temp%\compile_data.ini, stored_data, file_path
	if (file_path_new != "") {
		file_path := file_path_new
		splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
		if !regexmatch(file_directory, "imO).+\\$")
			file_directory := file_directory . "\"
		file_extension_dot := "." . file_extension
	}
	if (file_path_csv_new != "")
		file_path_csv := file_path_csv_new
	if (load_bookmark = "yes")
		file_path_csv := select_file_path_csv
	if (ffmpeg_time = "")
		ffmpeg_time := ffmpeg_time(file_path_csv)
	if (bookmark_status = "complete") {
		if (a_thismenuitem = "Merge") 
			loop_count := 1
		else if (a_thismenuitem = "Split") {
			loop_count := ""
			count := 0
			pos := 1
			while pos := RegExMatch(ffmpeg_time, "imO)(\d{2}:\d{2}:\d{2})\s(\d{2}:\d{2}:\d{2})", match, pos)
				{
				loop_count++
				pos += StrLen(match[0])
			}
		}
		if !regexmatch(file_directory, "imO).+\\$")
			file_directory := file_directory . "\"
		Loop, %loop_count% {
			if (a_thismenuitem = "Split") {
				file_path_create := " " . file_path . " " . file_directory . file_name . "[cs" . a_index . "]" . file_extension_dot . " "
				if regexmatch(ffmpeg_time, "imO)(\d+:\d+:\d+\s\d+:\d+:\d+)", pairs) {
					new_ffmpeg_time := pairs[0]
					ffmpeg_time := regexreplace(ffmpeg_time, "imO)" . pairs[0])
					run(a_temp . "\compile_merge.bat" . file_path_create . new_ffmpeg_time)
				}
			}
			if (a_thismenuitem = "Merge") {
				iniread, time1, %a_temp%\compile_data.ini, stored_data, file_path
				if instr(file_path,"MPC-BE")
					file_path	:= file_path := regexreplace(file_path, "imO)(\s\-\s)?MPC\-BE\s.+")
					
				file_path_create := " " . file_path . " " . file_directory . file_name . "[done]" . file_extension_dot . " "		
				run(a_temp . "\compile_merge.bat" . file_path_create . ffmpeg_time)
			}
			window_waitExist("cmd.exe")
			window_waitClose("cmd.exe")
		}
		msgbox, 4, , Delete source video and csv file?
		ifmsgbox, Yes
			{
			file_recycle(file_path_csv)
			file_recycle(file_path)
			file_path_csv := ""
			file_path := ""
			menu_add("load", "file_path_csv")
			menu_add("load", "file_path")
			global_delete := 1
		}
		ifmsgbox, No
			{
			
		}
	} else {
		msg("You cannot split or merge files without completing the last bookmark.")
		return
	}
	clear_gui()
	winactivate, ahk_class MPC-BE
	bookmarkstatus := ""
	bookmark_started := ""
	data := ""
	ffmpeg_time := ""
	file_both := ""
	file_directory := ""
	file_drive := ""
	file_extension :=""
	file_extension_dot := ""
	file_name := ""
	file_path_create := ""
	last_line := ""
	loop_count := ""
	show := ""
	stats := ""
	time_total := ""
return

erase:
	file_info(file_path)
	if (load_bookmark = "yes")
		file_path_csv := select_file_path_csv
	iniwrite, 1, %a_temp%\compile_data.ini, stored_data, bookmark_removed
	if regexmatch(ffmpeg_time, "imO)(.+)(\d{2}\:\d{2}\:\d{2})$", ffmpeg_remove_time)
		ffmpeg_time := ffmpeg_remove_time[1]
	if (file_path_csv != "") {
		if instr(csv_lastline(file_path_csv), "Bookmark")
			last_line := regexreplace(csv_lastline(file_path_csv), "imO)(\d+\,)(\d+\,Bookmark\d+)", "$2")
		else
			last_line := regexreplace(csv_lastline(file_path_csv), "imO)(\d+\,)", "$1")
		if (csv_linecount(file_path_csv) = 1) and if regexmatch(last_line, "imO)^(\d+),$") 
			file_delete(file_path_csv)
		else
			file_write(file_path_csv, strreplace(csv_data(file_path_csv), last_line), "w")
	}
	IniRead, erased, %a_temp%\compile_data.ini, stored_data, bookmark_removed
	file_show(file_path_csv)
	stats := info("all")
	guicontrol,, stats_data, %stats%
	advanced := info("advanced")
	guicontrol,, stats_advanced, %advanced%
return

clear:
	file_recycle(file_path_csv)
	file_path_csv := ""
	menu_add("load", "file_path_csv")
	menu_add("load", "file_path")
	clear_gui()
return

reset:
	reload
return

checked:
	if (file_name_new != "")
		file_name := file_name_new
	if (file_path_new != "")
		file_path := file_path_new
	if (file_path_csv_new != "")
		file_path_csv := file_path_csv_new 
    if regexmatch(%a_guicontrol%, "imO)(\d+)\,(\d+)\,Bookmark(\d+)", checked_time) {
        compare1 := checked_time[0]
		data := file_read(file_path_csv)
		loop, parse	, % data, `n
			{
			if regexmatch(a_loopfield, "imO)(\d+)\,(\d+)\,Bookmark(\d+)", line_time) {
				compare2 := line_time[0]
				if (compare1 = compare2) {
                    matched_index := A_Index
                    break
				}
			}
		}
    }
    checked_ffmpeg_time := time_secToLong(checked_time[1]) . " " . time_secToLong(checked_time[2])
    file_path_check := file_directory . file_name . "[cs1]" . file_extension_dot
    if fileexist(file_path_check) {
        while fileexist(file_path_check) {
            if regexmatch(file_path_check, "imO).+(\[cs(\d+)\]).+", cs_match)
                new_num := cs_match[2] + 1
            file_path_check := file_directory . file_name . "[cs" . new_num . "]" . file_extension_dot
        }
    }
    file_path_create := file_path . " " . file_path_check
	clipboard := A_Temp . "\compile_merge.bat " . file_path_create . " " . checked_ffmpeg_time
    run(A_Temp . "\compile_merge.bat " . file_path_create . " " . checked_ffmpeg_time)
    if not IsObject(checkbox_index)
        checkbox_index := []
    check_checkbox := "checkbox" . matched_index
    checkbox_index.Push(check_checkbox)
	for index, value in checkbox_index
		GuiControl, , %value%, 1
	window_waitclose("cmd.exe")
	winactivate, ahk_class MPC-BE
Return

hotkey:
	Gui, HotkeyGUI:New
	Gui, HotkeyGUI:Add, Text, , Press desired hotkey:
	Gui, HotkeyGUI:Add, Hotkey, vSelectedHotkey, MButton
	Gui, HotkeyGUI:Add, Button, gHotkeyGUISubmit, OK
	Gui, HotkeyGUI:Add, Button, gHotkeyGUICancel, Cancel
	Gui, HotkeyGUI:Show, , Set new hotkey
return

no_action:

return

HotkeyGUIClose:
HotkeyGUIEscape:
	Gui, HotkeyGUI:Destroy
Return

HotkeyGUISubmit:
	Gui, HotkeyGUI:Default
	Gui, Submit, NoHide
	GuiControlGet, SelectedHotkey, , SelectedHotkey
	If (SelectedHotkey = "") {
		MsgBox, 16, Error, Hotkey cannot be empty. Please select a hotkey.
		Return 
	}
	If (ActiveHotkey != "")
		Hotkey, %ActiveHotkey%, MyHotkeyLabel, Off
	Hotkey, %SelectedHotkey%, MyHotkeyLabel, On
	MsgBox, Hotkey set to: %SelectedHotkey%
	ActiveHotkey := SelectedHotkey
	Gui, Destroy
	file_show(file_path_csv)
Return

HotkeyGUICancel:
	Gui, HotkeyGUI:Destroy
Return
		
exit_script:
	exitapp
return

reload_script:
	reload
return

edit_script:
	run(file_path_csv)
return

view_variables:
	listvars
return
