settimer, bookmark, 1000

bookmark:
	global file_path
	if WinExist("ahk_exe mpc-be64.exe") {
		reload_var := 1
		if window_active("ahk_class MPC-BE") {
			if (stop_var != 1) {
				wait(1)
				file_path := window_title("ahk_class MPC-BE")
				if regexmatch(file_path, "imO)MPC-BE\sx(32|64)\s(\d+\.\d+\.\d+)", num_match) {
					version := num_match[2]
					arch := num_match[1]
				}
				while (file_path = "MPC-BE x" . arch . " " . version . "" or file_path = "MPC-BE")
					file_path := window_title("ahk_class MPC-BE")
				file_path := regexreplace(file_path, "imO) - MPC-BE x" . arch . " " . version, "")
				splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
				file_directory := file_directory . "\"
				file_name := string_caseLower(file_name)
				file_extension_dot := "." . file_extension
				file_path_csv := strreplace(file_path, file_extension, "csv")
				if FileExist(file_path_csv) {
					if (bookmark_started != 1) {
						timer := 1
						data := file_read(file_path_csv)
						edit_bookmarks("show")
						stop_var := 1
					}
				}
			}
		}
	} else if (reload_var = 1)
		reload
return
