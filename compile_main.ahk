main:
	file_path := mpc_getSource2()
	current_seconds := mpc_getTime2("seconds")
	previous_time := current_seconds
	iniwrite, %current_seconds%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, latest_time
	if (current_seconds = 0 or current_seconds = 00)
		current_seconds := 1
	total_time := mpc_getDuration2()
	previous_time := current_seconds
	set_time := current_time
	bookmark_started := "yes"
	file_path := file_info2(file_path)
	if !window_exist("cmd.exe")
		iniwrite, %file_path%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, previous_file_path
	file_path := folder_rename2(file_path)
	file_path_csv := file_create2(current_seconds)
	file_path := file_rename2(file_path)
	show_gui2()
	update_menus2()
return

add_history2:
    menu, historyMenu, deleteall
    menu, quickhistorymenu, deleteall
    Menu, quickhistorymenu, Add, 5, history_number_maximum
    Menu, quickhistorymenu, Icon, 5, %A_AppData%\MPC-HC Video Editor\icons\dash.ico, 1
    Menu, quickhistorymenu, Add, 10, history_number_maximum
    Menu, quickhistorymenu, Icon, 10, %A_AppData%\MPC-HC Video Editor\icons\dash.ico, 1
    Menu, quickhistorymenu, Add, 15, history_number_maximum
    Menu, quickhistorymenu, Icon, 15, %A_AppData%\MPC-HC Video Editor\icons\dash.ico, 1
    Menu, quickhistorymenu, Add, 20, history_number_maximum
    Menu, quickhistorymenu, Icon, 20, %A_AppData%\MPC-HC Video Editor\icons\dash.ico, 1
    Menu, historyMenu, Add, Maximum to show, :quickhistorymenu
    menu, historyMenu, icon, Maximum to show, %A_AppData%\MPC-HC Video Editor\icons\test2.ico, 1
    Menu, historyMenu, Add, Clear history, clear_history
    menu, historyMenu, icon, Clear history, %A_AppData%\MPC-HC Video Editor\icons\clear.ico, 1
    menu, historymenu, add,  _________________, no_action
    iniread, history_number_maximum, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_number_maximum
    if (history_number_maximum = "" or history_number_maximum = 0 or history_number_maximum = "ERROR") {
        history_number_maximum := 5
        menu, quickhistorymenu, check, 5
        iniwrite, 5, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_number_maximum
    } else
        menu, quickhistorymenu, check, %history_number_maximum%
    iniread, history_number_actual, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_number_actual
    if (history_number_actual = "" or history_number_actual = "ERROR" or history_number_actual = 0 or file_path = "Media Player Classic Home Cinema") {
        delete_no_history := 1
        Menu, historyMenu, Add, <no history>, no_action
        menu, historyMenu, icon, <no history>, %A_AppData%\MPC-HC Video Editor\icons\question.ico, 1
        iniwrite, 0, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_number_actual
    } else {
        loop, %history_number_actual% {
            IniRead, history_video_path, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_video%A_Index%
            if (history_video_path != "" && history_video_path != "ERROR") {
				Menu, historyMenu, Add, %history_video_path%, history_video_action
				if !fileexist(history_video_path)
					menu, historymenu, disable, %history_video_path%
                menu, historyMenu, icon, %history_video_path%, %A_AppData%\MPC-HC Video Editor\icons\%a_index%.ico, 1
            }
        }
    }
return

add_history:
	if (file_path = "Media Player Classic Home Cinema")
		file_path := ""
	else {
		IniRead, history_number_actual, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_number_actual, 0
		IniRead, history_number_maximum, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_number_maximum, 5
		if (history_number_actual = "ERROR")
			history_number_actual := 0
		if (history_number_maximum = "ERROR")
			history_number_maximum := 5
		error := 0
		loop, %history_number_actual% {
			IniRead, existing_video, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_video%a_index%
			if (existing_video != "ERROR" && existing_video = file_path) {
				error := 1
				break
			}
		}
		if (error != 1) {
			new_number := history_number_actual + 1
			if (new_number > history_number_maximum) {
				loop, %history_number_maximum% {
					old_index := a_index + 1
					if (old_index <= history_number_maximum) {
						IniRead, old_path, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_video%old_index%
						IniWrite, %old_path%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_video%a_index%
					}
				}
				IniWrite, %file_path%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_video%history_number_maximum%
				IniWrite, %history_number_maximum%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_number_actual
			} else {
				IniWrite, %file_path%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_video%new_number%
				IniWrite, %new_number%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_number_actual
			}
			gosub, add_history2
		}
	}
