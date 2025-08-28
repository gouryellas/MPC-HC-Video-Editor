main:
	file_path := mpc_getSource2()
	bookmark_started := "yes"
	file_path := file_info2(file_path)
	file_path := file_rename2(file_path)
	file_path := folder_rename2(file_path)
	current_seconds := file_time2()
	file_create2(current_seconds)
	show_gui2()
	update_menus2()
return


TR_SelectType:
	transition_effect := a_thismenuitem
	menu, settingsmenu, rename, 2&, Current: %transition_effect%
return

DUR_SelectType:
	transition_duration := a_thismenuitem
	menu, settingsmenu, rename, 5&, Current: %transition_duration%
	transition_duration := strreplace(transition_duration,"s")
return

enter_time:
	gui, enterGUI:New
	gui, +owner +border -resize +maximizebox +sysmenu +caption +toolwindow +DPIScale +alwaysontop +lastfound
	gui, enterGUI:margin, 5, 5
	Gui, enterGUI:Color, 000000
	gui, enterGUI:font, bold s11 cBlack, Fira Code
	Gui, enterGUI:Add, Edit, center vEnterTime w250, 00:00:00
	gui, enterGUI:font, bold s11 cBlack, Fira Code
	Gui, enterGUI:Add, Button, xs w95 center gEnterGUISubmit default, OK
	gui, enterGUI:Add, text, x+, %a_space%
	Gui, enterGUI:Add, Button, x+ w145 center gEnterGUICancel, Cancel
	Gui, enterGUI:Show, , Enter a time or range of time
return

EnterGUISubmit:
	guicontrolget, entertime, , entertime
	time_seconds2 := ""
	time_seconds3 := ""
	if regexmatch(entertime, "imO)(\d+:\d+|\d+)\s\-\s(\d+\:\d+|\d+)", get_time) {
		get_time1 := get_time[1]
		get_time2 := get_time[2]
		time_seconds2 := time_shortToSec2(get_time1)
		time_seconds3 := time_shortToSec2(get_time2)
	} else if regexmatch(entertime, "imO)(\d+\:)?(\d+\:)?(\d+)")
		time_seconds1 := time_shortToSec2(entertime)
	else {
		msg2("Invalid time. Try again.")
		return
	}
	last_line := csv_lastline2(file_path_csv)
	linecount := csv_linecount2(file_path_csv)
	data := file_read2(file_path_csv)
	last_line := csv_lastline2(file_path_csv) 
	line_count := csv_linecount2(file_path_csv)
	if regexmatch(last_line, "imO)(\d+)\,(\d+),Bookmark(\d+)") {
		if (time_seconds2 != "" AND time_seconds3 != "")
			file_write2(file_path_csv, "`n" . time_seconds2 . "," . time_seconds3 . ",Bookmark" . (line_count + 1))
		else
			file_write2(file_path_csv, "`n" . time_seconds1 . ",")
	} else {
		if (time_seconds2 != "" AND time_seconds3 != "") {
			msg2("You cannot enter a pair. The last bookmark is unfinished.")
			return
		} else
			file_write2(file_path_csv, time_seconds1 . ",Bookmark" . line_count)
	}
	gui, submit
	show_gui2()
	stats := info2("all")
	guicontrol,, stats_data, %stats%
	advanced := info2("advanced")
	guicontrol,, stats_advanced, %advanced%
return

EnterGUICancel:
	ui_destroy2("enterGUI")
return


time_split:
    GuiControlGet, t_time,, %A_GuiControl%
    seek_steps2(t_time)
return

basicGuiClose:
	Exitapp
return

csv_check:
	result := csv_repair2(file_path_csv)
return

