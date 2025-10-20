global last_known_video_path := ""
global g_manual_load_in_progress := 0
menu_show2("test") 
first := 1

Initialize()
settimer, csv_time, 250
settimer, file_time, 1
settimer, file_path, 250
settimer, time_total, 250
settimer, time_current, 250
return

Initialize() {
    global
    max_detected := 0
    loop, 50 {
        IniRead, test_path, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_video%A_Index%
        if (test_path != "ERROR" && test_path != "")
            max_detected := A_Index
    }
    IniWrite, %max_detected%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_number_actual
    history_number_actual := max_detected
    if (history_number_actual = 0)
        gosub, clear_history
    menu_show2("test") 
    update_menus2()
    if window_exist2("ahk_class MediaPlayerClassicW") {
        file_path := current_video_path := mpc_getSource2()
        show_gui2() 
        if (current_video_path && !InStr(current_video_path, "Media Player Classic")) {
            file_path := current_video_path
            if (file_path != "") {
                splitpath, file_path, , file_directory
                if !regexmatch(file_directory, "imO).+\\$")
                    file_directory := file_directory . "\"
                Menu, SaveToMenu, rename, 2&, Current: %file_directory%
                time_current := mpc_getTime2()
            }
            last_known_video_path := current_video_path
            check_csv_path := check_matching_csv2(file_path)
            if fileexist(check_csv_path) {
                file_path_csv := check_csv_path
                timer := "on"
                show_gui2()
                timer := "off"
            }
            update_menus2()
        }
    } else
        show_gui2()
    SetTimer, MonitorMPC, 500
}

MonitorMPC:
    if window_exist2("ahk_class MediaPlayerClassicW") {
        current_video_path := mpc_getSource2()
        if (current_video_path != last_known_video_path) {   
            if (g_manual_load_in_progress = 1) {
                last_known_video_path := current_video_path
                g_manual_load_in_progress := 0
                return
            }
            if (current_video_path && !InStr(current_video_path, "Media Player Classic")) {
                file_path := current_video_path 
				splitpath, file_path, , file_directory
				if !regexmatch(file_directory, "imO).+\\$")
					file_directory := file_directory . "\"
				Menu, SaveToMenu, rename, 2&, Current: %file_directory%
				iniwrite, %file_directory%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, file_directory     
                check_csv_path := check_matching_csv2(file_path)
				if (file_path_csv != check_csv_path) {	
					if fileexist(check_csv_path) {
						file_path_csv := check_csv_path
						timer := "on"
						show_gui2()
						timer := "off"
					}
				}
                update_menus2()
                last_known_video_path := current_video_path
            }
        }
    } else {
        if (last_known_video_path != "") {
			Menu, SaveToMenu, rename, 2&, Current: %a_desktop%\
			file_directory := a_desktop . "\"
			iniwrite, %file_directory%, %A_AppData%\MPC-HC Video Editor\compile_data.ini, stored_data, file_directory
            file_path := ""
            last_known_video_path := ""
		} else
			file_path := ""
	}
return

file_time:
	update_menus2()
return

file_path:
	if (file_path and last_file_path = "" or file_path and last_file_path != file_path) {
		global new := yes
		SB_SetText(file_path,2)
		if (delete_no_history = 1)
			menu, historymenu, delete, <no history>
		gosub, add_history
		iniread, exists, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_video0
		if (exists = "ERROR")
			inidelete, %A_AppData%\MPC-HC Video Editor\compile_data.ini, settings, history_video0
		delete_no_history := 0
		error := 0
		last_file_path := file_path
	} else
		new := ""
	if (!file_path)
		SB_SetText("Video: <not loaded>",2)
	else
		SB_SetText(file_path,2)
	get_dir := GetMenuItemText2("savetomenu", 2)
	if regexmatch(get_dir, "imO)Current\:\s(.+)", get_dir) {
		dir := get_dir[1]
		if (dir = a_desktop "\") 
			menu, savetomenu, check, 7&
		else
			menu, savetomenu, uncheck, 7&
	}
return

csv_time:
	filegetsize, check_csv, file_path_csv
	if (file_path_csv) {
		edited_duration := edit_duration2(file_path_csv)
		edited_duration := time_secToAlt2(edited_duration)
		Gui, Font, cWhite
		guicontrol, font, edittime
		guicontrol, , edittime, Edit time:
		Gui, Font, cFFA500
		guicontrol, font, EditedDuration
		guicontrol, , EditedDuration, %edited_duration%
		guicontrol, show, editedduration
		Gui, Font, cWhite
	} else if (fileexist(file_path_csv) and check_csv = "") {
		file_recycle2(file_path_csv)
		file_path_csv := ""
	} else {
		Gui, Font, cRed
		guicontrol, font, edittime
		guicontrol, , edittime,  Bookmarks <not loaded>
		guicontrol, move, edittime, w350
		guicontrol, hide, editedduration
		Gui, Font, cWhite
	}
return

time_current:
	paint_all := ""
	time_current := mpc_getTime2()
	loop, parse, % time_current
		{
		char := a_loopfield
		if (paint_all = 1)
			color := "cfcff00"
		else if (char = ":")
			color := "c101010"		
		else if (char = "0")
			color := "c101010"
		else {
			color := "cfcff00"
			paint_all := 1
		}
		gui, font, %color%
		guicontrol, font, timecurrent%a_index%
		guicontrol, , timecurrent%a_index%, %char%
		Gui, Font, cWhite
	}
return	


time_total:
	paint_all := ""
	settimer, time_total, off
	keywait, mbutton
	time_total := mpc_getDuration2()
	loop, parse, % time_total
		{
		char := a_loopfield
		if (paint_all = 1)
			color := "cf19c09"
		else if (char = ":")
			color := "c101010"		
		else if (char = "0")
			color := "c101010"
		else {
			color := "cf19c09"
			paint_all := 1
		}
		gui, font, %color%
		guicontrol, font, timetotal%a_index%
		GuiControl, , timetotal%a_index%, %char%
		Gui, Font, cWhite
	}	
	if (!file_path and !file_path_csv)
		guicontrol, hide, editedduration
	if (file_path) {
		Gui, Font, cWhite
		guicontrol, font, spacer
	} else {
		Gui, Font, c101010
		guicontrol, font, spacer
		Gui, Font, cWhite
	}
	settimer, time_total, on
return