return

history_number_maximum:
    IniRead, history_number_actual, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_number_actual, 0
    IniRead, old_history_number_maximum, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_number_maximum, 5
	if (history_number_actual = "ERROR")
        history_number_actual := 0
    if (old_history_number_maximum = "ERROR")
        old_history_number_maximum := 5
    new_maximum := A_ThisMenuItem
    IniWrite, %new_maximum%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_number_maximum
    if (new_maximum = 5)
        number := 1
    else if (new_maximum = 10)
        number := 2
    else if (new_maximum = 15)
        number := 3
    else if (new_maximum = 20)
        number := 4
    loop, 4 {
        if (A_Index != number)
            Menu, quickhistorymenu, Uncheck, %A_Index%&
        else
            Menu, quickhistorymenu, Check, %A_Index%&
    }
    if (new_maximum < old_history_number_maximum && history_number_actual > new_maximum) {
        delete_count := history_number_actual - new_maximum
        remaining := new_maximum
        loop, %delete_count%
            IniDelete, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_video%A_Index%
        count := 0
        loop, %remaining% {
            old_index := delete_count + A_Index
            IniRead, value, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_video%old_index%, ERROR
            if (value != "ERROR" && value != "") {
                IniWrite, %value%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_video%A_Index%
                IniDelete, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_video%old_index%
                count++
            }
        }
        IniWrite, %new_maximum%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_number_actual
        gosub, add_history2
    }
    update_menus2()
return


history_video_action:
	menu_item := a_thismenuitem
	menu_item := regexreplace(a_thismenuitem, "imO)(\d+)\:\s")
	if fileexist(menu_item) {
		msgbox, 4, , Play the video?
		ifmsgbox, yes
			{
			run2(menu_item)
		}
		ifmsgbox, no
			{
			run2("explorer.exe /select, " . menu_item . "")
		}
	} else
		msg2("File no longer exists.")
return

clear_history:
	iniread, history_number, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_number_actual
	file_array := []
	loop %history_number% {
		iniread, get_file_path, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_video%a_index%
		file_array.push(get_file_path)
	}
	loop, % file_array.length() {
		file := file_array[a_index]
		if (delete_no_history = 1) {
			delete_no_history := 0
			menu, historymenu, delete, <no history>
		}
		if !instr(file,"ERROR")
			menu, historymenu, delete, %file%
		inidelete, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_video%a_index%
	}
	iniwrite, 0, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_number_actual
	menu, historymenu, add, <no history>, no_action
	menu, historyMenu, icon, <no history>, %A_AppData%\MPC-HC Video Editor\icons\question.ico, 1
	menu, quickhistorymenu, check, 5
	delete_no_history := 1
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
			file_path_check := check_matching_video2(file_path_csv)
			if fileexist(file_path_check) {
				file_path := file_path_check
				run, %file_path%
            }
            update_menus2()
        } else if regexmatch(extension, "imO)(mp4|avi|wmv|mov|flv|mpg|mpeg|ts|f4v|mkv)") {
            g_manual_load_in_progress := 1
            file_path := dropped_file_path
            run, %file_path%
			csv_check := check_matching_csv2(file_path)
			if (file_path_csv != csv_check) {
				if fileexist(csv_check) {
					file_path_csv := csv_check
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

open_file:
	if instr(a_thismenuitem, "csv")
		file := strreplace(a_thismenuitem,"Bookmarks: ")
	else
		file := strreplace(a_thismenuitem,"Video: ")
	run2("explorer.exe /select, " . file . "")  
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
		menu_item := p
		Menu, savetomenu, delete, %menu_item%
	}
	IniWrite, %new_number%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_number
	loop, %new_number%
		IniWrite, % remaining[A_Index], %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_path%A_Index%
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
			Menu, savetomenu, Add, %shortcut_path%, shortcut_actions
			Menu, savetomenu, icon, %shortcut_path%, %A_AppData%\MPC-HC Video Editor\icons\%a_index%.ico, 1
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
		Menu, savetomenu, Add, %file_directory%, shortcut_actions
		Menu, savetomenu, icon, %file_directory%, %A_AppData%\MPC-HC Video Editor\icons\%shortcut_number%.ico, 1
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
			Menu, savetomenu, Add, %shortcut_path%, shortcut_actions
			Menu, savetomenu, icon, %shortcut_path%, %A_AppData%\MPC-HC Video Editor\icons\%shortcut_number%.ico, 1
		}
		stats := info2("all")
		guicontrol,, stats_data, %stats%
		advanced := info2("advanced")
		guicontrol,, stats_advanced, %advanced%
		update_menus2()
	}