stats:	
    if window_exist2("MPC-HC Video Editor v2.1") {
        WinGetPos, current_x, current_y, current_w, current_h, MPC-HC Video Editor v2.1
        gui_existed := 1
    }
    xpos := a_screenwidth - 1200
	if (a_thismenuitem = "Advanced") {
		if (advanced_checked = 1) {
			advanced_checked := 0
			Menu, StatsMenu, Uncheck, Advanced
			guicontrol, hide, stats_advanced
		} else if (advanced_checked = 0 or advanced_checked = "") {
			advanced_checked := 1
			Menu, StatsMenu, Check, Advanced
			guicontrol, show, stats_advanced
		}
		if (enable_checked = 1) {
			advanced := info2("advanced")
            line_count := StrSplit(advanced, "`n").Length()
			if (first_check_advanced = "") {
				ui_add_edit2(advanced, "+left +vstats_advanced +hscroll +w500 +r" . line_count)	
				first_check_advanced := 1
			}
		}
	} else if (a_thismenuitem = "Enable") {
		if (enable_checked = 1) {
			enable_checked := 0
			advanced_checked := 0
			Menu, StatsMenu, Uncheck, Enable
			Menu, StatsMenu, Uncheck, advanced
			guicontrol, hide, stats_data
			guicontrol, hide, stats_advanced
			Menu, StatsMenu, Disable, Advanced
		} else {
			enable_checked := 1
			stats := info2("all")
            line_count := StrSplit(stats, "`n").Length()
			if (first_check_enable = "") {
				ui_add_edit2(stats, " xs +left +vstats_data +hscroll +w500 +r" . line_count)
				first_check_enable := 1
			}
			Menu, StatsMenu, Check, Enable
			Menu, StatsMenu, Enable, Advanced
			guicontrol, show, stats_data
		}
	}
	if (gui_existed = 1) {
		Gui, Show, NA Autosize x%current_x% y%current_y%, MPC-HC Video Editor v2.1
		Gui, Show, NA w500 x%current_x% y%current_y%, MPC-HC Video Editor v2.1
	} else
		Gui, Show, NA w500 x%xpos% y140, MPC-HC Video Editor v2.1
return

load_bookmark:	
	fileselectfile, select_file_path_csv, 3,, Select a bookmark file, CSV files (*.csv)
	if (select_file_path_csv) {
		g_manual_load_in_progress := 1
		load_bookmark := 1
		file_path_csv := file_path_csv2 := select_file_path_csv
		stats := info2("all")
		guicontrol,, stats_data, %stats%
		advanced := info2("advanced")
		guicontrol,, stats_advanced, %advanced%
		file_path_check := check_matching_video(file_path_csv)
		if fileexist(file_path_check) {
			file_path := file_path_check
			run, %file_path%
			Menu, LoadMenu, Rename, 6&, Video: %file_path%
		}
		Menu, LoadMenu, Rename, 1&, Bookmarks: %file_path_csv%
		show_gui2()
		update_menus2()
	} else
		load_bookmark := ""
return

edit_bookmark:
	run, %file_path_csv%
	window_waitactive2("ahk_class MediaPlayerClassicW")
	stats := info2("all")
	guicontrol,, stats_data, %stats%
	advanced := info2("advanced")
	guicontrol,, stats_advanced, %advanced%
	show_gui2()
return

delete_bookmark:
	file_recycle2(file_path_csv)
	file_path_csv := ""
	iniwrite, %file_path_csv%, %a_temp%\compile_data.ini, stored_data, file_path_csv_saved
	stats := info2("all")
	guicontrol,, stats_data, %stats%
	advanced := info2("advanced")
	guicontrol,, stats_advanced, %advanced%
	previous_time := ""
	Menu, LoadMenu, Rename, 1&, Bookmarks: <not loaded>
	update_menus2()
	show_gui2()
return

saveto_change:
	file_directory_old := file_directory
	FileSelectFolder, folder_path, , 3, Select a folder
	if (folder_path) {
		file_directory := folder_path
		file_directory := file_directoryfix2(file_directory)
		update_file_directory := 1
		iniwrite, %file_directory%, %a_temp%\compile_data.ini, stored_data, file_directory
		stats := info2("all")
		guicontrol,, stats_data, %stats%
		advanced := info2("advanced")
		guicontrol,, stats_advanced, %advanced%
		update_menus2()
		Menu, SaveToMenu, rename, 2&, Current: %file_directory%
	}
