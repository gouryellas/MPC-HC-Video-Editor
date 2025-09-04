gui_title := "MPC-HC Video Editor v2.1"

menu_add(menu, string, label) {
	menu, %menu%, add, %string%, %label%	
}
menu_icon(menu, string, icon) {
	icon .= ", 1"
	menu, %menu%, icon, %string%, %icon%
}
menu_enable(menu, string) {
	menu, %menu%, enable, %string%
	return 1
}
menu_disable(menu, string) {
	menu, %menu%, disable, %string%
	return 1
}
menu_rename(menu, string1, string2) {
	Menu, %menu%, Rename, %string1%, %string2%	
}
menu_deleteall(menu) {
	menu, %menu%, deleteall
}

mpchc_isFullPath() {
    if WinExist("ahk_class MediaPlayerClassicW")
        WinGetTitle, t, ahk_class MediaPlayerClassicW
    else if WinExist("ahk_exe mpc-hc64.exe")
        WinGetTitle, t, ahk_exe mpc-hc64.exe
    else
        return -1
    if (t = "")
        return -1
    base := RegExReplace(RegExReplace(t, " - MPC.*"), " - Media Player Classic.*")
    return RegExMatch(base, "i)^(?:[A-Z]:\\|\\\\|https?://)") ? 1 : 0
}

Menu, LoadMenu, Rename, 6&, Video: <not loaded>	

menu_show2(var) {
	if (delete_all = 1) {
		menu, savetomenu, deleteall
		menu, AddMenu, deleteall
		menu, LoadMenu, deleteall
		menu, ActionsMenu, deleteall
		menu, hotkeyMenu, deleteall
		menu, videoMenu, deleteall
		menu, statsMenu, deleteall
	}
	Menu, SaveToMenu, Add, Select..., saveto_change
	Menu, SaveToMenu, Icon, Select..., C:\Users\Chris\Desktop\ahk\photoshop\ico\Location and Other Sensors.ico, 1
	Menu, SaveToMenu, Add, Current: %file_directory%, no_action
	Menu, SaveToMenu, Icon, Current: %file_directory%, C:\Users\Chris\Desktop\ahk\photoshop\ico\Bookmarks.ico, 1
	Menu, quicksavetomenu, Add, Add..., add_shortcut	
	Menu, quicksavetomenu, Add, -----------------, no_action
	Menu, quicksavetomenu, Add, %a_desktop%\, shortcut_actions
	if (file_path) {
		splitpath, file_path, , shortcut_path
		Menu, quicksavetomenu, Add, %shortcut_path%, shortcut_actions
	}
	Menu, SaveToMenu, Add, Shortcuts, :quicksavetomenu
	Menu, SaveToMenu, Default, 2&
	Menu, AddMenu, Add, Enter a time..., enter_time
	Menu, AddMenu, Icon, Enter a time..., C:\Users\Chris\Desktop\ahk\backups\photoshop - 1.30.2022\ico\Metro\Office Apps\ICO\InfoPath alt 2.ico, 1
	Menu, LoadMenu, Add, Bookmarks: %file_path_csv%, load_bookmark
	Menu, LoadMenu, Icon, Bookmarks: %file_path_csv%, C:\Users\Chris\Desktop\ahk\photoshop\ico\Notifications.ico, 1
	Menu, LoadMenu, Add, Load..., load_bookmark 
	Menu, LoadMenu, Icon, Load...,	C:\Users\Chris\Desktop\ahk\photoshop\ico\Share.ico, 1
	Menu, LoadMenu, Add, Edit, edit_bookmark 
	Menu, LoadMenu, Icon, Edit, C:\Users\Chris\Desktop\ahk\photoshop\ico\AutoPlay.ico, 1
	Menu, LoadMenu, Add, Delete`t, delete_bookmark
	Menu, LoadMenu, Icon, Delete`t, C:\Users\Chris\Desktop\ahk\photoshop\ico\Excel alt 1.ico, 1
	Menu, LoadMenu, Add, -----------------, no_action
	Menu, LoadMenu, Add, Video: %file_path%, load_video
	Menu, LoadMenu, Icon, Video: %file_path%, C:\Users\Chris\Desktop\ahk\backups\photoshop - 1.30.2022\ico\Metro\Applications\ICO\Windows Media Player.ico, 1
	Menu, LoadMenu, Add, Load...`t, load_video 
	Menu, LoadMenu, Icon, Load...`t, C:\Users\Chris\Desktop\ahk\photoshop\ico\MS Office Upload Center.ico, 1
	Menu, LoadMenu, Add, Delete, delete_video
	Menu, LoadMenu, Icon, Delete, C:\Users\Chris\Desktop\ahk\photoshop\ico\Power - Standby.ico, 1		
	Menu, ActionsMenu, Add, Merge all, merge_split
	Menu, ActionsMenu, Icon, Merge all, C:\Users\Chris\Desktop\ahk\photoshop\ico\Aperture.ico, 1
	Menu, ActionsMenu, Add, Split all, merge_split
	Menu, ActionsMenu, Icon, Split all, C:\Users\Chris\Desktop\ahk\photoshop\ico\Screen Resolution.ico, 1
	Menu, ActionsMenu, Add, -----------------, no_action
	Menu, ActionsMenu, Add, Undo last bookmark, undo_bookmark
	Menu, ActionsMenu, Icon, Undo last bookmark, C:\Users\Chris\Desktop\ahk\backups\photoshop - 1.30.2022\ico\Metro\System Icons\ICO\Network Drive Offline.ico, 1
	Menu, ActionsMenu, Add, Clear all bookmarks, clear_bookmarks
	Menu, ActionsMenu, Icon, Clear all bookmarks, C:\Users\Chris\Desktop\ahk\backups\photoshop - 1.30.2022\ico\Metro\System Icons\ICO\Recycle Bin Full.ico, 1
	Menu, ActionsMenu, Add, Reset everything, reset_everything
	Menu, ActionsMenu, Icon, Reset everything, C:\Users\Chris\Desktop\ahk\backups\photoshop - 1.30.2022\ico\Metro\Other\ICO\Power - Shut Down.ico, 1
	Menu, ActionsMenu, Add, -----------------%a_space%, no_action
	Menu, ActionsMenu, Add, Check for errors, csv_check
	Menu, ActionsMenu, Icon, Check for errors, C:\Users\Chris\Desktop\ahk\backups\photoshop - 1.30.2022\ico\Metro\Other\ICO\Power - Lock.ico, 1	
	Menu, HotkeyMenu, Add, Select..., hotkey
	Menu, HotkeyMenu, Icon, Select..., C:\Users\Chris\Desktop\ahk\photoshop\ico\Fraps.ico, 1
	if (activehotkey = "") 
		activehotkey := "MButton"
	Menu, HotkeyMenu, Add, Current: %activehotkey%, no_action
	Menu, HotkeyMenu, Icon, Current: %activehotkey%, C:\Users\Chris\Desktop\ahk\photoshop\ico\Wikipedia alt 1.ico, 1
	Menu, HotkeyMenu, Default, 2&
	Menu, effectsmenu, Add, None, TR_SelectType
	Menu, effectsmenu, Add, Fadeblack, TR_SelectType
	Menu, durationmenu, Add, 1s, DUR_SelectType
	Menu, durationmenu, Add, 2s, DUR_SelectType
	Menu, durationmenu, Add, 3s, DUR_SelectType
	Menu, durationmenu, Add, 4s, DUR_SelectType
	Menu, durationmenu, Add, 5s, DUR_SelectType
	Menu, videoMenu, Add, Transition effect >, :effectsmenu
	Menu, videoMenu, Icon, 1&, C:\Users\Chris\Desktop\ahk\backups\photoshop - 1.30.2022\ico\Metro\Applications\ICO\Visual Studio 2012.ico, 1
	Menu, videoMenu, Add, Current: None, no_action
	Menu, videoMenu, Icon, 2&, C:\Users\Chris\Desktop\ahk\backups\photoshop - 1.30.2022\ico\Metro\Applications\ICO\Calendar.ico, 1
	Menu, videoMenu, Default, 2&
	Menu, videoMenu, Add, -----------------, no_action
	Menu, videoMenu, Add, Effect duration >, :durationmenu
	Menu, videoMenu, Icon, 4&, C:\Users\Chris\Desktop\ahk\backups\photoshop - 1.30.2022\ico\Metro\Office Apps\ICO\InfoPath.ico, 1
	Menu, videoMenu, Add, Current: 2s, no_action
	Menu, videoMenu, Icon, 5&, C:\Users\Chris\Desktop\ahk\photoshop\ico\InfoPath alt 2.ico, 1
	Menu, StatsMenu, Add, Enable, stats
	Menu, StatsMenu, Add, Advanced, stats
	Menu, StatsMenu, Disable, Advanced
	Menu, %var%, Add, &Save to, :SaveToMenu
	Menu, %var%, Add, &Add, :AddMenu
	Menu, %var%, Add, &Load/Unload, :LoadMenu
	Menu, %var%, Add, &Actions, :ActionsMenu
	Menu, %var%, Add, &Hotkey, :HotkeyMenu
	Menu, %var%, Add, &Video, :videoMenu
	Menu, %var%, Add, &Stats, :StatsMenu
	if (file_directory = "") {
		Menu, saveToMenu, Rename, 2&, Current: %a_desktop%\
		iniwrite, %a_desktop%\, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, file_directory
		file_directory := a_desktop . "\"
	}
	Gui, Menu, %var%
	delete_all := 1
}

