settimer, bookmark, 250
;settimer, file_change, 500

bookmark:
	if WinExist("ahk_class MPC-BE") {
		existed := 1
		clear := ""
		if window_active("ahk_class MPC-BE") {
			file_path := window_title("ahk_class MPC-BE")
			file_path := file_info(file_path)
			file_path_csv := RegExReplace(file_path, "(.+)\.[^.]+$", "$1.csv")
			if (file_path != last_file_path and last_file_path != "") {
				bookmark_started := ""
				last_file_path_csv := RegExReplace(last_file_path, "(.+)\.[^.]+$", "$1.csv")
				file_path_csv := RegExReplace(file_path, "(.+)\.[^.]+$", "$1.csv")
				new_video := 1
			}
			last_file_path := file_path
			if (FileExist(file_path_csv) and bookmark_started != "yes") {
				file_path := checks(file_path)
				timer := "on"
				file_show(file_path_csv)
				timer := "off"
				bookmark_started := "yes"
				new_video := 0
			} else if (new_video = 1) {
				menu_add("load", "file_path_csv")
				clear_gui(last_file_path_csv)
				new_video := 0
				file_path_csv := ""
				last_file_path_csv := ""
				bookmark_started := ""
			}
		}
	}
	if !WinExist("ahk_class MPC-BE") {
		if (clear = "" and existed != "") {
			clear_gui(file_path_csv)
			clear := "set"
			bookmark_started := ""
			file_path := ""
			file_path_csv :=
			menu_add("load", "file_path_csv")
		}
	}
return

file_change:
	global file_path, file_path_csv
	if (buffer_check = 1) {
		SetTimer, file_change, off
		buffer_check := 0
		window_waitActive("ahk_class MPC-BE")
	}
	if WinExist("ahk_class MPC-BE") {
		SetTimer, file_change, Off
		mpc_hwnd := WinExist() 
		original_hwnd := mpc_hwnd
		WinGetTitle, check_title, ahk_id %mpc_hwnd%
		now_title := check_title
		while (original_hwnd = newest_hwnd) {
			WinGetTitle, now_title, ahk_id %mpc_hwnd%
			newest_hwnd := WinExist(ahk_id %mpc_hwnd%)
		}
		file_path := now_title
		while WinExist(original_hwnd) {
			WinGetTitle, check_title, ahk_id %mpc_hwnd%		
		}
		mpc_hwnd := WinExist("ahk_class MPC-BE") 
		new_hwnd := mpc_hwnd
		if (original_hwnd != new_hwnd) {
			file_path := regexreplace(file_path, "imO) - MPC-BE x" . arch . " " . version, "")
			ext := file_extension(file_path)
			file_path_csv := strreplace(file_path,ext,"csv")
			if fileexist(file_path_csv) {
				timer := "on"
				data := file_read(file_path_csv)
				
			}
		}
	
	} else
		guicontrol, hide, MPC-BE Video Editor v2
	SetTimer, file_change, on
return
