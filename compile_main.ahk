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

GuiDropFiles:
	Loop, Parse, A_GuiEvent, `n
		{
        dropped_file_path := A_LoopField
        SplitPath, dropped_file_path, , , extension       
        if (extension = "csv") {
            g_manual_load_in_progress := 1
            file_path_csv := dropped_file_path
            show_gui2()            
			file_path_check := check_matching_video(file_path_csv)
			if fileexist(file_path_check) {
				file_path := file_path_check
				run, %file_path%
            }
            update_menus2()
        } else if regexmatch(extension, "imO)(mp4|avi|wmv|mov|flv|mpg|mpeg|ts|f4v|mkv)") {
            g_manual_load_in_progress := 1
            file_path := dropped_file_path
            run, %file_path%
			csv_check := check_matching_csv(file_path)
			if (file_path_csv != csv_check) {
				if fileexist(csv_check) {
					file_path_csv := file_path_csv_check
					show_gui2()
				} else
					update_menus2()
				break
			}
        }
	}
return

delete_shortcut:
	WinGetPos, current_x, current_y, current_w, current_h, MPC-HC Video Editor v2.1
	Gui, DeleteShortcutGUI:New, +Owner +Border -Resize +ToolWindow +AlwaysOnTop +LastFound
	Gui, DeleteShortcutGUI:Color, 000000
	Gui, DeleteShortcutGUI:Font, s10 c000000, Segoe UI
	iniread, shortcut_number, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_number
	if (shortcut_number = "" or shortcut_number = 0)
		shortcut_number := 0
	Gui, DeleteShortcutGUI:Add, ListView, Checked r%shortcut_number% w500 vDeleteShortcutList hwndhLV, Select shortcuts to remove:
	SendMessage, 0x1001, 0, 0x00FFFFFF,, ahk_id %hLV%
	SendMessage, 0x1024, 0, 0x000000,, ahk_id %hLV%
	SendMessage, 0x1026, 0, 0x00FFFFFF,, ahk_id %hLV%
	loop, %shortcut_number% {
		iniread, shortcut_path, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_path%a_index%
		if (shortcut_path != "") {
			if (SubStr(shortcut_path, StrLen(shortcut_path), 1) != "")
				shortcut_path := shortcut_path . ""
			LV_Add("Checked", shortcut_path)
		}
	}
	Gui, DeleteShortcutGUI:Font, s10 cFFFFFF, Segoe UI
	Gui, DeleteShortcutGUI:Add, Button, xm w100 gconfirm_delete default, Confirm
	Gui, DeleteShortcutGUI:Add, Button, x+10 w100 gcancel_delete, Cancel
	current_h += 143
	Gui, MainGui:+Owner +OwnDialogs
	Gui, DeleteShortcutGUI:Show, x%current_x% y%current_h% w500, Save to > Shortcuts > Delete...
return

confirm_delete:
	remove_arr := []
	idx := LV_GetNext(0, "Checked")
	while (idx) {
		LV_GetText(path, idx)
		if (SubStr(path, StrLen(path), 1) != "\")
			path := path . "\"
		remove_arr.Push(path)
		LV_Delete(idx)
		idx := LV_GetNext(0, "Checked")
	}
	if (!IsObject(remove_arr) or remove_arr.Length() = 0) {
		Gui, DeleteShortcutGUI:Destroy
		return
	}
	IniRead, old_count, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_number
	if (old_count = "" or old_count = 0) {
		Gui, DeleteShortcutGUI:Destroy
		return
	}
	existing := []
	loop, %old_count% {
		IniRead, p, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_path%A_Index%
		if (p != "") {
			if (SubStr(p, StrLen(p), 1) != "\")
				p := p . "\"
			existing.Push(p)
		}
	}
	remaining := []
	for index, p in existing {
		found := 0
		for k, rp in remove_arr {
			if (p = rp) {
				found := 1
				break
			}
		}
		if (!found)
			remaining.Push(p)
	}
	new_number := remaining.Length()
	loop % old_count {
		p := existing[A_Index]
		menu_item := A_Index ": " p
		Menu, savetomenu, delete, %menu_item%
	}
	IniWrite, %new_number%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_number
	loop, %new_number% {
		IniWrite, % remaining[A_Index], %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_path%A_Index%
	}
	if (old_count > new_number) {
		loop, % old_count - new_number {
			i := new_number + A_Index
			IniDelete, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_path%i%
		}
	}
	update_menus2()
	Gui, DeleteShortcutGUI:Destroy
return

cancel_delete:
	Gui, DeleteShortcutGUI:Destroy
return

load_shortcuts:
	iniread, shortcut_number, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_number
	if (shortcut_number = "")
		shortcut_number := 0
	if (shortcut_number > 0) {
		loop, %shortcut_number% {
			iniread, shortcut_path, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_path%a_index%
			Menu, savetomenu, Add, %a_index%: %shortcut_path%, shortcut_actions
		}
		update_menus2()
	}
return

add_current:
	iniread, shortcut_add, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_add
	if (shortcut_add = 0)
		iniwrite, 1, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_add
	iniread, shortcut_number, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_number
	if (shortcut_number = "")
		shortcut_number := 0
	found_dup := 0
	loop, %shortcut_number% {
		iniread, p, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_path%A_Index%
		if (p != "") {
			if (SubStr(p, StrLen(p), 1) != "\")
				p := p . "\"
			if (p = file_directory) {
				found_dup := 1
				break
			}
		}
	}
	if (!found_dup) {
		shortcut_number++
		iniwrite, %shortcut_number%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_number
		iniwrite, %file_directory%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_path%shortcut_number%
		Menu, savetomenu, Add, %shortcut_number%: %file_directory%, shortcut_actions
	}
	stats := info2("all")
	guicontrol,, stats_data, %stats%
	advanced := info2("advanced")
	guicontrol,, stats_advanced, %advanced%
	update_menus2()
return

add_shortcut:
	FileSelectFolder, shortcut_path, , 3, Select a folder
	if (shortcut_path) {
		if (SubStr(shortcut_path, StrLen(shortcut_path), 1) != "\")
			shortcut_path := shortcut_path . "\"
		if (shortcut_path = a_desktop . "\") {
			MsgBox, Desktop is already added as a hardcoded shortcut.
			return
		}
		Menu, savetomenu, UseErrorLevel
		Menu, savetomenu, Uncheck, %shortcut_path%
		if (ErrorLevel = 0) {
			return
		}
		iniread, shortcut_add, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_add
		if (shortcut_add = 0)
			iniwrite, 1, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_add
		iniread, shortcut_number, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_number
		if (shortcut_number = "")
			shortcut_number := 0
		found_dup := 0
		loop, %shortcut_number% {
			iniread, p, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_path%A_Index%
			if (p != "") {
				if (SubStr(p, StrLen(p), 1) != "\")
					p := p . "\"
				if (p = shortcut_path) {
					found_dup := 1
					break
				}
			}
		}
		if (!found_dup) {
			shortcut_number++
			iniwrite, %shortcut_number%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_number
			iniwrite, %shortcut_path%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_path%shortcut_number%
			Menu, savetomenu, Add, %shortcut_number%: %shortcut_path%, shortcut_actions
		}
		stats := info2("all")
		guicontrol,, stats_data, %stats%
		advanced := info2("advanced")
		guicontrol,, stats_advanced, %advanced%
		update_menus2()
	}
return

shortcut_actions:
	if regexmatch(a_thismenuitem, "imO)(\d+\:\s)(.+)", remove_number)
		menu_item := remove_number[2]
	else
		menu_item := a_thismenuitem
	if (desktop_checked = 1 and !instr(a_thismenuitem, a_desktop))
		Menu, savetomenu, uncheck, %a_desktop%\	
	iniread, shortcut_number, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_number
	if (shortcut_number = "")
		shortcut_number := 0
	loop, %shortcut_number% {
		iniread, shortcut_path, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_path%a_index%
		if (shortcut_path != "") {
			sp := a_index ": " shortcut_path
			if (SubStr(sp, StrLen(sp), 1) != "\")
				sp := sp . "\"
			im := a_thismenuitem
			if (SubStr(im, StrLen(im), 1) != "\")
				im := im . "\"
			if (menu_item = a_desktop "\") {
				Menu, savetomenu, check, %a_desktop%\
				Menu, savetomenu, rename, 2&, Current: %menu_item%
				file_directory := a_desktop "\"
			}
			if (im = sp) {
				Menu, savetomenu, check, %im%
				menu, savetomenu, rename, 2&, Current: %menu_item%
				Menu, savetomenu, uncheck, %a_desktop%\
				file_directory := menu_item
			} else
				menu, savetomenu, uncheck, %sp%
		}
	}
	stats := info2("all")
	guicontrol,, stats_data, %stats%
	advanced := info2("advanced")
	guicontrol,, stats_advanced, %advanced%
	update_menus2()
return

TR_SelectType:
	transition_effect := a_thismenuitem
	if (a_thismenuitem != "None") {
		menu, effectsmenu, check, %transition_effect%
		menu, effectsmenu, uncheck, None
		menu, videomenu, enable, Effect duration
		menu, videomenu, enable, 5&
		menu, durationmenu, check, 2s
	} else {
		menu, effectsmenu, uncheck, Fadeblack
		menu, effectsmenu, check, None
		menu, videomenu, disable, Effect duration
		menu, videomenu, disable, 5&
	}
	menu, videomenu, rename, 2&, Current: %transition_effect%
return

DUR_SelectType:
	transition_duration := a_thismenuitem
	if regexmatch(transition_duration, "imO)(\d+)s", time) {
		time_sec := time[1]
		Loop, 5 {
		if (a_index != time_sec)
			menu, durationmenu, uncheck, %a_index%s
		else
			menu, durationmenu, check, %time_sec%s
		}
	}
	menu, videomenu, rename, 5&, Current: %transition_duration%
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
			guicontrol,  hide, stats_advanced
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

undo_bookmark:
	if (load_bookmark = "yes")
		file_path_csv := select_file_path_csv
	iniwrite, 1, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, bookmark_removed
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
	IniRead, erased, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, bookmark_removed
	show_gui2()
return

clear_bookmarks:
	file_recycle2(file_path_csv)
	file_path_csv := ""
	clear_gui2()
	show_gui2()
	update_menus2()
return

unload_bookmark:
	file_path_csv := ""
	transition_effect := "None"
	show_gui2()
	update_menus2()
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
			menu, loadmenu, rename, 7&, Video: %file_path%
		}
		Menu, LoadMenu, Rename, 1&, Bookmarks: %file_path_csv%
		show_gui2()
		update_menus2()
		edited_duration := edit_duration2(file_path_csv)
		edited_duration := time_secToAlt2(edited_duration)
		gui, font, cWhite
		guicontrol, font, edittime
		guicontrol, , edittime, Edit time: 
		gui, font, cFFA500
		guicontrol, font, EditedDuration
		guicontrol, , EditedDuration, %edited_duration%
		Gui, Font, cWhite
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
	iniwrite, %file_path_csv%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, file_path_csv_saved
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
		iniwrite, %file_directory%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, file_directory
		stats := info2("all")
		guicontrol,, stats_data, %stats%
		advanced := info2("advanced")
		guicontrol,, stats_advanced, %advanced%
		update_menus2()
		Menu, SaveToMenu, rename, 2&, Current: %file_directory%
	}
	iniread, shortcut_number, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_number
	menu, savetomenu, uncheck, %a_desktop%\
	Loop %shortcut_number% {
		iniread, shortcut_path, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_path%a_index%
		if (shortcut_path = file_directory)
			menu, savetomenu, check, %a_index%: %shortcut_path%
		else
			menu, savetomenu, uncheck, %a_index%: %shortcut_path%
	}
return

load_video:	
	old_file_path_csv := file_path_csv
	FileSelectFile, select_file_path, 3,, Select a video file,(Video Files (*.3gp;*.3g2;*.3gpp;*.asf;*.avi;*.divx;*.dv;*.evo;*.f4v;*.flv;*.m2ts;*.m4v;*.mkv;*.mov;*.mp4;*.mpeg;*.mpg;*.mts;*.ogm;*.ogv;*.qt;*.rm;*.rmvb;*.swf;*.ts;*.vob;*.webm;*.wmv)|*.3gp;*.3g2;*.3gpp;*.asf;*.avi;*.divx;*.dv;*.evo;*.f4v;*.flv;*.m2ts;*.m4v;*.mkv;*.mov;*.mp4;*.mpeg;*.mpg;*.mts;*.ogm;*.ogv;*.qt;*.rm;*.rmvb;*.swf;*.ts;*.vob;*.webm;*.wmv|All Files (*.*)|*.*)
	if (select_file_path) {
		g_manual_load_in_progress := 1
		load_video := 1
		file_path := select_file_path
		iniwrite, %file_path%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, file_path_saved
		splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
		run, %file_path%
		stats := info2("all")
		guicontrol,, stats_data, %stats%
		advanced := info2("advanced")
		guicontrol,, stats_advanced, %advanced%
		Menu, LoadMenu, Rename, 7&, Video: %file_path%
		file_path_csv_check := check_matching_csv(file_path)
		if fileexist(file_path_csv_check) {
			file_path_csv := file_path_csv_check
			Menu, LoadMenu, Rename, 1&, Bookmarks: %file_path_csv%
			show_gui2()
		} 
		Menu, LoadMenu, Rename, 7&, Video: %file_path%
		update_menus2()
        SetTimer, ResetVideoLoadFlag, -1000
		loop { 
			title := mpc_getSource2()
			if instr(title, ".")
				break
		}
		wait(1)
		time_total := mpc_getDuration2()
		time_total := time_longToAlt2(time_total)
		gui, font, cWhite
		guicontrol, font, videolength
		GuiControl, , videolength, Video length:
		gui, font, cYellow
		guicontrol, font, timetotal
		GuiControl, , TimeTotal, %time_total%
		guicontrol, move, timetotal, x+123
		last_known_video_path := file_path
	} else
		load_video := 0
return

unload_video:
	file_path := ""
	mpc_close2()
	show_gui2()
	update_menus2()
	gui, font, cred
	guicontrol, font, videolength
	guicontrol, , videolength, No video loaded
	GuiControl, , TimeTotal
	guicontrol, move, timetotal, x+140
return

delete_video:
	file_recycle2(file_path)
	stats := info2("all")
	guicontrol,, stats_data, %stats%
	advanced := info2("advanced")
	guicontrol,, stats_advanced, %advanced%
	Menu, LoadMenu, rename, 7&, Video: <not loaded>
	GuiControl, , VideoLength
	GuiControl, , TimeTotal
	file_path := ""
	update_menus2()
	show_gui2()
return

ResetVideoLoadFlag:
    load_video_flag := 0
return

merge_split:
	if (update_file_directory = 1)
		iniread, file_directory, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, file_directory
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
			count := a_index
			if (a_thismenuitem = "Split all" or a_thismenuitem = "Split selected") {
				split := 1
				file_path_create := " " . file_path . " " . file_directory . file_name . "[cs" . a_index . "]" . file_extension_dot . " "
				if regexmatch(ffmpeg_time, "imO)(\d+:\d+:\d+\s\d+:\d+:\d+)", pairs) {
					new_ffmpeg_time := pairs[0]
					ffmpeg_time := regexreplace(ffmpeg_time, "imO)" . pairs[0])
					Gui, Font, cFFA500
					GuiControl, Font, line_num%count%
					GuiControl, Font, bracketleft%count%
					GuiControl, Font, bracketright%count%
					GuiControl, Font, dash%count%
					GuiControl, Font, time_split1_%count%
					GuiControl, Font, time_split2_%count%
					GuiControl, Font, parenthesisleft%count%
					GuiControl, Font, parenthesisright%count%
					GuiControl, Font, durationtime%count%
					Gui, Font, cDefault
					run2(a_temp . "\compile_merge.bat" . file_path_create . new_ffmpeg_time)
				}
			} else if (a_thismenuitem = "Merge all" or a_thismenuitem = "Merge selected") {
				merge := 1
				file_path_create := " " . file_path . " " . file_directory . file_name . "[done]" . file_extension_dot . " "		
				if RegExMatch(ffmpeg_time,"imO)(\d{2}:\d{2}:\d{2})\s\1",pairs) 
					ffmpeg_time := RegExReplace(ffmpeg_time,"imO)(\d{2}:\d{2}:\d{2})\s\1","")
				if (transition_effect != "None" and transition_effect != "") {
					pairs := ParsePairsFromString2(ffmpeg_time)
					output := file_directory . file_name . "[done]" . file_extension_dot
					ok := ffmpeg_transition2(file_path, output, ffmpeg_time, transition_effect, transition_duration)
				} else {
					times := csv_linecount(file_path_csv)
					Loop, %times% {
						Gui, Font, cFFA500  ; Set the drawing color for the font
						GuiControl, Font, line_num%a_index% ; Apply the new font color to the specified checkbox
						GuiControl, Font, bracketleft%a_index% ; Apply the new font color to the specified checkbox
						GuiControl, Font, bracketright%a_index% ; Apply the new font color to the specified checkbox
						GuiControl, Font, dash%a_index% ; Apply the new font color to the specified checkbox
						GuiControl, Font, time_split1_%a_index% ; Apply the new font color to the specified checkbox
						GuiControl, Font, time_split2_%a_index% ; Apply the new font color to the specified checkbox
						GuiControl, Font, parenthesisleft%a_index% ; Apply the new font color to the specified checkbox
						GuiControl, Font, parenthesisright%a_index% ; Apply the new font color to the specified checkbox
						GuiControl, Font, durationtime%a_index% ; Apply the new font color to the specified checkbox
						Gui, Font, cDefault ; Reset the color back to default for any other GUI operations
					}
					run2(a_temp . "\compile_merge.bat" . file_path_create . ffmpeg_time)
					wait(1)
				}
			}
			window_waitExist2("cmd.exe")
			window_waitClose2("cmd.exe")
			if (split = 1) {
				Gui, Font, cGreen  ; Set the drawing color for the font
				GuiControl, Font, line_num%count% ; Apply the new font color to the specified checkbox
				GuiControl, Font, bracketleft%count% ; Apply the new font color to the specified checkbox
				GuiControl, Font, bracketright%count% ; Apply the new font color to the specified checkbox
				GuiControl, Font, dash%count% ; Apply the new font color to the specified checkbox
				GuiControl, Font, time_split1_%count% ; Apply the new font color to the specified checkbox
				GuiControl, Font, time_split2_%count% ; Apply the new font color to the specified checkbox
				GuiControl, Font, parenthesisleft%count% ; Apply the new font color to the specified checkbox
				GuiControl, Font, parenthesisright%count% ; Apply the new font color to the specified checkbox
				GuiControl, Font, durationtime%count% ; Apply the new font color to the specified checkbox
				Gui, Font, cDefault ; Reset the color back to default for any other GUI operations
			} else if (merge = 1) {
				Loop, %times% {
					Gui, Font, cGreen  ; Set the drawing color for the font
					GuiControl, Font, line_num%a_index% ; Apply the new font color to the specified checkbox
					GuiControl, Font, bracketleft%a_index% ; Apply the new font color to the specified checkbox
					GuiControl, Font, bracketright%a_index% ; Apply the new font color to the specified checkbox
					GuiControl, Font, dash%a_index% ; Apply the new font color to the specified checkbox
					GuiControl, Font, time_split1_%a_index% ; Apply the new font color to the specified checkbox
					GuiControl, Font, time_split2_%a_index% ; Apply the new font color to the specified checkbox
					GuiControl, Font, parenthesisleft%a_index% ; Apply the new font color to the specified checkbox
					GuiControl, Font, parenthesisright%a_index% ; Apply the new font color to the specified checkbox
					GuiControl, Font, durationtime%a_index% ; Apply the new font color to the specified checkbox
					Gui, Font, cDefault ; Reset the color back to default for any other GUI operations
				}
			}
		}
		wait(1)
		Loop, %times%
			gosub, reset_timestamps
		msgbox, 4, , Delete source video and csv file?
		ifmsgbox Yes
			{
			wait(500)
			winactivate, ahk_class MediaPlayerClassicW
			send("{Shift}{escape}", "down")
			file_recycle2(file_path)
			file_recycle2(file_path_csv)
			file_path_csv := ""
			file_path := ""
			global_delete := 1
			Menu, LoadMenu, Rename, 1&, Bookmarks: <not loaded>
			menu, loadmenu, rename, 7&, Video: <not loaded>
			iniwrite, "", %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, time_saved
			winactivate, ahk_class MediaPlayerClassicW
			reload
		}
		ifmsgbox No
			{
		}
	} else {
		if (bookmark_status = "incomplete")
			msg2("You cannot split or merge files without completing the last bookmark.")
		return
	}
return

reset_timestamps:
	Gui, Font, cWhite
	GuiControl, Font, bracketleft%a_index% 
	Gui, Font, cGreen
	GuiControl, Font, line_num%a_index%
	Gui, Font, cWhite
	GuiControl, Font, bracketright%a_index%
	Gui, Font, cBlue
	GuiControl, Font, time_split1_%a_index%
	Gui, Font, cWhite
	GuiControl, Font, dash%a_index%
	Gui, Font, cBlue
	GuiControl, Font, time_split2_%a_index%
	Gui, Font, cWhite
	GuiControl, Font, parenthesisleft%a_index%
	Gui, Font, cYellow
	GuiControl, Font, durationtime%a_index%
	Gui, Font, cWhite
	GuiControl, Font, parenthesisright%a_index%
return

reset_everything:
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
			GuiControl, text, EditedDuration, %total_duration%	
		else 
			GuiControl, text, EditedDuration, %total_duration%
		if (merge_split_selected = "")
			merge_split_selected := 1
	} else {
		ffmpeg_time_selected := ""
		if (file_path != "") 
			GuiControl, text, EditedDuration, %selected_duration%
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
		iniread, file_directory, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, file_directory
	if !regexmatch(file_directory, "imO).+\\$")
		file_directory := file_directory . "\"
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

enter_time:
	InputBox, UserInput, Time, Enter a time or range of time, , 200, 120
	if (ErrorLevel = 0) {
		time_seconds1 := ""
		time_seconds2 := ""
		if regexmatch(UserInput, "imO)(\d+|\d+:\d+|\d+:\d+:\d+)\s\-\s(\d+|\d+:\d+|\d+:\d+:\d+)$", get_time)
			time_seconds1 := get_time[1], time_seconds2 := get_time[2]
		else if regexmatch(UserInput, "imO)(\d+|\d+:\d+|\d+:\d+:\d+)$", get_time)
			time_seconds1 := get_time[1]
		else {
			msg2("Invalid time. Try again.")
			return
		}
		time_seconds1 := time_shortToSec2(time_seconds1)
		time_seconds2 := time_shortToSec2(time_seconds2)
		time_total2 := time_altToSec2(time_total)
		if (time_total2 != "" AND time_total2 != 0) {
			if (time_seconds1 >= time_total2 and time_seconds1 != "" OR time_seconds2 >= time_total2 and time_seconds2 != "") {
				msg2("Time cannot be greater than the length of the video.")
				return
			}
		}
		if fileexist(file_path_csv) {
			last_line := csv_lastline2(file_path_csv) 
			line_count := csv_linecount2(file_path_csv)
			if regexmatch(last_line, "imO)(\d+)\,(\d+),Bookmark(\d+)") {
				if (time_seconds1 != "" AND time_seconds2 != "")
					file_write2(file_path_csv, "`n" . time_seconds1 . "," . time_seconds2 . ",Bookmark" . (line_count + 1))
				else if (time_seconds1 != "" AND time_seconds2 = "")
					file_write2(file_path_csv, "`n" . time_seconds1 . ",")
			} else if regexmatch(last_line, "imO)(\d+)\,") {
				if (time_seconds1 != "" AND time_seconds2 != "") {
					msg2("You cannot enter a pair. The last bookmark is unfinished.")
					return	
				} else if (time_seconds1 != "" AND time_seconds2 = "")
					file_write2(file_path_csv, time_seconds1 . ",Bookmark" . (line_count))
			}	
		} else {
			file_path_check := check_matching_video(file_path_csv)
			if (file_path) {
				file_path_csv := check_matching_csv(file_path)
				if (time_seconds1 != "" AND time_seconds2 != "")
					file_write2(file_path_csv, time_seconds1 . "," . time_seconds2 . ",Bookmark1")
				else if (time_seconds1 != "" AND time_seconds2 = "")
					file_write2(file_path_csv, time_seconds1 . ",")
			}
		}
	show_gui2()
	stats := info2("all")
	guicontrol,, stats_data, %stats%
	advanced := info2("advanced")
	guicontrol,, stats_advanced, %advanced%
	}