return

load_video:	
	old_file_path_csv := file_path_csv
	FileSelectFile, select_file_path, 3,, Select a video file,(Video Files (*.3gp;*.3g2;*.3gpp;*.asf;*.avi;*.divx;*.dv;*.evo;*.f4v;*.flv;*.m2ts;*.m4v;*.mkv;*.mov;*.mp4;*.mpeg;*.mpg;*.mts;*.ogm;*.ogv;*.qt;*.rm;*.rmvb;*.swf;*.ts;*.vob;*.webm;*.wmv)|*.3gp;*.3g2;*.3gpp;*.asf;*.avi;*.divx;*.dv;*.evo;*.f4v;*.flv;*.m2ts;*.m4v;*.mkv;*.mov;*.mp4;*.mpeg;*.mpg;*.mts;*.ogm;*.ogv;*.qt;*.rm;*.rmvb;*.swf;*.ts;*.vob;*.webm;*.wmv|All Files (*.*)|*.*)
	if (select_file_path) {
		g_manual_load_in_progress := 1
		load_video := 1
		file_path := select_file_path
		iniwrite, %file_path%, %a_temp%\compile_data.ini, stored_data, file_path_saved
		splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
		run, %file_path%
		stats := info2("all")
		guicontrol,, stats_data, %stats%
		advanced := info2("advanced")
		guicontrol,, stats_advanced, %advanced%
		Menu, LoadMenu, Rename, 6&, Video: %file_path%
		file_path_csv_check := check_matching_csv(file_path)
		if fileexist(file_path_csv_check) {
			file_path_csv := file_path_csv_check
			Menu, LoadMenu, Rename, 1&, Bookmarks: %file_path_csv%
			show_gui2()
		} 
		Menu, LoadMenu, Rename, 6&, Video: %file_path%
		Menu, LoadMenu, Rename, 1&, Bookmarks: %file_path_csv%
		update_menus2()
        SetTimer, ResetVideoLoadFlag, -1000
		gui, font, cwhite
		guicontrol, font, videolength
		guicontrol, , videolength, Video Length:
		loop { 
			title := mpc_getSource2()
			if instr(title, ".")
				break
		}
		wait(1)
		time_total := mpc_getDuration2()
		trim(time_total)
		gui, font, cYellow
		guicontrol, font, timetotal
		guicontrol, , timetotal, %time_total%
		guicontrol, move, timetotal, x+125
	} else
		load_video := 0
return

ResetVideoLoadFlag:
    load_video_flag := 0
return

delete_video:
	file_recycle2(file_path)
	stats := info2("all")
	guicontrol,, stats_data, %stats%
	advanced := info2("advanced")
	guicontrol,, stats_advanced, %advanced%
	Menu, LoadMenu, rename, 6&, Video: <not loaded>
	GuiControl, , VideoLength
	GuiControl, , TimeTotal
	file_path := ""
	update_menus2()
	show_gui2()
return