csv_repair2(file_path_csv) {
    if !FileExist(file_path_csv)
        return "File does not exist"    
    data := file_read2(file_path_csv)
    if (data = "")
        return "File is empty"   
    data := StrReplace(data, " ", "")
    data := StrReplace(data, "`t", "")  
    lines := StrSplit(data, "`n")
    clean_lines := [] 
    for index, line in lines {
        line := Trim(line, " `t`r`n")
        if (line != "")
            clean_lines.Push(line)
    }
    fixed_lines := []
    bookmark_counter := 1
    errors_found := 0
    for index, line in clean_lines {
		if RegExMatch(line, "imO)^(\d+),((\d+),Bookmark(\d+)$)", check_normal) {
			time1 := check_normal[1]
			time2 := check_normal[3]
			if (time2_linebefore >= time1 and time2_linebefore != "") {
				get_position := index
				errors_found := 1
				if (index := get_position)
					csv_linedelete2(file_path_csv, index)
					break
			}
			time1_linebefore := check_normal[1]
			time2_linebefore := check_normal[3]
			if (time1 = time2) {
				errors_found := 1
				saved_line := time1
				csv_linedelete2(file_path_csv, index)
				csv_linewrite2(file_path_csv, saved_line, index)
				line := saved_line
				fixed_lines.Push(line)
				break
			} else {
				fixed_lines.Push(line)
				continue
			}
		}
		if RegExMatch(line, "imO)\d+,\d+,Bookmark(\d+),\d+,Bookmark(\d+)", get_num) {
			original_num := get_num[2]
			errors_found := 1
			get_position := index + 1	
			for index, line in clean_lines {
				if (index = get_position) {
					RegExMatch(line, "imO)\d+,\d+,Bookmark(\d+)", get_num_check)
					num_after_after := get_num_check[1]
					num_after_check := (num_after_after - 1)
					num_after := original_num
					get_numfix := get_num[1]            
					if (num_after_check = num_after)
						num_before := (num_after - 1)
					pattern := "(\d+,\d+,Bookmark" . num_before . ")"
					for index, line in clean_lines {
						if RegExMatch(line, "imO)" . pattern . "(.+)", split_line) {
							line_fix_before := split_line[1]
							line_fix_after := split_line[2]
						}
					}
					if RegExMatch(line_fix_before, "imO)^(\d+),(\d+),Bookmark(\d+)$", match1) {
						time1 := match1[1]
						time2 := match1[2]					
						if (time1 >= time2)
							fixed_line1 := time2 . "," . time1 . ",Bookmark" . index
						else
							fixed_line1 := line_fix_before
						fixed_lines.Push(fixed_line1)
					}
					if RegExMatch(line_fix_after, "imO)(\d+),(\d+),Bookmark(\d+)", match2) {
						time1 := match2[1]
						time2 := match2[2]
						if (time1 >= time2)
							fixed_line2 := time2 . "," . time1 . ",Bookmark" . index + 1
						else
							fixed_line2 := line_fix_after
						fixed_lines.Push(fixed_line2)
					}
					continue
				}
			}
		} else if RegExMatch(line, "imO)^(\d+),(\d+),Bookmark(\d+)$", match) {
			time1 := match[1]
			time2 := match[2]
			bookmark_num := match[3]
			if (time1 >= time2) {
				errors_found := 1
				fixed_line := time2 . "," . time1 . ",Bookmark" . index + 1
			} else
				fixed_line := time1 . "," . time2 . ",Bookmark" . index + 1
			fixed_lines.Push(fixed_line)
			continue
		} else if RegExMatch(line, "imO)^(\d+,)$", match) {
			time1 := match[1]
			errors_found := 1
			get_position := index + 1
			for index, line in clean_lines {
				if (index = get_position) {
					if RegExMatch(line, "imO)^(\d+,Bookmark\d+)$", deleteMatch) {
						saved_line := deleteMatch[1]
						csv_linedelete2(file_path_csv, get_position)
						csv_linewrite2(file_path_csv, saved_line, get_position - 1)
						fixed_line := csv_lineread2(file_path_csv, get_position - 1)
						fixed_lines.Push(fixed_line . saved_line)
						continue
					}
				}
			}
			clean_lines.Push(time1 . ",")
			continue
		}
	}
	data := ""
	for index, line in fixed_lines {
		if (line != "")
			data .= line . "`n"
	}
	file_write2(file_path_csv, data, "w")
	data := file_read2(file_path_csv)
	new_lines := StrSplit(data, "`n")
	clean_lines := [] 
	bookmark_counter := 1
	for index, line in new_lines {
		if (line != "") {
			if RegExMatch(line, "imO)(\d+),(\d+),Bookmark(\d+)", match) {
				time1 := match[1]
				time2 := match[2]
				bookmark_num := match[3]
				if (time1 >= time2) {
					errors_found := true
					fixed_line := time2 . "," . time1 . ",Bookmark" . bookmark_counter
				} else
					fixed_line := time1 . "," . time2 . ",Bookmark" . bookmark_counter
				if (bookmark_num != bookmark_counter) 
					errors_found := true
				clean_lines.Push(fixed_line)
				bookmark_counter++
				continue
			}
			if RegExMatch(line, "imO)^(\d+),(\d+)$", match) {
				time1 := match[1]
				time2 := match[2]
				if (time1 >= time2) {
					errors_found := true
					fixed_line := time2 . "," . time1 . ",Bookmark" . bookmark_counter
				} else
					fixed_line := time1 . "," . time2 . ",Bookmark" . bookmark_counter
				errors_found := true
				clean_lines.Push(fixed_line)
				bookmark_counter++
				continue
			}
			if RegExMatch(line, "imO)^(\d+)$", match) {
				time1 := match[1]
				errors_found := true
				clean_lines.Push(time1 . ",")
				continue
			}
		}
	}
	if (errors_found = 1) {
		corrected_content := ""
		for index, line in clean_lines {
			corrected_content .= line
			if (index < clean_lines.Length())
				corrected_content .= "`n"
		}
		file_write2(file_path_csv, corrected_content, "w")
		msg2("Errors were found and repaired.")
		show_gui2()
	} else
		msg2("No errors were found.")
	return message
}

show_gui2() {
	global
	if window_exist2("MPC-HC Video Editor v2.1") {
		WinGetPos, current_x, current_y, current_w, current_h, MPC-HC Video Editor v2.1
		gui_existed := 1
		Gui, Destroy
	}
	line_num := csv_linecount2(file_path_csv)

	gui, +minimizebox +border -resize +sysmenu +caption -toolwindow +DPIScale +alwaysontop +lastfound +theme
	gui +hwndmygui
	gui, margin, 0, 0
	gui, menu, test
	Gui, Color, 000000
	gui, font, bold s11 cWhite, Fira Code
		data := file_read2(file_path_csv)
		loop, parse, % data, `n
			{
			if regexmatch(a_loopfield, "imO)(\d+)\,(\d+)\,Bookmark(\d+)", line_time) {
				ui_add_radio2(, "r1 +w23 xs y+5 +gRadio +vRadiobox" . a_index)
				ui_add_checkbox2(, "+w30 x+5 yp+0 +gCheckbox +center +vCheckBox" . a_index)
				time1sec := time1 := line_time[1]
				time2sec := time2 := line_time[2]
				iniwrite, %time2%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, time_saved
				line_num := line_time[3]
				duration_time := time_secToAlt2(time2 - time1)
				bookmark_status := "complete"
				time1 := time_secToShort2(time1)
				time2 := time_secToShort2(time2)
				gui, add, text, x+ +center cWhite vbracketleft%a_index%, [
				if (time1sec > time2sec or time1sec = time2sec)
					gui, add, text, x+ +center cRed, X
				else
					gui, add, text, x+ +center cGreen vline_num%a_index%, %line_num%
				gui, add, text, x+ +center cWhite vbracketright%a_index%, ]
				gui, add, text, x+ +center cWhite, %a_space%
				if (time1sec > time2sec or time1sec = time2sec)
					gui, add, text, x+ +center cRed gtime_split vtime_split1_%a_index%, %time1%
				else
					gui, add, text, x+ +center cBlue gtime_split vtime_split1_%a_index%, %time1%
				gui, add, text, x+ +center cWhite vdash%a_index%, %a_space%-%a_space%   
				if (time1sec > time2sec or time1sec = time2sec)
					gui, add, text, x+ +center cRed  gtime_split vtime_split1_%a_index%, %time1%
				else
					gui, add, text, x+ +center cBlue gtime_split vtime_split2_%a_index%, %time2%
				gui, add, text, x+ +center cWhite, %a_space%
				gui, add, text, x+ +center cWhite vparenthesisleft%a_index%, (
				if (time1sec > time2sec)
					gui, add, text, x+ +center cRed, %duration_time%
				else if (time1sec = time2sec)
					gui, add, text, x+ +center cRed, 0s
				else
					gui, add, text, x+ +center cYellow vdurationtime%a_index%, %duration_time%
				gui, add, text, x+ +center cWhite vparenthesisright%a_index%, )			
				button%a_index% := a_loopfield
				radiobox%a_index% := button%a_index%
				radiobox_checked%a_index% := button%a_index%
			} else if regexmatch(a_loopfield, "imO)(\d+)\,", line_time) {
				line_num := csv_linecount2(file_path_csv)
				ui_add_radio2(, "r1 +w23 xs y+5 +gRadio +vRadiobox" . a_index)
				ui_add_checkbox2(, "+w30 x+5 yp+0 +gCheckbox +center +vCheckBox" . a_index)
				time1 := time_secToShort2(line_time[1])
				bookmark_status := "incomplete"
				iniwrite, %time1%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, time_saved
				gui, add, text, x+ +center cWhite, [
				gui, add, text, x+ +center cRed, X
				gui, add, text, x+ +center cWhite, ] 
				gui, add, text, x+ +center cwhite, %a_space%
				gui, add, text, x+ +center cBlue gtime_split vtime_split%a_index%, %time1%
			}
		}
		edited_duration := edit_duration2(file_path_csv)
		edited_duration := time_secToAlt2(edited_duration)
		total_time := mpc_getDuration2()
		if (total_time != 0 and total_time != "")
			time_total := time_longToAlt2(total_time)
	if !fileexist(file_path_csv)
		gui, add, text, w500 xs y+10 +left cRed vEdit , No csv loaded
	else {
		if (edited_duration != "") {
			gui, add, text, xs y+5 cWhite vEditTime +left, Edit time:
			gui, add, text, x+ +left cwhite, %a_space%
			gui, add, text, x+ +left cRed vEditedDuration w500, %edited_duration%
		}
	}
	if !fileexist(file_path) {
		gui, add, text, xs y+5 cRed vVideoLength +left, No video loaded
		gui, add, text, x+ cWhite +left, %a_space%
		gui, add, text, x+ cYellow +left vTimeTotal w500,%time_total%
	} else {
		gui, add, text, xs y+5 +left cWhite vVideoLength, Video length:
		gui, add, text, x+ +left cWhite, %a_space%
		gui, add, text, x+ +left cYellow vTimeTotal w500,%time_total%
	}
	Gui, Add, GroupBox, +left vdragdropborder w500 h150 xs
	Gui, Add, Text, w490 Center xp+5 yp+65 vdragdroptext, <drag files here to load>
	xpos := a_screenwidth - 1200
	WinGet, state, MinMax, ahk_id %MyGui%
	if (gui_existed = 1) {
		Gui, Show, NA x%current_x% y%current_y% w500, MPC-HC Video Editor v2.1
		if (state = 1)
			WinMinimize, ahk_id %MyGui%
	} else {
		Gui, Show, NA x%xpos% y140 w500, MPC-HC Video Editor v2.1
		if (state = 1)
			WinMinimize, ahk_id %MyGui%
	}
	gui, color, 000000, 000000
	for index, value in radiobox_index
		GuiControl, , %value%, 1
	if (timer = "on")
		seek_steps2()
	WM_COPYDATA := 0x004A
	WM_COPYGLOBALDATA := 0x0049
	WM_DROPFILES := 0x0233
	MSGFLT_ALLOW := 1
	DllCall("ChangeWindowMessageFilterEx", "Ptr", mygui, "UInt", 0x0233, "UInt", 1, "Ptr", 0)
	DllCall("ChangeWindowMessageFilterEx", "Ptr", mygui, "UInt", WM_COPYDATA, "UInt", MSGFLT_ALLOW, "Ptr", 0)
	DllCall("ChangeWindowMessageFilterEx", "Ptr", mygui, "UInt", WM_COPYGLOBALDATA, "UInt", MSGFLT_ALLOW, "Ptr", 0)
	DllCall("ChangeWindowMessageFilterEx", "Ptr", mygui, "UInt", WM_DROPFILES, "UInt", MSGFLT_ALLOW, "Ptr", 0)
}

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

clear_gui2() {
    global
    if window_exist2("MPC-HC Video Editor v2.1") {
        WinGetPos, current_x, current_y, current_w, current_h, MPC-HC Video Editor v2.1
        gui_existed := 1
    }
	gui, %mygui%:destroy
    gui, +owner +border -resize +maximizebox +sysmenu +caption +toolwindow +DPIScale +alwaysontop +lastfound
	gui, +hwndmygui
    gui, margin, 0, 0
    gui, menu, test
    Gui, Color, 000000
    gui, font, bold s11 cWhite, Fira Code
    xpos := a_screenwidth - 1200
    if (gui_existed = 1)
        Gui, Show, NA x%current_x% y%current_y% w500, MPC-HC Video Editor v2.1
    else
        Gui, Show, NA x%xpos% y140 w500, MPC-HC Video Editor v2.1
    gui, color, 000000, 000000
}

file_info2(file_path:="") {
	file_path := window_title2("ahk_class MediaPlayerClassicW")
	wait2(500)
	splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
	if (folder_path_set = 1) 
		file_directory := folder_path
	file_directory := file_directoryfix2(file_directory)
	file_name := string_caseLower2(file_name)
	file_extension_dot := "." . file_extension
	file_path_csv := strreplace(file_path, file_extension, "csv")
	if (load_bookmark = "yes")
		file_path_csv := select_file_path_csv	
	return file_path
}

file_rename2(file_path) {
	if !regexmatch(file_path, "imO)^MPC-BE\sx(\d+)\s\d+\.\d+\.\d+$") {
		splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
		old_file_path := file_path
		old_csv_path := strreplace(old_file_path,file_extension,"csv")
		invalidPattern := "[&\s',!\\\/@?:\.=%\*¡]"
		new_file_name := RegExReplace(file_name,invalidPattern,"-")
		new_file_path := path_join2(file_directory,new_file_name,"." . file_extension)
		if (new_file_path != old_file_path && old_file_path != "") {
			mpc_close2()
			FileMove, %old_file_path%, %new_file_path%
			file_path := new_file_path
			new_csv_path := strreplace(new_file_path,file_extension,"csv")
			if (new_csv_path != old_csv_path) {
				FileMove, %old_csv_path%, %new_csv_path%
				file_path_csv := new_csv_path
			}
			run, %file_path%
			wait2(1)
			splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
			file_directory := file_directoryfix2(file_directory)
			file_name := string_caseLower2(file_name)
			file_extension_dot := "." . file_extension
		}
		iniwrite, %file_path%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, file_path
		return file_path
	}
}

folder_rename2(file_path) {
	if !regexmatch(file_path, "imO)^MPC-BE\sx(\d+)\s\d+\.\d+\.\d+$") {
		splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
		old_folder_path := file_directory
		new_folder_path := RegExReplace(file_directory," ","-")
		if (new_folder_path != old_folder_path) {
			window_kill2("ahk_class MediaPlayerClassicW")
			wait2(2)
			FileMoveDir, %old_folder_path%, %new_folder_path%
			file_path := path_join2(new_folder_path,file_both)
			file_path_csv := strreplace(file_path,file_extension,"csv")
			run, %file_path%
			wait2(2)
			splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
			file_directory := file_directoryfix2(file_directory)
		}			
		iniwrite, %file_path%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, file_path
		return file_path
	}
}

file_time2() {
	current_time := mpc_getTime2()
	total_time := mpc_getDuration2()
	if (total_time != 0 or total_time != "")
		time_total := time_longToAlt2(total_time)
	current_seconds := mpc_getTime2("seconds")
	if (current_time = "") {
		msg2("Failed to get the current time of the video.")
		return "error"
	}
	if (current_seconds = 0) 
		current_seconds := 1
	previous_time := current_seconds
	set_time := current_time
	return current_seconds
}

file_create2(current_seconds) {
	if (data = "") {
		line_num := 1
		file_write2(file_path_csv, current_seconds . ",")
		data := file_read2(file_path_csv)
	} else {
		last_line := csv_lastline2(file_path_csv)
		line_count := csv_linecount2(file_path_csv)
		if regexmatch(last_line, "imO)(\d+)\,(\d+)\,Bookmark(\d+)", line_time) {
			file_write2(file_path_csv, "`n" . current_seconds . ",")
			data := file_read2(file_path_csv)
			line_num++
			bookmark_status := "incomplete"
		} else {
			file_write2(file_path_csv, current_seconds . ",Bookmark" . line_num)
			bookmark_status := "complete"
		}
	}
	return file_path_csv
}