return


hotkey:
	WinGetPos, current_x, current_y, current_w, current_h, MPC-HC Video Editor v2.1
	current_x := current_x + 250
	current_y := current_y + 100
	Progress, w200 fs14 x%current_x% y%current_y%, Press a hotkey now
	Loop {
		if GetKeyState("MButton") {
			hotkey := "MButton"
			break
		}
		Input, OutputVar, L1 T0.1, {AppsKey}{F1}{F2}{F3}{F4}{F5}{F6}{F7}{F8}{F9}{F10}{F11}{F12}{Left}{Right}{Up}{Down}{Home}{End}{PgUp}{PgDn}{Del}{Ins}{BS}{CapsLock}{NumLock}{PrintScreen}{Pause}{a}{b}{c}{d}{e}{f}{g}{h}{i}{j}{k}{l}{m}{n}{o}{p}{q}{r}{s}{t}{u}{v}{w}{x}{y}{z}{LShift}{LControl}{LAlt}{LWin}{MButton}
		if instr(errorlevel, "endkey") {
			key1 := strreplace(errorlevel, "endkey:")
			if (key1 = "lshift" or key1 = "lctrl" or key1 = "lalt" or key1 = "lwin") {
				if (key1 = "lshift")
					modifier := "+"
				else if (key1 = "lctrl")
					modifier := "^"
				else if (key1 = "lalt")
					modifier := "!"
				else if (key1 = "lwin")
					modifier := "#"
				Input, OutputVar, L1 T3, {AppsKey}{F1}{F2}{F3}{F4}{F5}{F6}{F7}{F8}{F9}{F10}{F11}{F12}{Left}{Right}{Up}{Down}{Home}{End}{PgUp}{PgDn}{Del}{Ins}{BS}{CapsLock}{NumLock}{PrintScreen}{Pause}{a}{b}{c}{d}{e}{f}{g}{h}{i}{j}{k}{l}{m}{n}{o}{p}{q}{r}{s}{t}{u}{v}{w}{x}{y}{z}
				if instr(errorlevel, "endkey") {
					key2 := strreplace(errorlevel, "endkey:")
					hotkey := modifier . key2
					break
				} else if (errorlevel = "Timeout") {
					msg2("Error: Failed to set keybind. Try again.")
					break
				}
			} else {
				hotkey := key1
				break
			}
		}
	}
	Progress, w200 fs14 x%current_x% y%current_y%, %hotkey%
	wait(1)
	progress, off
	Menu, HotkeyMenu, Rename, 2&, Current: %hotkey%
	if (hotkey = activeHotkey)
		return
	Hotkey, %hotkey%, main, On
	Hotkey, IfWinActive
	iniWrite, %hotkey%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, activeHotkey
return

no_action:

return

		
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

show:
	gui, show, , MPC-HC Video Editor v2.1
return