return

shortcut_actions:
	iniwrite, %a_thismenuitem%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, file_directory
	if (a_thismenuitem = a_desktop "\")
		menu, savetomenu, check, 7&
	else
		menu, savetomenu, uncheck, %a_desktop%\
	menu_item := a_thismenuitem
	iniread, shortcut_number, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_number
	if (shortcut_number = "")
		shortcut_number := 0
	loop, %shortcut_number% {
		iniread, shortcut_path, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_path%a_index%
		if (shortcut_path != "") {
			sp := shortcut_path
			if (SubStr(sp, StrLen(sp), 1) != "\")
				sp := sp . "\"
			im := a_thismenuitem
			if (SubStr(im, StrLen(im), 1) != "\")
				im := im . "\"
			if (menu_item = a_desktop "\") {
				Menu, savetomenu, check, 7&
				Menu, savetomenu, rename, 2&, Current: %menu_item%
				file_directory := a_desktop "\"
			}
			if (im = sp) {			
				Menu, savetomenu, check, %im%
				menu, savetomenu, rename, 2&, Current: %menu_item%
				file_directory := menu_item
				iniwrite, %file_directory%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, file_directory
			} else {
				menu, savetomenu, uncheck, %sp%
			}
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
	ffmpeg_time_undo_bookmark := ffmpeg_time
	if (load_bookmark = "yes")
		file_path_csv := select_file_path_csv
	iniwrite, 1, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, bookmark_removed
	if regexmatch(ffmpeg_time_undo_bookmark, "imO)(.+)(\d{2}\:\d{2}\:\d{2})$", ffmpeg_remove_time)
		ffmpeg_time_undo_bookmark := ffmpeg_remove_time[1]
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
	timeCheck1 := ""
	show_gui2()
return

clear_bookmarks:
	file_recycle2(file_path_csv)
	file_path_csv := ""
	ffmpeg_time := ""
	show_gui2()
	update_menus2()
return

unload_bookmark:
	file_path_csv := ""
	transition_effect := "None"
	gui, font, cRed
	guicontrol, font, edittime
	guicontrol, , edittime, Bookmarks <not loaded>
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
		file_path_check := check_matching_video2(file_path_csv)
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
		guicontrol, show, editedduration
		guicontrol, move, editedduration, x95
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
	gosub, clear_bookmarks
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
		if (file_directory != a_desktop)
			menu, savetomenu, uncheck, 7&
	}
	iniread, shortcut_number, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_number
	menu, savetomenu, uncheck, 7&
	Loop %shortcut_number% {
		iniread, shortcut_path, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_path%a_index%
		if (shortcut_path = file_directory)
			menu, savetomenu, check, %shortcut_path%
		else {
			menu, savetomenu, uncheck, %shortcut_path%
			menu, savetomenu, uncheck, 7&
		}
	}
return

load_video:	
	old_file_path_csv := file_path_csv
	FileSelectFile, select_file_path, 3,, Select a video file,(Video Files (*.3gp;*.3g2;*.3gpp;*.asf;*.avi;*.divx;*.dv;*.evo;*.f4v;*.flv;*.m2ts;*.m4v;*.mkv;*.mov;*.mp4;*.mpeg;*.mpg;*.mts;*.ogm;*.ogv;*.qt;*.rm;*.rmvb;*.swf;*.ts;*.vob;*.webm;*.wmv)|*.3gp;*.3g2;*.3gpp;*.asf;*.avi;*.divx;*.dv;*.evo;*.f4v;*.flv;*.m2ts;*.m4v;*.mkv;*.mov;*.mp4;*.mpeg;*.mpg;*.mts;*.ogm;*.ogv;*.qt;*.rm;*.rmvb;*.swf;*.ts;*.vob;*.webm;*.wmv|All Files (*.*)|*.*)
	if (select_file_path) {
		g_manual_load_in_progress := 1
		load_video := 1
		file_path := select_file_path
		splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
		run, %file_path%
		stats := info2("all")
		guicontrol,, stats_data, %stats%
		advanced := info2("advanced")
		guicontrol,, stats_advanced, %advanced%
		Menu, LoadMenu, Rename, 7&, Video: %file_path%
		file_path_csv_check := check_matching_csv2(file_path)
		if fileexist(file_path_csv_check) {
			file_path_csv := file_path_csv_check
			Menu, LoadMenu, Rename, 1&, Bookmarks: %file_path_csv%
			show_gui2()
		} 
		wait2(500)
		Menu, LoadMenu, Rename, 7&, Video: %file_path%
		if (delete_no_history = 1) {
			delete_no_history := 0
			menu, historymenu, delete, <no history>
		}
		gosub, add_history
		update_menus2()
		Loop {
			file_path := window_title2("ahk_class MediaPlayerClassicW")
			if !instr(file_path, "Media Player Classic")
				break
		}
		last_known_video_path := file_path
	} else
		load_video := 0
return

unload_video:
	file_path := ""
	mpc_close2()
	show_gui2()
	update_menus2()
return

delete_video:
	file_recycle2(file_path)
	stats := info2("all")
	guicontrol,, stats_data, %stats%
	advanced := info2("advanced")
	guicontrol,, stats_advanced, %advanced%
	Menu, LoadMenu, rename, 7&, Video: <not loaded>
	file_path := ""
	update_menus2()
	show_gui2()
return

ResetVideoLoadFlag:
    load_video_flag := 0
return

merge_split:
	ffmpeg_time_merge_split := ffmpeg_time
	iniwrite, %file_path%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, file_path
	message := csv_repair2(file_path_csv)
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
	iniwrite, %file_path%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, file_path
	iniwrite, %file_path_csv%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, csv_path
	if (ffmpeg_time_merge_split = "")
		ffmpeg_time_merge_split := ffmpeg_time2(file_path_csv)
	if (bookmark_status = "complete" or merge_split_selected = 1) {
		if (a_thismenuitem = "Split selected" or a_thismenuitem = "Merge selected")
			ffmpeg_time_merge_split := ffmpeg_time_selected
		if (a_thismenuitem = "Merge all" or a_thismenuitem = "Merge selected")
			loop_count := 1
		else if (a_thismenuitem = "Split all" or a_thismenuitem = "Split selected") {
			loop_count := ""
			count := 0
			pos := 1
			while pos := RegExMatch(ffmpeg_time_merge_split, "imO)(\d{2}:\d{2}:\d{2})\s(\d{2}:\d{2}:\d{2})", match, pos) {
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
			if (a_thismenuitem = "Split all" or a_thismenuitem = "Split selected")
				gosub, split
			else if (a_thismenuitem = "Merge all" or a_thismenuitem = "Merge selected") {
				gosub, merge
				if (merge = 1) {
					Loop {
						if (clipboard != "")
							break
					}
					captured_time := clipboard
					if regexmatch(captured_time, "imO)(\d+\:\d+\:\d+)", get_time) {
						get_time := get_time[1]
						get_time := time_longToSec2(get_time)
						iniread, editedduration_save, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, editedduration
						if (get_time != editedduration_save) {
							msgbox, 4, Error, Not all the cuts were merged or split correctly. Try again?
							ifmsgbox, yes
								{
								reload
								if (a_thismenuitem = "Merge All" or a_thismenuitem = "Merge selected")
									gosub, merge
								else if (a_thismenuitem = "Split All" or a_thismenuitem = "Split selected")
									gosub, split
							}
							ifmsgbox, no
								{
								return
							}
						} else {
							msg2("Success!")
							loop {
								if !process_exist("cmd.exe")
									break
								else {
									process, close, cmd.exe
									cmd := 1
								}
							}
						}
					}
				}
			}
		}
		cmd := ""
		if (split = 1) {
			Gui, Font, cGreen
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
		} else if (merge = 1) {
			Loop, %times% {
				Gui, Font, cGreen
				GuiControl, Font, line_num%a_index%
				GuiControl, Font, bracketleft%a_index%
				GuiControl, Font, bracketright%a_index%
				GuiControl, Font, dash%a_index%
				GuiControl, Font, time_split1_%a_index%
				GuiControl, Font, time_split2_%a_index%
				GuiControl, Font, parenthesisleft%a_index%
				GuiControl, Font, parenthesisright%a_index%
				GuiControl, Font, durationtime%a_index%
				Gui, Font, cDefault
			}
		}
		msgbox, 4, , Delete source video and csv file?
		ifmsgbox Yes
			{
			iniread, previous_file_path, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, previous_file_path
			iniread, get_csv_path, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, csv_path
			wait2(500)
			window_activate2("ahk_class MediaPlayerClassicW")
			wait2(500)
			check := mpc_getsource2()
			if (check = previous_file_path) {
				send2("{shift}{escape}","down")
				wait2(500)
			}
			file_recycle2(previous_file_path)
			file_recycle2(get_csv_path)
			file_path_csv := ""
			file_path := ""
			global_delete := 1
			Menu, LoadMenu, Rename, 1&, Bookmarks: <not loaded>
			menu, loadmenu, rename, 7&, Video: <not loaded>
			iniwrite, ERROR, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, time_saved
		}
	} else {
		if (bookmark_status = "incomplete") {
			msg2("You cannot split or merge files without completing the last bookmark.")
			return
		}	
	}
	if (check = previous_file_path)
		reload
return

split:
	ffmpeg_time_split := ffmpeg_time
	split := 1
	get_dir := GetMenuItemText2("savetomenu", 2)
	if regexmatch(get_dir, "imO)Current\:\s(.+)", get_dir) {
		dir := get_dir[1]
		if !instr(dir,file_path)
			file_directory := dir
	}
	file_path_create := " " . file_path . " " . file_directory . file_name . "[cs" . a_index . "]" . file_extension_dot . " "
	if regexmatch(ffmpeg_time_split, "imO)(\d+:\d+:\d+\s\d+:\d+:\d+)", pairs) {
		new_ffmpeg_time := pairs[0]
		ffmpeg_time_split := regexreplace(ffmpeg_time_split, "imO)" . pairs[0])
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
		run2(A_AppData . "\MPC-HC Video Editor\compile_merge.bat" . file_path_create . new_ffmpeg_time)
	}
return

merge:
	ffmpeg_time_merge := ffmpeg_time
	merge := 1
	get_dir := GetMenuItemText2("savetomenu", 2)
	if regexmatch(get_dir, "imO)Current\:\s(.+)", get_dir) {
		dir := get_dir[1]
		if !instr(dir,file_path)
			file_directory := dir
	}
	file_path_create := " " . file_path . " " . file_directory . file_name . "[done]" . file_extension_dot . " "		
	if RegExMatch(ffmpeg_time_merge,"imO)(\d{2}:\d{2}:\d{2})\s\1",pairs) 
		ffmpeg_time_merge := RegExReplace(ffmpeg_time_merge,"imO)(\d{2}:\d{2}:\d{2})\s\1","")
	if (transition_effect != "None" and transition_effect != "") {
		pairs := ParsePairsFromString2(ffmpeg_time_merge)
		output := file_directory . file_name . "[done]" . file_extension_dot
		ok := ffmpeg_transition2(file_path, output, ffmpeg_time_merge, transition_effect, transition_duration)
	} else {
		times := csv_linecount2(file_path_csv)
		Loop, %times% {
			Gui, Font, cFFA500
			GuiControl, Font, line_num%a_index%
			GuiControl, Font, bracketleft%a_index%
			GuiControl, Font, bracketright%a_index%
			GuiControl, Font, dash%a_index%
			GuiControl, Font, time_split1_%a_index%
			GuiControl, Font, time_split2_%a_index%
			GuiControl, Font, parenthesisleft%a_index%
			GuiControl, Font, parenthesisright%a_index%
			GuiControl, Font, durationtime%a_index%
			Gui, Font, cDefault
		}
		clipboard := ""
		check_path := file_directory . file_name . "[done]" . file_extension_dot
		if fileexist(check_path) {
			msgbox, 4, , This file already exists. Overwrite it?`n`n%check_path%
			ifmsgbox no
				{
				return
			}
		}
		run2(A_AppData . "\MPC-HC Video Editor\compile_merge.bat" . file_path_create . ffmpeg_time_merge)
		guicontrolget, EditedDuration, , EditedDuration
		EditedDuration:= time_altToSec2(EditedDuration)
		iniwrite, %EditedDuration%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, EditedDuration
		controlgettext, get, static3, ahk_class MediaPlayerClassicW
		if regexmatch(get, "imO)(\w+)x(\w+)\s(.+)\sfps", stats) {
			height := stats[1]
			width := stats[2]
			fps := stats[3]
		}
		preset_factor := 1.0
		baseline_perf := 5.4
		res_scale := ((width*height) / (1920.0*1080.0)) ** 1.1
		duration_scale := (EditedDuration / 190.0) ** 0.05
		encode_time := (EditedDuration * (fps/30.0) * res_scale * duration_scale * preset_factor) / baseline_perf
		encode_time := number_roundwhole2(encode_time)
		SleepDelay := 350
		loop_count := Round((encode_time * 1000) / SleepDelay)
		sb_setprogress(0, 4, "range0-100")
		Loop, %loop_count% {
			progress := Round((A_Index / loop_count) * 100)
			sb_setprogress(progress, 4)
			 ; calculate remaining time in seconds
			elapsed := (A_Index / loop_count) * encode_time
			remaining := Round(encode_time - elapsed)
			if (remaining < 0)
				remaining := 0
			sb_settext(remaining, 3)
			Sleep, %SleepDelay%
		}
	}
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
	get_dir := GetMenuItemText2("savetomenu", 3)
	if regexmatch(get_dir, "imO)Quick save\:\s(.+)", get_dir) {
		dir := get_dir[1]
		file_directory := dir
	}
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
    file_path_check := file_directory . file_name . "[cs]" . file_extension_dot
    if fileexist(file_path_check) {
        while fileexist(file_path_check) {
            if regexmatch(file_path_check, "imO).+(\[cs(\d+)\]).+", cs_match)
                new_num := cs_match[2] + 1
			else
				new_num := 1
            file_path_check := file_directory . file_name . "[cs" . new_num . "]" . file_extension_dot
        }
    } else
		file_path_check := file_directory . file_name . "[cs]" . file_extension_dot
	file_path_create := file_path . " " . file_path_check
    clipboard := A_AppData . "\MPC-HC Video Editor\compile_merge.bat " . file_path_create . " " . checked_ffmpeg_time
    run2(A_AppData . "\MPC-HC Video Editor\compile_merge.bat " . file_path_create . " " . checked_ffmpeg_time)
    window_waitclose2("cmd.exe")
    if not IsObject(radiobox_index)
        radiobox_index := []
    radiobox_index.Push(matched_index)
    for index, value in radiobox_index
        GuiControl, , radiobox%value%, 1
    winactivate, ahk_class MediaPlayerClassicW
    update_menus2()
return

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
			file_path_check := check_matching_video2(file_path_csv)
			if (file_path) {
				file_path_csv := check_matching_csv2(file_path)
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
	wait2(1)
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

convert_files:
	count := 0
	convert_files := ""
	select_file_path := ""
	existing_files := ""
	files_exist := ""
	Loop {
		FileSelectFile, select_file_path, MS,, Select a video file,Video Files (*.3gp;*.3g2;*.3gpp;*.asf;*.avi;*.divx;*.dv;*.evo;*.f4v;*.flv;*.m2ts;*.m4v;*.mkv;*.mov;*.mp4;*.mpeg;*.mpg;*.mts;*.ogm;*.ogv;*.qt;*.rm;*.rmvb;*.swf;*.ts;*.vob;*.webm;*.wmv;*.3gp;*.3g2;*.3gpp;*.asf;*.avi;*.divx;*.dv;*.evo;*.f4v;*.flv;*.m2ts;*.m4v;*.mkv;*.mov;*.mp4;*.mpeg;*.mpg;*.mts;*.ogm;*.ogv;*.qt;*.rm;*.rmvb;*.swf;*.ts;*.vob;*.webm;*.wmv|All Files (*.*)|*.*)
		wait2(500)
		if (a_index = 1) {
			if instr(select_file_path,"`n") {
				loop, parse, % select_file_path, `n
					{
					if (a_index = 1) {
						select_file_dir := a_loopfield
						if !regexmatch(select_file_dir, "imO).+\\$")
							select_file_dir .= "\"
					} else {
						count := a_index - 1
						convert_files .= "#" count ". " select_file_dir . a_loopfield "`n"
						multiple := 1
					}
				}
				break
			} else
				multiple := 0
		}
		if (multiple = 0) {
			if (select_file_path) {
				convert_files .= "#" a_index ". " select_file_path "`n"
				count++
			} else
				break
		}
	}
	if (count = 0)
		return
	count := 0
	msgbox, 4, , Convert these files to mp4?`n`n%convert_files%
	ifmsgbox Yes
		{
		loop, parse, % convert_files, `n
			{	
			if (a_loopfield != "") {
				file := strreplace(a_loopfield, "#" a_index . ". ")
				SplitPath, file, , , extension
				file := strreplace(file,extension,"mp4")
				if fileexist(file) {
					files_exist := 1
					existing_files .= "#" a_index ". " file "`n"
				} else
					files_exist := 0
			}
		}
		if (files_exist = 1) {
			msgbox, 4, , These files already exist. Overwrite them?`n`n%existing_files%
			ifmsgbox No
				{
				return
			}
		}			
		loop, parse, % convert_files, `n
			{
			if (a_loopfield != "") {
				file := strreplace(a_loopfield, "#" a_index . ". ")
				file_path_create = "C:\Users\Chris\AppData\Roaming\MPC-HC Video Editor\compile_convert.bat" "%file%"
				run2(file_path_create)
				window_waitexist2("cmd.exe")
				window_waitclose2("cmd.exe")
			}
		}
		wait2(2)
		msgbox, 4, , Delete source video(s)?
		ifmsgbox Yes
			{
			loop, parse, % convert_files, `n
				{
				if (a_loopfield != "") {
					file := strreplace(a_loopfield, "#" a_index . ". ")
					file_recycle2(file)
				}
			}
		}
		ifmsgbox no
			{
			convert_files := ""
			return
		}
	}
	ifmsgbox no
		{
		convert_files := ""
		return
	}
	convert_files := ""
	reload