update_menus2(var:="") {
	if (var != "")
		file_path_csv := var
	if (!file_path_csv and !file_path) {
		Menu, AddMenu, Disable, Enter a time...
		Menu, videoMenu, Disable, Transition effect >
		Menu, videoMenu, Disable, 2&
		Menu, videoMenu, Disable, Effect duration >
		Menu, videoMenu, Disable, 5&
		Menu, ActionsMenu, Disable, 1&
		Menu, ActionsMenu, Disable, 2&
		Menu, ActionsMenu, Disable, Undo last bookmark
		Menu, ActionsMenu, Disable, Clear all bookmarks
		Menu, ActionsMenu, Disable, Reset everything
		Menu, ActionsMenu, Disable, Check for errors
		Menu, LoadMenu, Disable, Edit
		Menu, LoadMenu, Disable, Delete`t
		Menu, LoadMenu, Disable, Delete
		Menu, LoadMenu, Rename, 1&, Bookmarks: <not loaded>
		Menu, LoadMenu, Rename, 6&, Video: <not loaded>	
		Menu, LoadMenu, Disable, 8&
		Menu, ActionsMenu, Enable, Reset everything	
	} else if (FileExist(file_path_csv) and FileExist(file_path)) {
		Menu, AddMenu, Enable, Enter a time...
		Menu, LoadMenu, Rename, 1&, Bookmarks: %file_path_csv%
		Menu, LoadMenu, Rename, 6&, Video: %file_path%
		Menu, LoadMenu, Enable, 4&
		Menu, LoadMenu, Enable, 8&
		Menu, LoadMenu, Enable, Edit
		Menu, ActionsMenu, Enable, Undo last bookmark
		Menu, ActionsMenu, Enable, Clear all bookmarks
		Menu, ActionsMenu, Enable, Check for errors
		Menu, videoMenu, Enable, Transition effect >
		Menu, videoMenu, Enable, 2&
		Menu, videoMenu, Enable, Effect duration >
		Menu, videoMenu, Enable, 5&
		if (merge_split_selected = 1) {
			Menu, ActionsMenu, rename, 1&, Merge selected
			Menu, ActionsMenu, rename, 2&, Split selected
		} else if (merge_split_selected = "") {
			Menu, ActionsMenu, rename, 1&, Merge all
			Menu, ActionsMenu, rename, 2&, Split all
		}
		Menu, ActionsMenu, Enable, 1&
		Menu, ActionsMenu, Enable, 2&
	} else {
		if FileExist(file_path_csv) {
			Menu, AddMenu, Enable, Enter a time...
			Menu, ActionsMenu, Enable, Undo last bookmark
			Menu, ActionsMenu, Enable, Clear all bookmarks
			Menu, ActionsMenu, Enable, Reset everything
			Menu, ActionsMenu, Enable, Check for errors
			Menu, LoadMenu, Enable, Edit
			Menu, LoadMenu, Enable, Delete`t
			Menu, LoadMenu, Rename, 1&, Bookmarks: %file_path_csv%
			Menu, LoadMenu, Enable, 8&
		} else {
			Menu, videoMenu, Disable, Transition effect >
			Menu, videoMenu, Disable, 2&
			Menu, videoMenu, Disable, Effect duration >
			Menu, videoMenu, Disable, 5&
			Menu, ActionsMenu, Disable, 1&
			Menu, ActionsMenu, Disable, 2&
			Menu, ActionsMenu, Disable, Undo last bookmark
			Menu, ActionsMenu, Disable, Clear all bookmarks
			Menu, ActionsMenu, Disable, Reset everything
			Menu, ActionsMenu, Disable, 8&
			Menu, LoadMenu, Disable, Edit
			Menu, LoadMenu, Disable, Delete`t
			Menu, ActionsMenu, Enable, Reset everything	
			Menu, LoadMenu, Rename, 1&, Bookmarks: <not loaded>
		}
		if FileExist(file_path) {
			Menu, LoadMenu, Enable, Delete
			Menu, AddMenu, Enable, Enter a time...
			Menu, LoadMenu, Rename, 6&, Video: %file_path%
		} else {
			Menu, AddMenu, Enable, Enter a time...
			Menu, videoMenu, Disable, Transition effect >
			Menu, videoMenu, Disable, Effect duration >
			Menu, videoMenu, Disable, 2&
			Menu, videoMenu, Disable, 5&
			Menu, ActionsMenu, Disable, 1&
			Menu, ActionsMenu, Disable, 2&
			Menu, LoadMenu, Disable, Delete
			Menu, LoadMenu, Rename, 6&, Video: <not loaded>	
		}
	}
	iniread, shortcut_number, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_number
	loop, %shortcut_number% {
		iniread, shortcut_path, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, shortcut_path%a_index%
		Menu, quicksavetomenu, Add, %shortcut_path%\, shortcut_actions
	}
}

csv_linedelete2(var, number) {
	file := FileOpen(var, "rw")
	data := file.Read()
	if instr(data, "`n")
		var := "`n"
	else
		var := "`r"
	loop, parse, % data, %var%
		{
		if (a_index != number)
			new_data .= a_loopfield
	}
	file.write(new_data)
	file.close()
	return new_data
}
csv_lineread2(file_path_csv, num) {
	file := FileOpen(file_path_csv, "rw")
	data := file.Read()
	if instr(data, "`n")
		var := "`n"
	else
		var := "`r"
	loop, parse, % data, %var%
		{
		if (a_index = num) {
			line := a_loopfield
			break
		}
	}
	return line
}

csv_linewrite2(file_path_csv, string, num) {
	file := FileOpen(file_path_csv, "rw")
	data := file.Read()
	if instr(data, "`n")
		var := "`n"
	else
		var := "`r"
	loop, parse, % data, %var%
		{
		if (a_index = num) {
			file_write2(file_path_csv, string, "a")
			break
		}
	}
}

edit_duration2(file_path_csv:="") {
	data := file_read2(file_path_csv)
	loop, parse, % data, `n	
		{
		if regexmatch(a_loopfield, "imO)(\d+)\,(\d+)\,Bookmark(\d+)", time) {
			time1 := time[1]
			time2 := time[2]
			duration%a_index% := time2 - time1
			duration_total+= duration%a_index%
		}
	}
	return duration_total
}

ffmpeg_time2(file_path_csv) {
	loop, parse, % data, `n
		{
		if regexmatch(a_loopfield, "imO)(\d+)\,(\d+)\,Bookmark(\d+)", line_time) {
			time1 := line_time[1]
			time2 := line_time[2]
			time3 := line_time[3]
			if (time3 = 1)
				ffmpeg_time := time_secToLong2(time1) . " " . time_secToLong2(time2)
			else
				ffmpeg_time .= " " . time_secToLong2(time1) . " " . time_secToLong2(time2)
		}
	}
	return ffmpeg_time
}

seek_steps2(t_time:="") {
	iniread, previous_time, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, time_saved
	if (timer = "on") {
		if !instr(previous_time, ":")
			previous_time := time_secToShort2(previous_time)
		prefix := special2(previous_time)
		get_clicked_time := previous_time
	} else {
		prefix := special2(t_time)
		get_clicked_time := t_time
	}
	get_clicked_time2 := time_shortToSec2(get_clicked_time)
	time_total2 := time_shortToSec2(time_total2)
	if (get_clicked_time2 < time_total2) {
		msg2("Warning: The last bookmark time exceeds the length of the video. `n`nLast bookmark time: " . time_shortToAlt2(get_clicked_time) . "`nVideo length: " . time_total)
		return
	}
	num := number_countdigits2(get_clicked_time)
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
	WinActivate, ahk_class MediaPlayerClassicW
	Send, ^g
	WinWaitActive, Go To...
	Controlfocus, MFCMaskedEdit1, Go To...
	sendraw % new_time
	send2("{enter}")
}	

special2(var1) {
	total_time := mpc_getDuration2("raw")
	if regexmatch(total_time, "imO)(\d{2}):(\d{2}):(\d{2})", time_seek) {
		num := number_countDigits2(var1)
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
		num := number_countDigits2(var1)
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

preserve_stats2() {
	if window_exist2(gui_title) {
		WinGetPos, current_x, current_y, current_w, current_h, %gui_title%
		gui_existed := 1
	}
	xpos := a_screenwidth - 1200
	if (stats_hidden = 0) {
		stats := info2("all")
		guicontrol, , stats_data, %stats%
		guicontrol, show, stats_data
		menu, statsmenu, check, enable
		menu, statsmenu, enable, advanced
		stats := info2("all")
        line_count := StrSplit(stats, "`n").Length()
		ui_add_edit2(stats, "xs +left +vstats_data +hscroll +w500 +r" . line_count - 1)
		first_enable_click := 1
	}
	if (advanced_hidden = 0) {
		advanced := info2("advanced")
		guicontrol, , stats_advanced, %advanced%
		guicontrol, show, stats_advanced
		menu, statsmenu, check, advanced
		ui_add_edit2(advanced, "+left +vstats_advanced +hscroll +w500 +r" . line_count - 1)
		first_advanced_click := 1
	}
	if (stats_hidden = 1) {
		guicontrol, hide, stats_data
		guicontrol, hide, stats_advanced
		menu, statsmenu, uncheck, enable
		menu, statsmenu, uncheck, advanced
	}
	if (gui_existed = 1)
		Gui, Show, NA w500 x%current_x% y%current_y%, %gui_title%
	else
		Gui, Show, NA w500 x%xpos% y140, %gui_title%
}

ParsePairsFromString2(ffmpeg_time) {
	obj := []
	if (ffmpeg_time = "" or !RegExMatch(ffmpeg_time, "imO)\d{1,2}:\d{2}:\d{2}"))
		return obj
	toks := []
	Loop, Parse, ffmpeg_time, %A_Space%
		{
		if (A_LoopField != "")
			toks.Push(A_LoopField)
	}
	Loop % Floor(toks.MaxIndex()/2)
		{
		t1 := toks[2*A_Index-1],
		t2 := toks[2*A_Index]
		s1 := HMS_To_Seconds2(t1)
		s2 := HMS_To_Seconds2(t2)
		if (s2 > s1)
			obj.Push({start:s1,end:s2,dur:(s2-s1)})
	}
	return obj
}

ffmpeg_transition2(input, output, ffmpeg_time, effect:="", duration:="") {
    tmpDir := A_Temp . "\mpcbe_temp"
    FileCreateDir, %tmpDir%
    ff := "C:\Program Files\ffmpeg\tools\ffmpeg\bin\ffmpeg.exe"

    if (input = "" || !FileExist(input)) {
        MsgBox, 16, ERROR, Source file missing:`n%src%
        return 0
    }
    pairs := ParsePairsFromString2(ffmpeg_time)
    if (pairs.MaxIndex() < 1) {
        MsgBox, 48, INFO, No valid time ranges found.
        return 0
	}
    if (duration = "")
		duration := 2
    clips := []

    Loop % pairs.MaxIndex() {
        p := pairs[A_Index]
		ss := p.start
		ee := p.end
		st := p.dur - duration
		dt := duration
		ff := "C:\Program Files\ffmpeg\tools\ffmpeg\bin\ffmpeg.exe"
		num := pairs.MaxIndex()
		v_a := num * 0.5
		if (effect = "fadeblack") {
			effect_out := "fade=t=out:st="
			effect_in := "fade=t=in:st="
		}
		if (a_index = 1) {
			cmd := ff . """" . " -hwaccel auto -y -i " . """" . input . """" . " -filter_complex [0:v]trim=start=" . ss . ":end=" . ee . ",setpts=PTS-STARTPTS," . effect_out . st . ":d=" . dt . "[v1];[0:a]atrim=start=" . ss . ":end=" . ee . ",asetpts=PTS-STARTPTS,a" . effect_out . "0:d=" . dt . "[a1];"
			media := "[v1][a1]"
		} else {
			cmd .= "[0:v]trim=start=" . ss . ":end=" . ee . ",setpts=PTS-STARTPTS," . effect_in . "0:d=" . dt . "[v" . a_index . "];[0:a]atrim=start=" . ss . ":end=" . ee . ",asetpts=PTS-STARTPTS,a" . effect_in . st . ":d=" . dt . "[a" . a_index . "];"
			media .= "[v" . a_index . "][a" . a_index . "]"
		}
		if (A_Index = num) {
			cmd .= media . "concat=n=" . num . ":v=1:a=1[v][a] -map [v] -map [a] -c:v libx264 -preset ultrafast -crf 18 -pix_fmt yuv420p -r 30 -c:a aac -ar 48000 -ac 2 -b:a 192k " . """" output . """"
		}
	}
	cmd := regexreplace(cmd, "imO)\.\d+")
	command := " comspec " /c """" cmd
	clipboard := command
	Run2(command)
}

HMS_to_sec2(t) {
	if RegExMatch(t, "^\d{1,2}:\d{2}$")
		t := "00:" . t
	if !RegExMatch(t, "^\d{2}:\d{2}:\d{2}$")
		return 0
	StringSplit, p, t, :
	return (p1*3600)+(p2*60)+p3
}

HMS_To_Seconds2(hms) {
    if RegExMatch(hms, "^(?<H>\d{1,2}):(?<M>\d{2}):(?<S>\d{2})$", m)
        return (mH*3600 + mM*60 + mS)
}


info2(var) {
	if (var = "all") {
		if (number_of_bookmarks != "")
			number_of_bookmarks := "Number of bookmarks: " . line_num . "`n"
		if (edited_duration != "")
			save_video_length := "Save video length: " . edited_duration . "`n"
		if (file_directory != "")
			source_directory := "Source directory: " . file_directory . "`n"
		else if (file_directory_old != "") 
			source_directory := "Source directory: " . file_directory_old . "`n"
		if (time_total != "")
			source_video_length := "Source video length: " . time_total . "`n"
		if (file_path_csv != "") 
			bookmarks_loaded := "Bookmarks loaded: " . file_path_csv . "`n"
		if (file_path  != "") 
			video_loaded := "Video loaded: " . file_both . "`n"
		if (file_directory != "")
			save_directory := "Save directory: " . file_directory . "`n"
		hotkey_active := "Hotkey active: " . activehotkey
		data_main := number_of_bookmarks . bookmarks_loaded . video_loaded . source_directory . save_directory . save_video_length . source_video_length . hotkey_active
		return data_main
	} 
	if (var = "advanced") {
		var_list := ["file_path"
        , "file_path_csv"
		, "file_path_create"
		, "last_file_path"
		, "file_drive"
		, "file_directory"
		, "file_name"
		, "file_both"
		, "file_extension"
		, "file_dot_extension"
		, "folder_path"
		, "folder_path_set"
		, "data"
		, "ffmpeg_time"
		, "ffmpeg_time_selected"
		, "bookmark_started"
		, "bookmark_status"
		, "current_seconds"
		, "current_time"
		, "edited_duration"
		, "time1"
		, "time2"
		, "duration_time"
		, "previous_time"
		, "time_total"
		, "total_time"
		, "button1"
		, "button2"
		, "button3"
		, "button4"
		, "button5"
		, "button6"
		, "button7"
		, "button8"
		, "button9"
		, "button10"
		, "button11"
		, "button12"
		, "button13"
		, "button14"
		, "button15"
		, "button16"
		, "button17"
		, "button18"
		, "button19"
		, "button20"
		, "button21"
		, "button22"
		, "button23"
		, "button24"
		, "button25"
		, "button26"
		, "button27"
		, "button28"
		, "button29"
		, "button30"
		, "button31"
		, "button32"
		, "button33"
		, "button34"
		, "button35"
		, "button36"
		, "button37"
		, "button38"
		, "button39"
		, "button40"
		, "button41"
		, "checkbox_checked1"
		, "checkbox_checked2"
		, "checkbox_checked3"
		, "checkbox_checked4"
		, "checkbox_checked5"
		, "checkbox_checked6"
		, "checkbox_checked7"
		, "checkbox_checked8"
		, "checkbox_checked9"
		, "checkbox_checked10"
		, "checkbox_checked11"
		, "checkbox_checked12"
		, "checkbox_checked13"
		, "checkbox_checked14"
		, "checkbox_checked15"
		, "checkbox_checked16"
		, "checkbox_checked17"
		, "checkbox_checked18"
		, "checkbox_checked19"
		, "line_num"]
		advanced := ""
		for each, var_name in var_list {
			var_value := %var_name%
			if (var_value != "") 
				advanced .= var_name . ": " . var_value . "`n"
		}
		advanced := RTrim(advanced, "`n") 
		return advanced
	}
	if (var = "both")
		return data . "`n" . advanced
}

csv_lastline2(var) {
	file := FileOpen(var, "r")
	data := file.Read()
	if instr(data, "`n")
		var := "`n"
	else
		var := "`r"
	loop, parse, % data, %var%
		{
		if (a_loopfield != "")
			last_line := a_loopfield
	}
	return last_line
}

csv_linecount2(var) {
	file := FileOpen(var, "r")
	data := file.Read()
	if instr(data, "`n")
		var := "`n"
	else
		var := "`r"
	loop, parse, % data, %var%
		{
		if (a_loopfield != "")
			count := a_index
	}
	file.close()
	return count
}

csv_data2(var, line:="") {
	file := FileOpen(var, "r")
	data := file.Read()
	if instr(data, "`n")
		var := "`n"
	else
		var := "`r"
	if regexmatch(line, "imO)\d+") {
		loop, parse, % data, %var%
			{
			if (a_index = line) {
				data := a_loopfield
				break
			}
		}
	}
	file.close()
	return data
}

check_matching_video(file_path_csv) {
	extensions := []
	extensions.push("mp4", "avi", "mpg", "mkv", "avi", "mov", "wmv", "flv", "webm", "mpeg", "m4v", "ts")
	Loop % extensions.MaxIndex() 
		{
		new_file_path := strreplace(file_path_csv,"csv",extensions[a_index])
		if fileexist(new_file_path) {
			return file_path_new
		}
	}
}

check_matching_csv(file_path) {
	splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
	file_name := string_caseLower2(file_name)
	file_extension_dot := "." . file_extension
	new_file_path_csv := strreplace(file_path, file_extension, "csv")
	if fileexist(new_file_path_csv)
		return new_file_path_csv
}

error2(var :="") {
	if (var = "")
		msgbox,52, %a_scriptname% - error!, Script has encountered an error.`n`nWould you like to see variables?
	else 
		msgbox, 52, Error! - %a_scriptname%, %var%`n`n Would you like to see variables?
	ifmsgbox yes 
		{
		listvars
		return
	}
	ifmsgbox no
		{
		if !window_exist2("choose")
			reload
	}
}

file_directoryfix2(var) {
	if !regexmatch(var, "imO).+(\\)$")
		var := var . "\"
	return var
}

file_delete2(source) {
	filedelete, %source%
	return errorlevel
}

file_move2(source, target) {
	if regexmatch(source, "imO).+\.(...|..)$")
		FileMove, %source%, %target%
	else
		FileMoveDir, %source%, %target%, r
}

file_recycle2(source1, source2:="", source3:="", source4:="") {
	if instr(source1, " ") {
		new_source1 := strreplace(source1," ","-")
		file_move2(source1, new_source1)
		source1 := new_source1
	}
	filerecycle %source1%
	if (source2 != "") {
		if instr(source2, " ") {
			new_source2 := strreplace(source2," ","-")
			file_move2(source2, new_source2)
			source2 := new_source2
		}
		filerecycle %source2%
	}
	if (source3 != "") {
		if instr(source3, " ") {
			new_source3 := strreplace(source3," ","-")
			file_move2(source3, new_source3)
			source3 := new_source3
		}
		filerecycle %source3%
	}
	if (source4 != "") {
		if instr(source4, " ") {
			new_source4 := strreplace(source4," ","-")
			file_move2(source4, new_source4)
			source4 := new_source4
		}
		filerecycle %source4%
	}
	return errorlevel
}

file_read2(file, check:="") {
	file := FileOpen(file, "r")
	data := file.Read()
	if (check = "check") {
		if (data = "")
			error("Data is blank!")
	}
	file.close()
	return data
}

file_write2(file, string, mode:="a") {
	file := FileOpen(file, "" . mode . "")
	file.write(string)
	file.close()
}

file_write2line(file, string, mode:="a") {
	file := FileOpen(file, "" . mode . "")
	file.writeline(string)
	file.close()
}


file_extension2(string) {
	if RegExMatch(string, "iO)\.([^.]+)$", obj)
		ext := obj[1] 
	return ext
}

mpc_close2() {
	winactivate, ahk_class MediaPlayerClassicW
	send2("{shift}{escape}", "down")
}

mpc_open2(file_path) {
	run, %file_path%
}

mpc_getSource2() {
	regexmatch(window_title2("ahk_class MediaPlayerClassicW"), "imO)(.+)", get_source)
	var := get_source[1] . get_source[2]
	return var
}

mpc_getName2(byref var:="") {
	regexmatch(window_title("ahk_class MediaPlayerClassicW"), "imO).+\\(.+)", file_path)
	file_path := file_path[1]
	splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
	var := file_name
	return var
}

mpc_getPath2(byref var:="") {
	regexmatch(window_title("ahk_class MediaPlayerClassicW"), "imO)(.+\\)", file_path)
	file_path := file_path[1]
	splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
	var := file_path
	return var
}

mpc_getExt2(byref var:="") {
	file_path := window_title("ahk_class MediaPlayerClassicW")
	regexmatch(window_title("ahk_class MediaPlayerClassicW"), "imO)(....)$", get_ext)
	var := get_ext[1]
	return var
}

mpc_getDuration2(var:="") {
	controlgettext, get_duration, Static2, ahk_class MediaPlayerClassicW
	regexmatch(get_duration, "imO).+\/(.+)", time)
	raw_time := time[1]
	if regexmatch(raw_time, "imO)(\d+)\:(\d+)\:(\d+)", time)
		time_hours := time[1], time_minutes := time[2], time_seconds := time[3]
	else if regexmatch(raw_time, "imO)(\d+)\:(\d+)", time)
		time_hours := "0", time_minutes := time[1], time_seconds := time[2]
	else if regexmatch(raw_time, "imO)(\d+)", time)
		time_hours := "0", time_minutes := "0", time_seconds := time[1]
	total_seconds := (time_hours * 3600) + (time_minutes * 60) + time_seconds
	if (var = "seconds")
		var := total_seconds
	else if (var = "raw")
		var := raw_time
	else
		var := time_secToLong2(total_seconds)
	return var
}

mpc_getTime2(var:="") {
	controlgettext, get_duration, Static2, ahk_class MediaPlayerClassicW
	regexmatch(get_duration, "imO)(.+).+\/", time)
	raw_time := time[1]
	if regexmatch(raw_time, "imO)(\d+)\:(\d+)\:(\d+)", time)
		time_hours := time[1], time_minutes := time[2], time_seconds := time[3]
	else if regexmatch(raw_time, "imO)(\d+)\:(\d+)", time)
		time_hours := "0", time_minutes := time[1], time_seconds := time[2]
	else if regexmatch(raw_time, "imO)(\d+)", time)
		time_hours := "0", time_minutes := "0", time_seconds := time[1]
	total_seconds := (time_hours * 3600) + (time_minutes * 60) + time_seconds
	if (var = "seconds")
		var := total_seconds
	else
		var := time_secToLong2(total_seconds)
	return var
}

mpc_fileDelete2() {
	file_path := mpc_getPath2(file_path)
	filerecycle % file_path
	send2("{alt}{right}", "down")
}
mpc_playNext2() {
	sendinput % "{xbutton1}"
}

msg2(var, var2:="", var3:="", var4:="",var5:="",var6:="") {
	 if (var6 != "")
		msgbox, 262144, ,1 > %var%`n2 > %var2%`n3 > %var3%`n4 > %var4%`n5 > %var5%`n6 > %var6%
	else if (var5 != "")
		msgbox, 262144, ,1 > %var%`n2 > %var2%`n3 > %var3%`n4 > %var4%`n5 > %var5%
	else if (var4 != "")
		msgbox, 262144, ,1 > %var%`n2 > %var2%`n3 > %var3%`n4 > %var4%
	else if (var3 != "")
		msgbox, 262144, ,1 > %var%`n2 > %var2%`n3 > %var3%
	else if (var2 != "")
		msgbox, 262144, ,1 > %var%`n2 > %var2%
	else {
		msgbox, 262144, ,%var%
	}
}

number_max2(string, delim:=",") {
	sort, string, nuD,
	found_pos := regexmatch(string, "im)\d+$", string)
	return string
}
number_roundWhole2(var) {
	if regexmatch(var, "imO)(\d+)\.(\d+)", get_num) {
		if (get_num[2] >= 5)
			num := get_num[1] + 1
		else
			num := get_num[1]
		return num
	}
}

number_roundTen2(num) {
	if (num >= 0 and num <= 10) 
		num := 10
	if (num > 10 and num <= 20)
		num := 20
	if (num > 20 and num <= 30)
		num := 30
	if (num > 30 and num <= 40)
		num := 40
	if (num > 40 and num <= 50)
		num := 50
	if (num > 50 and num <= 60)
		num := 60
	if (num > 60 and num <= 70)
		num := 70
	if (num > 70 and num <= 80)
		num := 80
	if (num > 80 and num <= 90)
		num := 90
	if (num > 90 and num <= 100)
		num := 100
	return num
}
number_random2(var) {
	random, num, 1, var
	return num
}
number_count2(string,delim:="`,",omits:="") {
	loop, parse, % string, %delim%, omits
		count := a_index
	return count
}

number_oddeven2(num) {
	return ((num & 1) != 0) ? "Odd" : "Even"
}

number_zeros2(var) {
	if (var = 1) 
		zeros := "000"
	else if (var = 2)
		zeros := "00"
	else if (var = 3)
		zeros := "0"
	else if (var = 4 or var = 6)
		zeros := ""
	else if (var = 5)
		zeros := "0"
	return zeros
}

number_countDigits2(num) {
	Loop, Parse, num
		{
		if A_LoopField is digit
			digits++
	}
	return digits
}

path_join2(var1, var2:="", var3:="") {
	if !regexmatch(var1, "imO)(.+)\\$")
		var1 := var1 . "\"
	if regexmatch(var2, "imO)(.+)\.(mp4|avi|mpg|mpeg|mov|wmv|flv|csv|mkv|f4v|ts)$", file) {
		file_ext := file[1] . "." . file[2]
		return var1 . file_ext
	} else if regexmatch(var3, "imO)(.+)\.(mp4|avi|mpg|mpeg|mov|wmv|flv|csv|mkv|f4v|ts)$", file) {
		file_ext := file[1] . "." . file[2]
		if regexmatch(var2, "imO)[^\\](.+)[^\\]$")
			return var1 . var2 . file_ext
		if !regexmatch(var2, "imO)(.+)\\$")
			return var1 . var2 . "\" . file_ext
		else
			return var1 . var2 . file_ext
	} else if regexmatch(var3, "imO)\.(mp4|avi|mpg|mpeg|mov|wmv|flv|csv|mkv|f4v|ts)$", file) {
		ext := "." . file[1]
		return var1 . var2 . ext
	} else {
		if !regexmatch(var2, "imO)(.+)\\$")
			return var1 . var2 . "\"
		else
			return var1 . var2
	}
	
}

run2(var, var2:="") {
	if (var = "regedit")
		Run *RunAs C:\Windows\Regedit.exe
	else if (var = "explorer")
		Run, %ComSpec% /c "start explorer.exe"
	else if (var2 = "hide")
		Run, % var, , hide
	else
		Run, % var
}

run_command2(var1, var2:="", var3:="", var4:="") {
	var1 := """" . var1 . """"
	var2 := """" . var2 . """"
	var3 := """" . var3 . """"
	var4 := """" . var4 . """"
    if (var4 != "") 
        parameters := var1 . " " . var2 . " " . var3 . " " . var4
    else if (var3 != "") 
		parameters := var1 . " " . var2 . " " . var3
	else if (var2 != "") 
		parameters := var1 . " " . var2
    else 
        parameters := var1
    Run %comspec% /c %var1% %var2%
}

send2(var, options:="", speed:="600") {
	keyMapping := {}
	; Modifier Keys
	keyMapping["shift"] := true
	keyMapping["control"] := true
	keyMapping["ctrl"] := true
	keyMapping["alt"] := true
	keyMapping["lwin"] := true
	keyMapping["rwin"] := true
	keyMapping["capslock"] := true

	; Alphanumeric Keys
	Loop, 26 { ; a-z
		keyMapping[Chr(Asc("a") + A_Index - 1)] := true
	}
	Loop, 10 { ; 0-9
		keyMapping[Chr(48 + A_Index - 1)] := true
	}

	; Navigation and Editing Keys
	keyMapping["space"] := true
	keyMapping["enter"] := true
	keyMapping["tab"] := true
	keyMapping["escape"] := true
	keyMapping["esc"] := true
	keyMapping["backspace"] := true
	keyMapping["delete"] := true
	keyMapping["del"] := true
	keyMapping["insert"] := true
	keyMapping["home"] := true
	keyMapping["end"] := true
	keyMapping["pgup"] := true
	keyMapping["pgdn"] := true
	keyMapping["up"] := true
	keyMapping["down"] := true
	keyMapping["left"] := true
	keyMapping["right"] := true

	; Function Keys
	Loop, 12 {
		keyMapping["f" . A_Index] := true
	}

	; Numpad Keys
	keyMapping["numpad0"] := true
	keyMapping["numpad1"] := true
	keyMapping["numpad2"] := true
	keyMapping["numpad3"] := true
	keyMapping["numpad4"] := true
	keyMapping["numpad5"] := true
	keyMapping["numpad6"] := true
	keyMapping["numpad7"] := true
	keyMapping["numpad8"] := true
	keyMapping["numpad9"] := true
	keyMapping["numpadadd"] := true
	keyMapping["numpadsub"] := true
	keyMapping["numpadmul"] := true
	keyMapping["numpaddiv"] := true
	keyMapping["numpaddot"] := true
	keyMapping["numpadenter"] := true
	keyMapping["numpadclear"] := true
	keyMapping["numpaddel"] := true

	; Symbol Keys
	keyMapping["`"] := true
	keyMapping["-"] := true
	keyMapping["="] := true
	keyMapping["["] := true
	keyMapping["]"] := true
	keyMapping["\\"] := true
	keyMapping[";"] := true
	keyMapping["'"] := true
	keyMapping[","] := true
	keyMapping["."] := true
	keyMapping["/"] := true


	; Special Keys
	keyMapping["printscreen"] := true
	keyMapping["scrolllock"] := true
	keyMapping["pause"] := true
	keyMapping["break"] := true
	keyMapping["appskey"] := true
	keyMapping["numlock"] := true

	; Mouse Buttons
	keyMapping["lbutton"] := true
	keyMapping["rbutton"] := true
	keyMapping["mbutton"] := true
	keyMapping["xbutton1"] := true
	keyMapping["xbutton2"] := true


	if instr(var, "down") and instr(var, "up") {
		send_keys := var
	} else if instr(options, "down") and !instr(var, "{") {
		if regexmatch(var, "imO)(.+)\s(.+)", key_found) {
			send_keys := "{" . key_found[1] . " down}{" . key_found[2] . " down}{" . key_found[2] . " up}{" . key_found[1] . " up}"
		}
	} else if regexmatch(var, "imO)^\{?(.+)\}?(\,|\s)?\{?(.+)\}?(\,|\s)?\{?(.+)\}?$", key_found) {
		if keyMapping.HasKey(key_found[1]) and keyMapping.HasKey(key_found[3]) and keyMapping.HasKey(key_found[5]) {
			if instr(options, "down") {
				send_keys := "{" . key_found[1] . " down}{" . key_found[3] . " down}{" . key_found[5] . " down}{" . key_found[5] . " up}{" . key_found[3] . " up}{" . key_found[1] . " up}"
			} else {
				send_keys := "{" . key_found[1] . "}{" . key_found[2] . "}{" . key_found[3] . "}"
			}
		} else {
			send_keys := var
		}
	} else if regexmatch(var, "imO)^\{?([^,}]+)\}?(?:[,\s]+)?\{?([^}]+)\}?$", key_found) {
		if keyMapping.HasKey(key_found[1]) and keyMapping.HasKey(key_found[2]) {
			if instr(options, "down") {
				send_keys := "{" . key_found[1] . " down}{" . key_found[2] . " down}{" . key_found[2] . " up}{" . key_found[1] . " up}"
			} else {
				send_keys := "{" . key_found[1] . "}{" . key_found[2] . "}"
			}
		} else {
			send_keys := var
		}
	} else if regexmatch(var, "imO)^\{?[^,]+\}?$", key_found) {
		if keyMapping.HasKey(key_found[1]) {
			send_keys := "{" . key_found[1] . " down}{" . key_found[1] . " up}"
		} else {
			send_keys := var
		}
	}
	sendinput % send_keys
}

string_random2(length:="10") {
    randomString := ""
    validChars := "abcdefghijklmnopqrstuvwxyz0123456789"

    Loop, %length% {
        randomIndex := number_Random2(StrLen(validChars))
        randomChar := SubStr(validChars, randomIndex, 1)
        randomString .= randomChar
    }
    return randomString
}
string_trimRight2(string, num:=1) {
	Loop %num%
		dot .= "."
	regexmatch(string, "imO)(.+)(" . dot . ")$", match)
	string := match[1]
	return string
}
	
string_trimLeft2(string, num:=1) {
	Loop %num%
		dot .= "."
	regexmatch(string, "imO)(" . dot . ")(.+)$", match)
	string := match[2]
	return string
}

string_trimSpaces2(string) {
	string := strreplace(string, A_Space, "-")
	string := regexreplace(string, "im)\s+")
	string := regexreplace(string, "im)\s+$")
	string := regexreplace(string, "im)^\s+")
	return string
	listlines, on
}
string_removeDuplicates2(string, delim:=",") {
	listlines, off
	sort, string, uD%delim%
	return string
	listlines, on
}
string_removeSpaces2(string) {
	listlines, off
	return string := strreplace(string, A_Space, "")
	listlines, on
}
string_case2(string:="") {
	listlines, off
	num := number_random2(5)
	selection := num = 1 ? string_caseUpper2(string) : num = 2 ? string_caseTitle2(string) : num = 3 ? string_caseLower2(string) : num = 4 ? string_caseBlend2(string) : num = 5 ? string_caseSentence2 : selection
	return selection
	listlines, on
}
string_caseUpper2(string:="") {
	listlines, off
	if (string != "") {
		StringUpper, caseUpper, string
		return caseUpper
	} else {
		copy2(selected)
		wait2(1)
		StringUpper, caseUpper, selected
		sendinput % caseUpper
	}
	listlines, on
}
string_caseTitle2(string:="") {
	listlines, off
	if (string != "") {
		StringUpper, caseTitle, string, T
		return caseTitle
	} else {
		copy2(selected)
		wait2(1)
		StringUpper, caseTitle, string, T
		sendinput % caseTitle
	}
	listlines, on
}
string_caseLower2(string:="") {
	listlines, off
	if (string != "") {
		StringLower caseLower, string
		return caseLower
	} else {
		copy2(selected)
		wait2(1)
		StringLower, caseLower, string
		sendinput % caseLower
	}
	listlines, on
}
string_caseBlend2(string:="") {
	listlines, off
	if (string != "") {
		string := string_caseLower2(string)
		return RegExReplace(string, "(((^|([.!?]\s+))[a-z])| i | i')", "$u1")
	} else {
		copy2(selected)
		ClipWait
		selected := string_caseLower2(selected)
		selected := RegExReplace(selected, "(((^|([.!?]\s+))[a-z])| i | i')", "$u1")
		sendinput % selected
	}
	listlines, on
}
string_caseSentence2(string:="") {
	listlines, off
	X = I,AHK,AutoHotkey

	string := RegExReplace(string, "[\.\!\?]\s+|\R+", "$0þ") ; mark 1st letters of sentences with char 254
	Loop Parse, string, þ
	{
		StringLower L, A_LoopField
		I := Chr(Asc(A_LoopField))
		StringUpper I, I
		S .= I SubStr(L,2)
	}
	Loop Parse, X, `,
		S := RegExReplace(S,"i)\b" A_LoopField "\b", A_LoopField)

	return S
	listlines, on
}

string_JoinAfter2(var1, var2) {
	new_string := var1 . var2
	return new_string
}

string_JoinBefore2(var1, var2) {
	new_string := var2 . var1
	return new_string
}

string_CountCharacter2(string, character) {
    count := 0
    loop, parse, string
    {
        if (a_loopfield = character)
            count++
    }
    return count
}

string_RemoveBlanks2(string) {
	new_string := RegExReplace(string, "\R+", "`n")
	return new_string
}

string_invertCase2(str) {
	listlines, off
	Lab_Invert_Char_Out := ""
	Loop % Strlen(str) {
		Lab_Invert_Char := Substr(str, A_Index, 1)
		if Lab_Invert_Char is upper
			Lab_Invert_Char_Out := Lab_Invert_Char_Out Chr(Asc(Lab_Invert_Char) + 32)
		else if Lab_Invert_Char is lower
			Lab_Invert_Char_Out := Lab_Invert_Char_Out Chr(Asc(Lab_Invert_Char) - 32)
		else
			Lab_Invert_Char_Out := Lab_Invert_Char_Out Lab_Invert_Char
	}
	RETURN Lab_Invert_Char_Out
	listlines, on
}

time_LongToBookmark2(hrTime, total_time) {
	regexmatch(total_time, "imO)(\d{2}):(\d{2}):(\d{2})", time_seek)
	if regexmatch(hrTime, "imO)^(.)$", time)
		new_time := "000" . time[1] . ".000"
	else if regexmatch(hrTime, "imO)^(..)$", time)
		new_time := "00" . time[1] . ".000"
	else if regexmatch(hrTime, "imO)^(.):(..)$", time) {
		if (time_seek[1] = 02 or time_seek[1] = 01)
			new_time := "000" . time[1] . time[2] . ".000"
		else
			new_time := "0" . time[1] . time[2] . ".000"
	} else if regexmatch(hrTime, "imO)^(..):(..)$", time) {
		if (time_seek[1] = 02 or time_seek[1] = 01)
			new_time := "00" . time[1] . time[2] . ".000"
		else
			new_time := time[1] . time[2] . ".000"
	} else if regexmatch(hrTime, "imO)^(.):(..):(..)$", time)
		new_time := "0" . time[1] . time[2] . time[3] . ".000"
	else if regexmatch(hrTime, "imO)(..):(..):(..)", time) {

		if (time_seek[1] = 02 or time_seek[1] = 01)
			new_time := time[1] . time[2] . time[3] . ".000"
		else
			new_time := time[1] . time[2] . time[3] . ".000"
	}
	return new_time
}


time_secToLong2(var) {
    hours := Floor(var / 3600)
    rem := var - (hours * 3600)

    minutes := Floor(rem / 60)
    seconds := rem - (minutes * 60)

    hours := (hours < 10) ? "0" hours : hours
    minutes := (minutes < 10) ? "0" minutes : minutes
    seconds := (seconds < 10) ? "0" seconds : seconds

    return hours ":" minutes ":" seconds
}

time_secToShort2(var) {
	var := time_secToLong2(var)
	parts := StrSplit(var, ":")
    if (parts.Count() = 3) {
        hours := parts[1]
		minutes := parts[2]
		seconds := parts[3]
        hours := (hours = "00") ? "" : hours + 0
        minutes := minutes + 0
        seconds := seconds + 0
	    minutes := (minutes < 10) ? "0" minutes : minutes
        seconds := (seconds < 10) ? "0" seconds : seconds
		if (hours = "") {
			if regexmatch(minutes, "imO)0([1-9])", min)
				minutes := min[1]
        }
		if (hours = "" and minutes = "00") {
			minutes := ""
			if regexmatch(seconds, "imO)0([1-9])", sec)
				seconds := sec[1]
        }
        if (hours = "" and minutes = "")
			time := seconds
		else if (hours = "")
            time := minutes ":" seconds
        else
            time := hours ":" minutes ":" seconds
        return time
    }		
}


time_secToAlt2(var) {
    hours := Floor(var / 3600)
    rem := var - (hours * 3600)

    minutes := Floor(rem / 60)
    seconds := rem - (minutes * 60)
	time := ""
    if (hours > 0)
        time .= hours "h "

    if (minutes > 0)
        time .= minutes "m "

    if (seconds > 0)
        time .= seconds "s"
    return Trim(time, ", ")
}

time_shortToLong2(var) {
    parts := StrSplit(var, ":")
    if (parts.Count() = 3) {
        hours := parts[1]
		if (strlen(hours) = 1)
			hours := "0" . hours
        minutes := parts[2]
        seconds := parts[3]
    } else if (parts.Count() = 2) {
        hours := 00
        minutes := parts[1]
		if (strlen(minutes) = 1)
			minutes := "0" . minutes
        seconds := parts[2]
    } else {
        hours := 00
        minutes := 00
        seconds := parts[1]
		if (strlen(seconds) = 1)
			seconds := "0" . seconds
    }
	return hours ":" minutes ":" seconds
}

time_shortToSec2(var) {
    parts := StrSplit(var, ":")
    if (parts.Count() = 3) {
        hours := parts[1]
        mins := parts[2]
        secs := parts[3]
    } else if (parts.Count() = 2) {
        hours := 0
        mins := parts[1]
        secs := parts[2]
    } else {
        hours := 0
        mins := 0
        secs := parts[1]
    }
    totalSeconds := (hours * 3600) + (mins * 60) + secs
    return totalSeconds
}

time_shortToAlt2(var) {
	time1 := time_shortToSec2(var)
	time := time_secToAlt2(time1)
	return time
}

time_longToSec2(var) {
	if regexmatch(var, "imO)(\d+)\:(\d+)\:(\d+)", getting_time) {
		hours := getting_time[1]
		minutes := getting_time[2]
		seconds := getting_time[3]
		if (seconds = "00")
			seconds := "0"
		if (minutes = "00")
			minutes := "0"
		if (hours = "00")
			hours := "0"
		hours_to_seconds := hours * 3600
		minutes_to_seconds := minutes * 60
		return hours_to_seconds + minutes_to_seconds + seconds
	}
}

time_longToShort2(var) {
	time := time_longToSec2(var)
	time := time_secToShort2(time)
	return time
}

time_longToAlt2(var) {
	if regexmatch(var, "imO)(\d+)\:(\d+)\:(\d+)", getting_time) {
		hours := getting_time[1]
		minutes := getting_time[2]
		seconds := getting_time[3]
	} else if regexmatch(var, "imO)(\d+)\:(\d+)", getting_time) {
		hours := "00"
		minutes := getting_time[1]
		seconds := getting_time[2]
	} else if regexmatch(var, "imO)(\d+)", getting_time) {
		hours := "00"
		minutes := "00"
		seconds := getting_time[2]
	} else {
		hours := "00"
		minutes := "00"
		seconds := "00"
	}
	hours := (hours != "00" and hours != 0) ? hours + 0 "h" : ""
	minutes := (minutes != "00" and minutes != 0) ? minutes + 0 "m" : ""
	seconds := (seconds != "00" and seconds != 0) ? seconds + 0 "s" : ""
	time := ""
	if (hours != "" and hours != "h") 
		time .= hours . " "
	if (minutes != "" and minutes != "m") 
		time .= minutes . " "
	if (seconds != "" and seconds != "s") 
		time .= seconds
	return time
}



ui_setup2(name, options:="+owner +border -resize -maximizebox -sysmenu -caption -toolwindow +DPIScale -alwaysontop") {
	gui, %name%:default
	gui, margin, 0, 0
	gui, color, black, white
	gui, font, s14 cFFFFFF bold, Segoe UI
	gui, %options%
	return name
}



ui_pos2(options) {
	listlines, off
	if regexmatch(options, "imO)(\+)(left|absolute-left|right|absolute-right|center|top|absolute-top|bottom)", wcp) {
		pos := wcp[2] = "center" ? "center" : pos
		pos := wcp[2] = "absolute-left" ? "x0" : pos
		pos := wcp[2] = "left" ? "x" . A_ScreenWidth * 0.25 : pos
		pos := wcp[2] = "right" ? "x" . A_ScreenWidth * 0.75 : pos
		pos := wcp[2] = "absolute-right" ? "x" . A_ScreenWidth - 50 : pos
		pos := wcp[2] = "top" ? "y" . A_ScreenHeight * .25 : pos
		pos := wcp[2] = "absolute-top" ? "y60" : pos
		pos := wcp[2] = "bottom" ? "y" . A_ScreenHeight * 0.75 : pos
		pos := regexreplace(pos, "im)^(x|y)$")
		options := pos
	} else if regexmatch(options, "imO)(x\d{1,4})\s?(y\d{1,4})?", get_xypos) 
		options := get_xypos[0]
	else
		options := "center"
	listlines, on
	gui, show, %options%, %name%
}
ui_color2(options, control:="") {
	listlines, off
	colors := "black|silver|gray|maroon|red|purple|fuschia|green|lime|olive|navy|blue|teal|yellow|aqua|orange|white"
	if regexmatch(options, "imO)\+(wc|cc)(" . colors . ")\s?\+?(wc|cc)?(" . colors . ")?", wcc) {
		wc := wcc[1] = "wc" ? wcc[2] : wc
		wc := wcc[3] = "wc" ? wcc[4] : wc
		cc := wcc[1] = "cc" ? wcc[2] : cc
		cc := wcc[3] = "cc" ? wcc[4] : cc
		if (wc = "white")
			gui, font, cBlack
		if (wc = "black")
			gui, font, cWhite
		listlines, on
		gui, color, %wc%, %cc%
	}
}
ui_font2(options) {
	listlines, off
	if ff = ""
		ff := "Segoe UI"
	if regexmatch(options, "imO)(?<=\+)?(bold|italic|underline|norm)(\d+)?", ft) {
		options := regexreplace(options, "im)(\+)(bold|italic|underline|norm)", "$2")
		if (ft[1] = "bold" && ft[2] != "")
			options := strreplace(options, ft[0], " w" . ft[2])
		if (ft[1] = "bold" && ft[2] = "")
			options := strreplace(options, "bold", " w900")
	} else
		options .= " w900"
	if regexmatch(options, "imO)(?<=\+)(\d+)", fs)
		options := strreplace(options, fs[0], " s" . fs[1])
	if regexmatch(options, "imO)(\+)(black|silver|gray|maroon|red|purple|fuschia|green|lime|olive|navy|blue|teal|yellow|aqua|orange|white)", fc)
		options := strreplace(options, fc[0], " c" . fc[2])
	listlines, on
	gui, font, %options%, %ff%
}
ui_options2(var:="") {
	if (var = "")
		var := "+owner +border -resize -maximizebox -sysmenu -caption -toolwindow -alwaysontop "
	gui, %var%
}
ui_show2(name:="MyName") {
	gui, %name%:default
	gui, show, NA, %name%
}
ui_hide2(name:="MyName") {
	gui, %name%:default
	gui, hide
}
ui_add_picture2(value, options:="") {
	global
	if regexmatch(options, "imO)(\+w)(\d+)", pcw) 
		options := strreplace(options, pcw[1], " w")
	if !regexmatch(options, "im)(\w)(\d+)") && regexmatch(gui_size, "imO)(\d+)x(\d+)", wh)
		options .= " w" . wh[1]
	options := strreplace(options, "transparent", "backgroundtrans 0x4000000")
	options := strreplace(options, "aspect", "h-1")
	if !regexmatch(options, "im)h-1") and !regexmatch(options, "im)h\d+")
		options .= " h-1"	
	if !regexmatch(options, "im)backgroundtrans 0x4000000")
		options .= " backgroundtrans 0x4000000"
	gui, add, picture, %options%, %value%
}
ui_add_text2(value, options:="") {
	global
	listlines, off
	options := strreplace(options, "+transparent", "backgroundtrans 0x4000000")
	if !regexmatch(options, "im)backgroundtrans 0x4000000")
		options .= " backgroundtrans 0x4000000"
	if regexmatch(options, "imO)(\+w)(\d+)", cw) 
		options := strreplace(options, cw[1], "w")
	if !regexmatch(options, "imO)(w)(\d+)") && regexmatch(gui_size, "imO)(\d+)x(\d+)", wh)
		options .= " w" . wh[1]
	if regexmatch(options, "imO)(\+v)(\w+)", pv)
		options := strreplace(options, pv[1], " v")
	if regexmatch(options, "imO)(\+g)(\w+)", pg)
		options := strreplace(options, pg[1], " g")
	if regexmatch(options, "imO)(\+)(left|right|center)", pos)
		options := strreplace(options, pos[1])
	if regexmatch(options, "imO)(\+)(hscroll)", scroll)
		options := strreplace(options, scroll[1])
	else
		options .= " center"
	if regexmatch(options, "imO)(\+)?(black|silver|gray|maroon|red|purple|fuschia|green|lime|olive|navy|blue|teal|yellow|aqua|orange|white)", c)
		options := strreplace(options,c[1]c[2], " c" . c[2])
	listlines, on
	gui, add, text, %options%, %value%
}
ui_add_button2(value:="OK", options:="") {
	global
	listlines, off
	options := strreplace(options, "+transparent", "backgroundtrans")
	if regexmatch(options, "imO)(\+w)(\d+)", bcw) 
		options := strreplace(options, bcw[1], "w")
	if !regexmatch(options, "imO)(w)(\d+)") && regexmatch(gui_size, "imO)(\d+)x(\d+)", wh)
		options .= " w" . wh[1]
	if regexmatch(options, "imO)(\+v)(\w+)", bv)
		options := strreplace(options, bv[1], " v")
	if regexmatch(options, "imO)(\+g)(\w+)", bg)
		options := strreplace(options, bg[1], " g")
	else if regexmatch(value, "imO)(\w+)\s(\w+)", bg) 
		options .= " g" . bg[1] . bg[2]		
	else if regexmatch(value, "imO)(\w+)", bg) 
		options .= " g" . bg[1]
	listlines, on
	gui, add, button, %options%, %value%
}
ui_add_radio2(value:="", options:="Checked") {
	global
	listlines, off
	if regexmatch(options, "imO)(\+w)(\d+)", rcw) 
		options := strreplace(options, rcw[1], "w")
	if !regexmatch(options, "imO)(w)(\d+)") && regexmatch(gui_size, "imO)(\d+)x(\d+)", wh)
		options .= " w" . wh[1]
	if regexmatch(options, "imO)(\+v)(\w+)", rv)
		options := strreplace(options, rv[1], " v")
	if regexmatch(options, "imO)(\+g)(\w+)", rg)
		options := strreplace(options ,rg[1], " g")
	if regexmatch(options, "imO)(\+)(checked)", checked)
		options := strreplace(options, checked[1])
	listlines, on
	gui, add, radio, %options%, %value%
}
ui_add_checkbox2(value:="", options:="checkedGray") {
	global
	listlines, off
	if regexmatch(options, "imO)(\+w)(\d+)", rcw) 
		options := strreplace(options, rcw[1], "w")
	if !regexmatch(options, "imO)(w)(\d+)") && regexmatch(gui_size, "imO)(\d+)x(\d+)", wh)
		options .= " w" . wh[1]
	if regexmatch(options, "imO)(\+v)(\w+)", rv)
		options := strreplace(options, rv[1], " v")
	if regexmatch(options, "imO)(\+g)(\w+)", rg)
		options := strreplace(options ,rg[1], " g")
	if regexmatch(options, "imO)(\+)(checked)", checked)
		options := strreplace(options, checked[1])
	gui, add, checkbox, %options%, %value%
	listlines, on
}

ui_add_edit2(value:="", options:="") {
	global
	if regexmatch(options, "imO)(\+w)(\d+)", bcw) 
		options := strreplace(options, bcw[1], "w")
	if !regexmatch(options, "imO)(w)(\d+)") && regexmatch(gui_size, "imO)(\d+)x(\d+)", wh)
		options .= " w" . wh[1]
	if regexmatch(options, "imO)(\+v)(\w+)", bv)
		options := strreplace(options, bv[1], " v")
	else
		options .= " v" . value
	gui, add, edit, %options%, %value%
}
ui_destroy2(name:="") {
	if (name = "")
		name := this.name
	gui name:destroy
}
ui_add_progress2(options:="") {
	global
	if regexmatch(options, "imO)(\+pb)(\d+)", pb) {
		options := strreplace(options, pb[1])
		percent := pb[2]
	}
	options := strreplace(options, "+transparent", "backgroundtrans 0x4000000")
	if !regexmatch(options, "im)backgroundtrans 0x4000000")
		options .= " backgroundtrans 0x4000000"
	if regexmatch(options, "imO)(\+bc)(black|silver|gray|maroon|red|purple|fuschia|green|lime|olive|navy|blue|teal|yellow|aqua|orange|white)", bc)
		options := strreplace(options, bc[1], " c")
	if regexmatch(options, "imO)(\+bg)(black|silver|gray|maroon|red|purple|fuschia|green|lime|olive|navy|blue|teal|yellow|aqua|orange|white)", bg)
		options := strreplace(options, bg[1], " background")
	if regexmatch(options, "imO)(\+w)(\d+)", cw)
		options := strreplace(options, cw[1], " w")
	if regexmatch(options, "imO)(\+v)(\w+)", pv)
		options := strreplace(options, pv[1], " v")
	gui, add, progress, %options%, %percent%
}

wait2(var:="100") {
	listlines, off
	if regexmatch(var, "imO)^[1-9][0-9]?$")
		var .= "000"
	sleep, %var%
	listlines, on
}

window_waitExist2(title:="a", time:="") {
	WinWait, %title%, , %time%
	return errorlevel
}
window_waitClose2(title:="a", time:="") {
	WinWaitClose, %title%, , %time%
	return errorlevel
}
window_waitNotActive2(title:="a", time:="") {
	WinWaitNotActive, %title%, , %time%
	return errorlevel
}
window_waitActive2(title:="a", time:="") {
	WinWaitActive, %title%, , %time%
	return errorlevel
}
window_maximize2(title:="a") {
	winmaximize, %title%
	winget, max_status, minmax, %title%
	if (max_status = 0) 
		winactivate, %title%
}
window_minimize2(title:="a") {
	winminimize, %title%
	winget, max_status, minmax, %title%
		if (max_status != -1) 
		winminimize, %title%
}
window_minmax2(title:="a") {
	winget, max_status, minmax, %title%
	if (max_status = -1)
		max_status := "minimized"
	else if (max_status = 1)
		max_status := "maximized"
	return max_status
}
window_activate2(title:="a") {
	winactivate, %title%
}
window_kill2(title:="a") {
	listlines, off
	winkill, %title%
	listlines, on
}
window_hide2(title:="a") {
	listlines, off
	winhide, %title%
	listlines, on
}
window_show2(title:="a") {
	listlines, off
	winshow, %title%
	listlines, on
}
window_exist2(title:="a") {
	if winexist(title) 
		return 1
	else
		return 0
}
window_active2(title:="a") {
	if winactive(title) 
		return 1
	return 0
}
window_getActive2(ByRef var:="") {
	WinGetActiveTitle, var
	return var
}
window_id2(title:="a") {
	winget, uservar, id, %title%
	return uservar
}
window_pid2(title:="a") {
	winget, uservar, pid, %title%
	return uservar
}
window_path2(title:="a") {
	winget, uservar, processpath, %title%
	return uservar
}
window_process2(title:="a") {
	winget, uservar, processname, %title%
	return uservar
}

window_controls2(title:="a") {
	winget, uservar, controllist, %title%
	return uservar
}

window_title2(title = "a") {
    if InStr(title, "ahk_exe") {
        title := StrReplace(title, "ahk_exe ", "")
        WinGet, windowList, List, ahk_exe %title%
        Loop, %windowList% {
            windowID := windowList%A_Index%
            WinGetTitle, currentTitle, ahk_id %windowID%
            if (currentTitle != "")
                return currentTitle
        }
    } else {
        WinGetTitle, uservar, %title%
        return uservar
    }
    return ""
}

window_setTitle2(new_title, title:="a") {
	listlines, off
	winsettitle, %title%, , %new_title%
	listlines, on
}

window_text2(title:="a") {
	listlines, off
	wingettext, uservar, text, %title%
	return uservar
	listlines, on
}

window_class2(title:="a") {
	wingetclass, uservar, class, %title%
	return uservar
}

window_stats2(title:="a") {
	wingetpos, x, y, width, height, %title%
	if (title = "a")
		wingetactivestats, blank, width, height, x, y
	uservar := {}
	uservar.width := width - 5
	uservar.height := height - 5
	uservar.x := x
	uservar.y := y
	return uservar
}

window_move2(var, title:="a") {
	if regexmatch(var, "imO)(\d+)(\s\d+)?", pos) {
		xpos := pos[1]
		ypos := pos[2]
	}
	winmove, %title%, , %xpos%, %ypos%
}

window_ontop2(title:="a") {
	WinGet, exStyle, ExStyle, %title%
	WS_EX_TOPMOST := 0x00000008
	if (exStyle & WS_EX_TOPMOST) {
		winset, alwaysontop, off, %title%
		window_setTitle2(regexreplace(window_title2(), "imO)\*{8}"))
	} else {
		winset, alwaysontop, on, %title%
		window_setTitle2("******** " . window_title2())
	}
}
status2(num, var:="", control:="MyText", delay:="") {
	get_color := object("0","000000","1","fa004b","2","fa0047","3","fa0043","4","fa003e","5","fa003a","6","fa0036","7","fa0032","8","fa002e","9","fa002a","10","fa0026","11","fa0021","12","fa001d","13","fa0019","14","fa0015","15","fa0011","16","fa000c","17","fa0008","18","fa0004","19","fa0000","20","fa0400","21","fa0800","22","fa0d00","23","fa1100","24","fa1500","25","fa1900","26","fa1d00","27","fa2600","28","fa2a00","29","fa2e00","30","fa3200","31","fa3600","32","fa3a00","33","fa3e00","34","fa4300","35","fa4700","36","fa4b00","37","fa5300","38","fa5700","39","fa5c00","40","fa6000","41","fa6800","42","fa6c00","43","fa7000","44","fa7500","45","fa7900","46","fa7d00","47","fa8100","48","fa8500","49","fa8a00","50","fa8e00","51","fa9200","52","fa9600","53","fa9a00","54","fa9e00","55","faa300","56","faa700","57","faab00","58","faaf00","59","fab300","60","fab700","61","fabb00","62","fac000","63","fac400","64","fac800","65","facc00","66","fad000","67","fad400","68","fad900","69","fadd00","70","fae100","71","fae500","72","fae900","73","faed00","74","faf200","75","faf600","76","fafa00","77","f6fa00","78","f2fa00","79","edfa00","80","e9fa00","81","e5fa00","82","e1fa00","83","ddfa00","84","d9fa00","85","d5fa00","86","d0fa00","87","ccfa00","88","c4fa00","89","bbfa00","90","abfa00","91","a2fa00","92","9afa00","93","92fa00","94","8afa00","95","81fa00","96","79fa00","97","70fa00","98","68fa00","99","60fa00","100","58fa00")
	if (num = "error") {
		error := 1
		color_chosen := get_color.1
		num := 100
		sleep_num := 2000
	} else if (num = "red") {
		color_chosen := get_color.1
		num := 100
	} else {
		for k, v in get_color {
			if (num = k)
				color_chosen := v
		}
	}
	if (var = "") {
		var := num . "%"
	}
	guicontrol, ,%control%, %var%
	sleep %delay%
	if (error = 1) {
		wait2(sleep_num)
		if (a_scriptname = "move_file.ahk") {
			window_kill2("save_playlist.ahk")
			window_kill2("move_file.ahk")
		} else if (a_scriptname = "save_playlist.ahk") {
			window_kill2("move_file.ahk")
			window_kill2("save_playlist.ahk")
		}
	Pause
	}
}
click_relative2(xpos:="", ypos:="", num:=1, button:="l") {
	coordmode, mouse, window
	send {click %xpos% %ypos% %num% %button%}
	s(100)
}

copy2(byref var:="") {
	sendinput % "{ctrl down}{c down}{c up}{ctrl up}"
	wait2(100)
	var := clipboard
	return var
}
