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
        file_path := current_video_path := mpc_getSource2()
        if (current_video_path != last_known_video_path) {
            if (g_manual_load_in_progress = 1) {
                last_known_video_path := current_video_path
                g_manual_load_in_progress := 0
                 return
            }
            file_path := current_video_path
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
    } else {
        if (last_known_video_path != "") {
            file_path := ""
            last_known_video_path := ""
            update_menus2()
			reload
        }
    }
return