merge_split:
	iniread, file_path, %a_temp%\compile_data.ini, stored_data, file_path
	if (update_file_directory = 1)
		iniread, file_directory, %a_temp%\compile_data.ini, stored_data, file_directory
	if (file_path_new != "") {
		file_path := file_path_new
		splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
		if !regexmatch(file_directory, "imO).+\\$")
			file_directory := file_directory . "\"
		file_extension_dot := "." . file_extension
	}
	if (file_path_csv_new != "")
		file_path_csv := file_path_csv_new
	if (ffmpeg_time = "")
		ffmpeg_time := ffmpeg_time2(file_path_csv)
	if (bookmark_status = "complete" or merge_split_selected = 1) {
		if (a_thismenuitem = "Split selected" or a_thismenuitem = "Merge selected")
			ffmpeg_time := ffmpeg_time_selected
		if (a_thismenuitem = "Merge all" or a_thismenuitem = "Merge selected")
			loop_count := 1
		else if (a_thismenuitem = "Split all" or a_thismenuitem = "Split selected") {
			loop_count := ""
			count := 0
			pos := 1
			while pos := RegExMatch(ffmpeg_time, "imO)(\d{2}:\d{2}:\d{2})\s(\d{2}:\d{2}:\d{2})", match, pos) {
				loop_count++
				pos += StrLen(match[0])
			}
		}
		if !regexmatch(file_directory, "imO).+\\$")
			file_directory := file_directory . "\"
		old_file_path := file_path
		old_csv_path := file_path_csv
		Loop, %loop_count% {
			if (a_thismenuitem = "Split all" or a_thismenuitem = "Split selected") {
				
					
				file_path_create := " " . file_path . " " . file_directory . file_name . "[cs" . a_index . "]" . file_extension_dot . " "
				if regexmatch(ffmpeg_time, "imO)(\d+:\d+:\d+\s\d+:\d+:\d+)", pairs) {
					new_ffmpeg_time := pairs[0]
					ffmpeg_time := regexreplace(ffmpeg_time, "imO)" . pairs[0])
					run2(a_temp . "\compile_merge.bat" . file_path_create . new_ffmpeg_time)
				}
			}
			if (a_thismenuitem = "Merge all" or a_thismenuitem = "Merge selected") {
				
					
				file_path_create := " " . file_path . " " . file_directory . file_name . "[done]" . file_extension_dot . " "		
				if RegExMatch(ffmpeg_time,"imO)(\d{2}:\d{2}:\d{2})\s\1",pairs) 
					ffmpeg_time := RegExReplace(ffmpeg_time,"imO)(\d{2}:\d{2}:\d{2})\s\1","")
				if (transition_effect != "None" and transition_effect != "") {
					pairs := ParsePairsFromString2(ffmpeg_time)
					output := file_directory . file_name . "[done]" . file_extension_dot
					ok := ffmpeg_transition2(file_path, output, ffmpeg_time, transition_effect, transition_duration)
				} else
					run2(a_temp . "\compile_merge.bat" . file_path_create . ffmpeg_time)
			}
			window_waitExist2("cmd.exe")
			window_id := window_id2("cmd.exe")
			window_waitClose2("ahk_id " . window_id)
		}
		msgbox, 4, , Delete source video and csv file?
		ifmsgbox Yes
			{
			file_recycle2(old_file_path)
			file_recycle2(old_csv_path)
			file_path_csv := ""
			file_path := ""
			global_delete := 1
			reload
			Menu, LoadMenu, Rename, 1&, Bookmarks: <not loaded>
			menu, loadmenu, rename, 6&, Video: <not loaded>
			update_menus2()
			mpc_close2()
		}
		ifmsgbox No
			{
			global_delete := 0
		}
	} else {
		if (bookmark_status = "incomplete")
			msg2("You cannot split or merge files without completing the last bookmark.")
		return
	}
	iniwrite, ERROR, %a_temp%\compile_data.ini, stored_data, time_saved
	winactivate, ahk_class MediaPlayerClassicW
	bookmark_status := ""
	bookmark_started := ""
	data := ""
	ffmpeg_time := ""
	ffmpeg_time_last := ""
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
	if (load_bookmark = "yes")
		file_path_csv := select_file_path_csv
	iniwrite, 1, %a_temp%\compile_data.ini, stored_data, bookmark_removed
	if regexmatch(ffmpeg_time, "imO)(.+)(\d{2}\:\d{2}\:\d{2})$", ffmpeg_remove_time)
		ffmpeg_time := ffmpeg_remove_time[1]
	if (file_path_csv != "") {
		if instr(csv_lastline2(file_path_csv), "Bookmark")
			last_line := regexreplace(csv_lastline2(file_path_csv), "imO)(\d+\,)(\d+\,Bookmark\d+)", "$2")
		else
			last_line := regexreplace(csv_lastline2(file_path_csv), "imO)(\d+\,)", "$1")
		if (csv_linecount2(file_path_csv) = 1 and regexmatch(last_line, "imO)^(\d+),$")) {
			Menu, LoadMenu, Rename, 1&, Bookmarks: <not loaded>
			file_recycle2(file_path_csv)
			file_path_csv := ""
			update_menus2()
		} else
			file_write2(file_path_csv, strreplace(csv_data2(file_path_csv), last_line), "w")
	}
	loop, parse	, % data, `n	
		{
		if regexmatch(a_loopfield, "imO)(\d+)\,(\d+)\,Bookmark(\d+)", line_time)
			button%a_index% := a_loopfield
		else
			button%a_index% := ""
	}
	IniRead, erased, %a_temp%\compile_data.ini, stored_data, bookmark_removed
	show_gui2()
return

clear:
	file_recycle2(file_path_csv)
	file_path_csv := ""
	clear_gui2()
	update_menus2()
return

reset:
	reload
return


checkbox:
	line_count := csv_linecount2(file_path_csv)
	ffmpeg_time_selected := ""
	count := 0
	loop, %line_count% {
		count++
		if (a_index = 1)
			duration := 0
		guicontrolget, state, , checkbox%a_index%
		if (state = 1) {
			checkbox_time := radiobox%a_index%
			if regexmatch(checkbox_time, "imO)(\d+)\,(\d+)\,Bookmark(" . count . ")", line_time) {
				time1 := line_time[1]
				time2 := line_time[2]
				time3 := line_time[3]
				duration += (time2 - time1)
				ffmpeg_time_selected .= time_secToLong2(time1) . " " . time_secToLong2(time2) . " "
			}
		}
	}
	edited_duration := edit_duration2(file_path_csv)
	selected_duration := time_secToAlt2(edited_duration)
	wait2(1)
	total_time := mpc_getDuration2()
	if (total_time != 0 or total_time != "")
		time_total := time_longToAlt2(total_time)
    if (duration > 0) {
		total_duration := time_secToAlt2(duration)
		if (file_path != "")
			GuiControl, text, EditedDuration, %total_duration% / %time_total%	
		else 
			GuiControl, text, EditedDuration, %total_duration%
		if (merge_split_selected = "")
			merge_split_selected := 1
	} else {
		ffmpeg_time_selected := ""
		if (file_path != "") 
			GuiControl, text, EditedDuration, %selected_duration% / %time_total%
		else 
			GuiControl, text, EditedDuration, %selected_duration%
		if (merge_split_selected = 1) 
			merge_split_selected := ""
	}
	update_menus2()
return

radio:
	matched_index := a_guicontrol
	if (update_file_directory = 1)
		iniread, file_directory, %a_temp%\compile_data.ini, stored_data, file_directory
    if regexmatch(%a_guicontrol%, "imO)(\d+)\,(\d+)\,Bookmark(\d+)", checked_time) {
        compare1 := checked_time[0]
		loop, parse, % data, `n
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
    checked_ffmpeg_time := time_secToLong2(checked_time[1]) . " " . time_secToLong2(checked_time[2])
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
    run2(A_Temp . "\compile_merge.bat " . file_path_create . " " . checked_ffmpeg_time)
	window_waitclose2("cmd.exe")
    if not IsObject(radiobox_index)
        radiobox_index := []
    radiobox_index.Push(matched_index)
	for index, value in radiobox_index
		GuiControl, , radiobox%value%, 1
	winactivate, ahk_class MediaPlayerClassicW
	update_menus2()
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
	Gui, Submit
	Menu, HotkeyMenu, Rename, 2&, Current: %SelectedHotkey%
	if (selectedHotkey = activeHotkey)
		return
	Hotkey, IfWinActive, ahk_class MediaPlayerClassicW
	Hotkey, %activeHotkey%, main, Off
	activehotkey := selectedhotkey
	Hotkey, %activeHotkey%, main, On
	Hotkey, IfWinActive
	iniWrite, %activeHotkey%, %A_Temp%\compile_data.ini, settings, activeHotkey
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
	Edit
return

view_variables:
	listvars
return


