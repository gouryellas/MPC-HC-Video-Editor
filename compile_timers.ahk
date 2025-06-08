settimer, bookmark, 1000
settimer, HoverTooltipCheck, 100

HoverTooltipCheck:
	global hgui
	WinGetPos,winx,winy, guiW, guiH, ahk_id %hGUI%
	MouseGetPos, xpos, ypos, id, control
	guiH := guiH + winy
    xPos := xpos - winX
    yyPos := (ypos - winY) + 141
	if (ypos > 177) and if (ypos < guiH) and if (xpos > 20) and if (xpos < 50)
			tooltip % "save this clip"
	else if regexmatch(control, "imO)Static(\d+)", static_num) {
		if (static_num[1] > 5) {
			tooltip % "jump to"
		} else
			tooltip % ""
	} else
		tooltip % ""
return

bookmark:
	global file_path
	if window_exist("MPC-BE") {
		if window_active("MPC-BE") {
			wait(1)
			file_path := window_title("MPC-BE")
			if regexmatch(file_path, "imO)MPC-BE\sx64\s(\d+\.\d+\.\d+)", num_match)
				version := num_match[1]
			while (file_path = "MPC-BE x64 " . version . "" or file_path = "MPC-BE")
				file_path := window_title("MPC-BE")
			file_path := regexreplace(file_path, "imO) - MPC-BE x64 " . version, "")
			splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
			file_directory := file_directory . "\"
			file_name := string_caseLower(file_name)
			file_extension_dot := "." . file_extension
			file_path_csv := strreplace(file_path, file_extension, "csv")
			if FileExist(file_path_csv) {
				if (bookmark_started != 1) {
					timer := 0
					data := file_read(file_path_csv)
					edit_bookmarks("show")
				}
				window_waitclose("MPC-BE")
				ui_destroy("basic")
				ui_destroy("basic2")
				if (bookmark_status = "incomplete") {
					msgbox,37, Error, Last bookmark is incomplete.
					ifmsgbox, cancel
						{
						reload
					}
					ifmsgbox, retry
						{
						run(file_path)
						reload
					}
				} else
					edit_video()
			}
		} 
	} 
	
return
