global last_known_video_path := ""
global g_manual_load_in_progress := 0
menu_show2("test") 

Initialize()

Initialize() {
    global
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
				GuiControl, , TimeTotal, %time_total%
			}
            last_known_video_path := current_video_path
			check_csv_path := check_matching_csv(file_path)
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
                total_time := mpc_getDuration2()
                if (total_time != 0 and total_time != "") {
                    time_total := time_longToAlt2(total_time)
                    gui, font, cWhite
                    guicontrol, font, videolength
                    GuiControl, , videolength, Video length:
                    gui, font, cYellow
                    guicontrol, font, timetotal
                    GuiControl, , TimeTotal, %time_total%
                    guicontrol, move, timetotal, x+97
                }               
                check_csv_path := check_matching_csv(file_path)
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
            update_menus2()
            gui, font, cRed
            guicontrol, font, videolength
            guicontrol, , videolength, No video loaded
			GuiControl, , TimeTotal
        }
    }
return
