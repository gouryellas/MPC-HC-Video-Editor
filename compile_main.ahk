#ifwinactive, ahk_class MPC-BE
	MButton::edit_bookmarks("create")
#ifwinactive

basicGuiClose:
	msgbox, 4, , Delete source video and csv file?
	ifmsgbox, Yes
		{
		file_recycle(path_join(file_directory, file_name, file_extension_dot))
		file_recycle(file_path_csv,file_path_mkv)
	}
	WinGet, pid, PID, MPC-BE Video Editor v1.6
	PostMessage, 0x112, 0xF060,,, ahk_pid %pid%
	Process, Close, %pid%
return

edit_bookmarks(var:="") {
	global file_path
	global timer
	if (var = "erase") {
		gosub, start_steps
		gosub, erase_steps
    } else if (var = "create") {
		bookmark_started := 1
		gosub, start_steps
		gosub, create_steps
		gosub, show_steps
	} else if (var = "show") {
		gosub, read_steps
		gosub, show_steps
		if (timer = 1 and new_hotkey_loaded = "")
			gosub, seek_steps
	}
	return


	start_steps:
		wait(1)
		file_path := window_title("ahk_class MPC-BE")
		if regexmatch(file_path, "imO)MPC-BE\sx(32|64)\s(\d+\.\d+\.\d+)", num_match) {
			version := num_match[2]
			arch := num_match[1]
		}
		while (file_path = "MPC-BE x" . arch . " " . version . "" or file_path = "MPC-BE" or file_path = "MPC-BE Video Editor v1")
			file_path := window_title("ahk_class MPC-BE")
		file_path := regexreplace(file_path, "imO) - MPC-BE x" . arch . " " . version, "")
		splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
		file_directory := file_directory . "\"
		file_name := string_caseLower(file_name)
		file_extension_dot := "." . file_extension
		file_path_csv := strreplace(file_path, file_extension, "csv")
		current_time := mpc_getTime()
		if !fileexist(file_path_csv) {
			if (current_time = "00:00:00" or current_time = "" or current_time = "00:00:00 ")
				current_time := pretty_print := "00:00:01"
		}
		current_seconds := time_longToSec(current_time)
		current_timeShort := time_secToShort(current_seconds)
	return
	
	create_steps:
		global save_latest_time
		save_latest_time := time_shortToSec(save_latest_time)
		if (current_seconds <= save_latest_time) {
			msg("New bookmark must be greater than: " . time_secToAlt(save_latest_time))
			return
		}
		bookmark_started := 1
		edited_duration := 0
		if (file_path = "" or file_directory = "" or file_extension = "" or file_name = "") {
			msg("one or more variables are blank")
			return
		}
		if regexmatch(ffmpeg_time, "imO)(\d+:\d+:\d+)\s(\d+:\d+:\d+).?$", ffmpeg_time_last)
			ffmpeg_time_last := ffmpeg_time_last[1] . " " . ffmpeg_time_last[2]
		data := file_read(file_path_csv)
		if (current_seconds = 0) 
			current_seconds := 1
		if (data = "") {
			line_num := 1, bookmark_status := "incomplete"
			file_write(file_path_csv, current_seconds)
			pretty_print :=  time_secToShort(current_seconds)
			last_line := csv_lastline(file_path_csv)
		} else {
			last_line := csv_lastline(file_path_csv)
			if regexmatch(last_line, "imO)^(\d+)$", check_time) {
				checking_time := check_time[1]
				if ffmpeg_time != ""
					ffmpeg_time .= " " . time_secToLong(checking_time) . " " . current_time
				bookmark_status := "complete"
				if (line_num = "")
					line_num := 1
				file_write(file_path_csv, "," . current_seconds . ",Bookmark" . line_num)
				data := file_read(file_path_csv)
				Loop, parse, % data, `n
					{
					if regexmatch(a_loopfield, "imO)^(\d+)\,(\d+)\,Bookmark(\d+)$", line_time) {
						if (line_time[1] <= 9) 
							line_time[1] := SubStr(line_time[1], 1)
						difference_seconds := line_time[2] - line_time[1]
						edited_duration += difference_seconds
						save_duration := edited_duration
					} 
				}
				line_num := line_num + 1, set_time := current_time
				pretty_print .= " - " . time_secToShort(current_seconds) . " (" . time_secToAlt(difference_seconds) . ")"
			} else if regexmatch(last_line, "imO)^(\d+)\,(\d+)\,Bookmark(\d+)$", line_time) {
				line_num := line_time[3] + 1
				bookmark_status := "incomplete"
				file_write(file_path_csv, "`n" . current_seconds)
				pretty_print .= "`n" . time_secToShort(current_seconds)
			}
		}
	return
	
	read_steps:
		loop, parse, % data, `n
			{
			current_line := a_loopfield, line_num := a_index
			if regexmatch(current_line, "imO)^(\d+)$", line_time) {
				time1 := time_secToShort(line_time[1])
				bookmark_status := "incomplete"
				get_time := time1
				pretty_print .= "`n" . time1
			} else if regexmatch(current_line, "imO)(\d+)\,(\d+)\,Bookmark(\d+)", line_time) {
				if (line_time[3] = 1)
					ffmpeg_time := time_secToLong(line_time[1]) . " " . time_secToLong(line_time[2])
				else
					ffmpeg_time .= " " . time_secToLong(line_time[1]) . " " . time_secToLong(line_time[2])
				ffmpeg_time_last := time_secToLong(line_time[1]) . " " . time_secToLong(line_time[2])
				bookmark_status := "complete", time1 := time_secToShort(line_time[1]), time2 := time_secToShort(line_time[2])
				if (line_time[1] <= 9) 
					line_time[1] := SubStr(line_time[1], 1)
				difference_seconds := line_time[2] - line_time[1]
				edited_duration += difference_seconds
				save_duration := edited_duration
				pretty_print .= "`n" . time1 . " - " . time2 . " (" . time_secToAlt(difference_seconds) . ")"
				get_time := time2
			}
		}
	return
	
	show_steps:
		ui_destroy("basic")
		gui, basic:default
		gui, +owner +border -resize +maximizebox +sysmenu +caption +toolwindow +DPIScale +alwaysontop +lastfound
		gui, margin, 0, 0
		Gui, Color, 000000
		gui, Font, s12 cWhite Bold
		Menu, MyMenuBar, Add, Undo, undo
		Menu, MyMenuBar, Add, Clear, clear
		Menu, MyMenuBar, Add, Reload, reload 
		Menu, CSVMenu, Add, Open..., csv_open
		if (file_path_csv != "")
			Menu, CSVMenu, Add, Loaded: %file_path_csv%, csv_loaded
		Menu, MyMenuBar, Add, CSV, :CSVMenu
		Menu, HotkeyMenu, Add, Change..., hotkey
		if (activehotkey != "")
			Menu, HotkeyMenu, Add, Current: %activehotkey%, noaction
		else
			Menu, HotkeyMenu, Add, Current: MButton, noaction
		Menu, MyMenuBar, Add, Hotkey, :HotkeyMenu
		Menu, MyMenuBar, Add, Merge, split_merge
		Menu, MyMenuBar, Add, Split, split_merge
		Gui, Menu, MyMenuBar
		loop, parse, % pretty_print, `n
			{
			if a_loopfield != ""
				pretty_last_line := a_loopfield
		}
		buttoncount := 0
		Loop, parse, % pretty_print, `n
			{
			if (a_index = 1) {
				gui, Font, cYellow
				time_total := mpc_getTotalTime()
				time_total := time_longToAlt(time_total)
			}
			if (a_loopfield != "") {
				if (pretty_print != "") {
					ui_add_radio(, "xs y+m +gChecked +vCheckbox" . a_index)
					gui, Font, cWhite
					if regexmatch(a_loopfield, "imO)(.+)\s\-\s(.+)\s(\(.+\))", split) {
						global time_split1 := split[1]
						global time_split2 := split[2]
						save_latest_time := time_split2
						duration_split := split[3]
						gui, add, link, gseek_steps x+0 +center, <a id="a">%time_split1%</a> - <a id="b">%time_split2%</a> %duration_split%		
					} else {
						global time_split3 := a_loopfield
						save_latest_time := time_split3
						gui, add, link, gseek_steps x+0 , <a id="c">%time_split3%</a>
					}
					buttonCount++
					button%a_index% := a_loopfield
					checkbox%a_index% := button%a_index%
				} 
			} 
		}
		gui, Font, cYellow
		if (bookmark_status = "complete") {
			ui_add_text("cut length: " . time_secToAlt(edited_duration) . " / " . time_total, "x+0 yp+25")
			save_duration := edited_duration
		} else if (line_num = "1")
			ui_add_text("video duration: " . time_total, "x+0 yp+25")
		else
			ui_add_text("cut length: " . time_secToAlt(save_duration) . " / " . time_total, "x+0 yp+25")
		xpos := a_screenwidth - 1000
		gui, show, NA y140 x%xpos% w330, MPC-BE Video Editor v1.6
	return

	seek_steps:
		get_errorlevel := errorlevel
		total_time := mpc_getTotalTime(time_total)
		if (timer = 1) {
			prefix := special(get_time, total_time)
			get_clicked_time := get_time
			timer := 0
		} else if regexmatch(a_guicontrol, "imO)<a id=""a"">(.+)</a>\s\-\s<a id=""b"">(.+)</a>\s", get_clicked) {
			if get_errorlevel = "a"
				get_clicked_time := get_clicked[1]
			else if get_errorlevel = "b"
				get_clicked_time := get_clicked[2]
		} else if regexmatch(a_guicontrol, "imO)<a id=.c.>(.+)<.a>", get_clicked)
				get_clicked_time := get_clicked[1]
		prefix := special(get_clicked_time, total_time)
		num := number_countdigits(get_clicked_time)
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
		new_time := prefix . time[1] . time[2] . time[3] . ".000"
		window_activate("ahk_class MPC-BE")
		wait(1)
		file_path := window_title("ahk_class MPC-BE")
		if regexmatch(file_path, "imO)MPC-BE\sx(32|64)\s(\d+\.\d+\.\d+)", num_match) {
			version := num_match[2]
			arch := num_match[1]
		}
		while (file_path = "MPC-BE x" . arch . " " . version . "" or file_path = "MPC-BE" or file_path = "MPC-BE Video Editor v1")
			file_path := window_title("ahk_class MPC-BE")
		file_path := regexreplace(file_path, "imO) - MPC-BE x" . arch . " " . version, "")
		send("{control}{g}", "down")
		wait(1)
		window_waitActive("Go To...")
		send(new_time)
			send("{enter}")
	return

	noaction:

	return

	hotkey:
		Gui, HotkeyGUI:New
		Gui, HotkeyGUI:Add, Text, , Press desired hotkey:
		Gui, HotkeyGUI:Add, Hotkey, vSelectedHotkey, MButton
		Gui, HotkeyGUI:Add, Button, gHotkeyGUISubmit, OK
		Gui, HotkeyGUI:Add, Button, gHotkeyGUICancel, Cancel
		Gui, HotkeyGUI:Show, , Set new hotkey
	return

	HotkeyGUIClose:
	HotkeyGUIEscape:
		Gui, HotkeyGUI:Destroy
	Return

	HotkeyGUISubmit:
		Gui, HotkeyGUI:Default
		Gui, Submit, NoHide
		GuiControlGet, SelectedHotkey, , SelectedHotkey
		If (SelectedHotkey = "") {
			MsgBox, 16, Error, Hotkey cannot be empty. Please select a hotkey.
			Return 
		}
		If (ActiveHotkey != "")
			Hotkey, %ActiveHotkey%, MyHotkeyLabel, Off
		Hotkey, %SelectedHotkey%, MyHotkeyLabel, On
		MsgBox, Hotkey set to: %SelectedHotkey%
		ActiveHotkey := SelectedHotkey
		Gui, Destroy
		refreshhotkeymenu()
		ui_destroy("basic")
		pretty_print := ""
		data := file_read(file_path_csv)
		new_hotkey_loaded := 1
		edit_bookmarks("show")
	Return

	HotkeyGUICancel:
		Gui, HotkeyGUI:Destroy
	Return

	MyHotkeyLabel:
		edit_bookmarks("create")
	Return

	split_merge:
		if (bookmark_status = "complete") {
			global file_path
			save_ffmpeg_time := ffmpeg_time
			if RegExMatch(ffmpeg_time, "imO)(\d{2}:\d{2}:\d{2})(?=\d{2}:\d{2}:\d{2})", check_time)
				ffmpeg_time := RegExReplace(ffmpeg_time, "(\d{2}:\d{2}:\d{2})(?=\d{2}:\d{2}:\d{2})", "$1 ")
			timestampCount := 0
			Loop, Parse, ffmpeg_time, %A_Space%
				{
				if (a_loopfield != "") {
					timestampCount+=1
				}
			}
			if (number_oddeven(timestampCount) = "odd") {
				msg("There is an odd number of timestamps`r`r" . ffmpeg_time)
				return
			}
			ffmpeg_time := RegExReplace(ffmpeg_time, "(\d{2}:\d{2}:\d{2})(?=\d{2}:\d{2}:\d{2})", "$1 ")
			if (a_thismenuitem = "merge") {
				loop_count := 1
			} else if (a_thismenuitem = "split") {
				loop_count := ""
				count := 0
				pos := 1
				while pos := RegExMatch(ffmpeg_time, "imO)(\d{2}:\d{2}:\d{2})\s(\d{2}:\d{2}:\d{2})", match, pos)
					{
					loop_count++
					pos += StrLen(match[0])
				}
			}
			Loop, %loop_count% 
				{
				if (a_thismenuitem = "split") {
					file_path_create := " " . file_path . " " . file_directory . file_name . "[split" . a_index . "]" . file_extension_dot . " "
					if regexmatch(ffmpeg_time, "imO)(\d+:\d+:\d+\s\d+:\d+:\d+)", pairs) {
						new_ffmpeg_time := pairs[0]
						ffmpeg_time := regexreplace(ffmpeg_time, "imO)" . pairs[0])
						run(a_temp . "\compile_merge.bat" . file_path_create . new_ffmpeg_time)
					}
				} else {
					file_path_create := " " . file_path . " " . file_directory . file_name . "[done]" . file_extension_dot . " "
					run(a_temp . "\compile_merge.bat" . file_path_create . ffmpeg_time)
				}
				window_waitExist("ahk_exe cmd.exe")
				window_waitClose("ahk_exe cmd.exe")
			}	
		} else
			msg("You cannot split or merge files without completing the last bookmark.")
	return

	csv_open:
		FileSelectFile, chosen, 1, , Select a CSV file, CSV Files (*.csv)
		if (ErrorLevel)
			return
		global file_path_csv
		global data
		file_path_csv := chosen
		RefreshCSVMenu()
		ui_destroy("basic")
		pretty_print := ""
		new_csv_loaded := 1
		data := file_read(file_path_csv)
		timer := 1
		edit_bookmarks("show")
	return

	csv_loaded:
		run(file_path_csv)
	return
	
	reload:
		reload
	return
	
	undo:
		iniwrite, 1, %a_temp%\compile_data.ini, stored_data, bookmark_removed
		edit_bookmarks("erase")
		iniwrite, 0, %a_temp%\compile_data.ini, stored_data, bookmark_removed
		reload
	return
	
	clear:
		file_recycle(file_path_csv)
		send("{F5}")
		reload
	return
	
	checked:
		if RegExMatch(%a_guicontrol%, "imO)(.+)\s\-\s(.+)\s\(.+\)", checked_time) {
			ffmpeg_time := time_shortToLong(checked_time[1]) . " " . time_shortToLong(checked_time[2])
			file_path_check := file_directory . file_name . "[split1]" . file_extension_dot
			if fileexist(file_path_check) {
				file_path_check := file_directory . file_name . "[split2]" . file_extension_dot
				while fileexist(file_path_check) {
					if regexmatch(file_path_check, "imO).+(\[split(\d+)\]).+", cs_match)
						new_num := cs_match[2] + 1
					file_path_check := file_directory . file_name . "[split" . new_num . "]" . file_extension_dot
				}
			}
			file_path_create := file_path . " " . file_path_check
			run(a_temp . "\compile_merge.bat " . file_path_create . " " . ffmpeg_time)
		}
	return


	erase_steps:
		if regexmatch(ffmpeg_time, "imO)(.+)(\d{2}\:\d{2}\:\d{2})$", ffmpeg_remove_time)
			ffmpeg_time := ffmpeg_remove_time[1]
		if (file_path_csv != "") {
			if instr(csv_lastline(file_path_csv), "Bookmark")
				last_line := regexreplace(csv_lastline(file_path_csv), "imO)(\d+)(\,\d+\,Bookmark\d+)", "$2")
			else
				last_line := regexreplace(csv_lastline(file_path_csv), "imO)(\d+)", "$1")
			if (csv_linecount(file_path_csv) = 1) and if regexmatch(last_line, "imO)^(\d+)$") 
				file_delete(file_path_csv)
			else
				file_write(file_path_csv, strreplace(csv_data(file_path_csv), last_line), "w")
		}
	return

}
ClearBookmarkGUI() 
{
    Gui, basic:Destroy
}
RefreshHotkeyMenu()
{
    global file_path_csv
    Menu, HotkeyMenu, DeleteAll
    Menu, HotkeyMenu, Add, Change, hotkey
    if (file_path_csv != "")
        Menu, CSVMenu, Add, Loaded: %file_path_csv%, CSV_Loaded
    Menu, MyMenuBar, Add, CSV, :CSVMenu
}
RefreshCSVMenu()
{
    global file_path_csv
    Menu, CSVMenu, DeleteAll
    Menu, CSVMenu, Add, Open..., CSV_Open
    if (file_path_csv != "")
        Menu, CSVMenu, Add, Loaded: %file_path_csv%, CSV_Loaded
    Menu, MyMenuBar, Add, CSV, :CSVMenu
}