return

merge_files:
	merge_files := ""
	error := ""
	count := ""
	select_file_path := ""
	multiple := ""
	Loop {
		FileSelectFile, select_file_path, MS,, Select a video file,(Video Files (*.3gp;*.3g2;*.3gpp;*.asf;*.avi;*.divx;*.dv;*.evo;*.f4v;*.flv;*.m2ts;*.m4v;*.mkv;*.mov;*.mp4;*.mpeg;*.mpg;*.mts;*.ogm;*.ogv;*.qt;*.rm;*.rmvb;*.swf;*.ts;*.vob;*.webm;*.wmv)|*.3gp;*.3g2;*.3gpp;*.asf;*.avi;*.divx;*.dv;*.evo;*.f4v;*.flv;*.m2ts;*.m4v;*.mkv;*.mov;*.mp4;*.mpeg;*.mpg;*.mts;*.ogm;*.ogv;*.qt;*.rm;*.rmvb;*.swf;*.ts;*.vob;*.webm;*.wmv|All Files (*.*)|*.*)
		loop, parse, % select_file_path, `n
			{
			if (a_index = 1) {
				file_dir := a_loopfield
				break
			}
		}
		if !regexmatch(file_dir, "imO).+\\$")
			file_dir .= "\"
		if (a_index = 1) {
			if instr(select_file_path,"`n") {
				multiple := 1
				loop, parse, % select_file_path, `n
					{
					if (a_index = 1) {
						select_file_dir := a_loopfield
						if !regexmatch(select_file_dir, "imO).+\\$")
							select_file_dir .= "\"
					} else {
						num := a_index - 1
						merge_files .= select_file_dir . a_loopfield "`n"
						latest_file := select_file_dir . a_loopfield
					}
				}
				break
			} else
				multiple := 0
		}
		if (multiple = 0) {
			if (select_file_path) {
				merge_files .= select_file_path "`n"
				latest_file := select_file_path
				count++
			} else
				break
		}
	}
	SplitPath, latest_file, , , extension
	if (multiple = 0 and count < 2) {
		msg2("You must select more than 1 file.")
		return
	} else if (num = 1) {
		msg2("You must select more than 1 file.")
		return
	}		
	msgbox, 4, , Merge these files in this order?`n`n%merge_files%
	ifmsgbox Yes
		{
		if regexmatch(latest_file, "imO).+\\(.+)", file)
			latest_file := file[1]
		InputBox, UserInput, Save as, Enter the filename, , 200, 120,x ,y , , , %latest_file%
		FileSelectFolder, select_folder_path, 3,, Select a save location
		if (select_folder_path) {
			file_directory_chosen := select_folder_path "\"
			if fileexist(file_directory_chosen . userinput) {
				msgbox, 4, , This file already exists. Overwrite it?`n`n%file_dir%%userinput%
				ifmsgbox no
					{
					return
				}
				ifmsgbox, yes
					{
					count := 0
					loop, parse, % merge_files, `n
						{
						if (a_loopfield != "")
							count++
					}
					FFPROBE_PATH := A_AppData . "\MPC-HC Video Editor\ffprobe.exe"
					temp_dur := A_Temp . "\ffprobe_dur.txt"
					if !FileExist(FFPROBE_PATH) {
						MsgBox, 16, Error, ffprobe not found at %FFPROBE_PATH%. Duration check skipped.
						total_duration := "Unknown"
					} else {
						total_duration := 0
						successful_files := 0
						Loop, Parse, % merge_files, `n
							{
							file := a_loopfield
							if (file != "") {
								FileDelete, %temp_dur%
								RunWait, cmd /c ""%FFPROBE_PATH%" -v quiet -print_format json -show_entries format=duration "%file%" > "%temp_dur%"", , Hide UseErrorLevel
								if (ErrorLevel = 0) {
									FileRead, ffprobe_output, %temp_dur%
									if (ffprobe_output != "") {
										RegExMatch(ffprobe_output, "i)""duration""\s*:\s*""?(\d+(?:\.\d+)?)""?", dur_match)
										if (dur_match1 != "") {
											dur_sec := dur_match1 + 0
											total_duration += dur_sec
											successful_files++
										}
									}
								}
								FileDelete, %temp_dur%
							}
						}
						if (successful_files = 0)
							formatted_total := "Unknown"
						else {
							total_h := Floor(total_duration / 3600)
							total_m := Floor(Mod(total_duration, 3600) / 60)
							total_s := Mod(total_duration, 60)
							formatted_total := Format("{:02d}:{:02d}:{:02d}", total_h, total_m, Round(total_s))
						}
					}
					files := ""
					loop, parse, % merge_files, `n
						{
						if (a_loopfield != "") {
							file := a_loopfield
							if (a_index = 1)
								files .= """" . file . """"
							else if (a_index = 2)
								files .= " " . """" . file . """"
							else
								files .= " " . """" . file . """" . " "
						}
					}
					formatted_total := time_longToAlt2(formatted_total)
					Gui, Font, cWhite
					guicontrol, font, edittime
					guicontrol, , edittime, Edit time:
					Gui, Font, cFFA500
					guicontrol, font, EditedDuration
					guicontrol, , EditedDuration, %formatted_total%
					guicontrol, show, editedduration
					Gui, Font, cWhite
					file_path_create = "%A_AppData%\MPC-HC Video Editor\compile_merge_files.bat" "%file_directory%%userinput%" %files%
					clipboard := file_path_create
					run2(file_path_create)
					files := ""
					merge_files := ""
					wait2(2)
					loop
						{
						if !window_exist2("cmd.exe")
							break
					}
					msgbox, 4, , Delete source videos?
					ifmsgbox Yes
						{
						loop, parse, % merge_files, `n
							{
							if (a_loopfield != "") {
								file := a_loopfield
								file_recycle2(file)
							}
						}
						files := ""
						merge_files := ""
					}
					ifmsgbox No
						{
						files := ""
						merge_files := ""
					}
				}
			}
		}
		ifmsgbox No
			{
			files := ""
			merge_files := ""
		}
		reload
	}
return

status:
	statusbargettext, status, 2, MPC-HC Video Editor v2.1
	run2("explorer.exe /select, " . status . "")
return

add_quick:
	FileSelectFolder, select_quick_folder, 3,, Select a save location
	if (select_quick_folder) {
		iniwrite, %select_quick_folder%\, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, quicksave
		menu, savetomenu, rename, 3&, Quick save: %select_quick_folder%\
	}
return
