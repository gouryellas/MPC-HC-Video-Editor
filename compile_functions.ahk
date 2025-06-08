edit_bookmarks(var:="") {
	global file_path
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
		gosub, seek_steps
	}
	return
	

	start_steps:
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
		current_time := mpc_getTime()
		if !fileexist(file_path_csv) {
			if (current_time = "00:00:00" or current_time = "" or current_time = "00:00:00 ")
				current_time := pretty_print := "00:00:01"
		}
		current_seconds := time_longToSec(current_time)
		current_timeShort := time_secToShort(current_seconds)
	return
	
	create_steps:
		bookmark_started := 1
		edited_duration := 0
		if (file_path = "" or file_directory = "" or file_extension = "" or file_name = "") 
			error("one or more variables are blank")
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
				if (check_time[1] < current_seconds)
					file_write(file_path_csv, "," . current_seconds . ",Bookmark" . line_num)
				else {
					msgbox,37, Error, Bookmark cannot start at a time less than the previous one.
					ifmsgbox, cancel
						{
						reload
					}
					ifmsgbox, retry
						{
						reload
					}
				}
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
		font_size := (line_num <= 40) ? 12
			: (line_num <= 45) ? 12
			: (line_num <= 50) ? 12
			: (line_num <= 55) ? 11
			: (line_num <= 60) ? 11
			: (line_num <= 65) ? 11
			: (line_num > 70)  ? 10
			: font_size
		ui_width := 400
		button_divide := ui_width * 0.5
		ui_destroy("basic")
		gui, basic:default
		gui, +owner +border -resize -maximizebox -sysmenu -caption -toolwindow -dpiscale +alwaysontop +lastfound
		gui, margin, 0, 0
		Gui, Color, 000000
		button_size := 5
		ui_width := 500	
		button_size := number_roundWhole(ui_width / button_size)
		button_position += button_size
		ui_add_picture("C:\users\Chris\Desktop\new working files\working\images\undo.png", "x0 +w95 +h50 +gundoVideo")
		ui_add_picture("C:\users\Chris\Desktop\new working files\working\images\clear.png", "xp+95 +w109 +h50 +gclearVideo")
		ui_add_picture("C:\users\Chris\Desktop\new working files\working\images\reload.png", "xp+109 +w123 +h50 +greloadVideo")
		ui_add_picture("C:\users\Chris\Desktop\new working files\working\images\csv.png", "xp+123 +w75 +h50 +gopenCSV")
		ui_add_picture("C:\users\Chris\Desktop\new working files\working\images\close.png", "xp+75 +w100 +h50 +gquitVideo")
		gui, Font, s%font_size% cWhite Bold
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
				button_width := "+w" . ui_width
				if (pretty_print != "") {
					ui_add_radio(, "x15 +h30 +w30 +gChecked +vCheckbox" . a_index)
					gui, Font, cWhite
					if bookmark_status = "complete"
						ui_add_text(a_loopfield, "xs+10 +w500 +gseek_steps +vButton" . a_index . " hwndhButton" . a_index)
					else
						ui_add_text(a_loopfield, "xp+10 +w500 yp+0 +gseek_steps +vButton" . a_index . " hwndhButton" . a_index)
					buttonCount++
					button%a_index% := a_loopfield
					checkbox%a_index% := button%a_index%
				} 
			} 
		}
		gui, Font, cAqua
		ui_width := 500	
		xpos := a_screenwidth - ui_width - 50
		pull := csv_lastline(file_path_csv)
		if regexmatch(pull, "imO)(\d+),(\d+)", check) {
			if (check[1] > check[2])
				gui, Font, cRed
        }
		gui, show, NA y140 x%xpos%, basic
		global hgui
		hGUI := WinExist() 
		wingetpos, x, y, width, height, basic
		ui_destroy("basic2")
		gui, basic2:default
		gui, +owner +border -resize -maximizebox -sysmenu -caption -toolwindow -dpiscale +alwaysontop +lastfound
		gui, margin, 0, 0
		Gui, Color, 000000
		gui, Font, s%font_size% cAqua Bold
		if (bookmark_status = "complete") {
			ui_add_text(time_secToAlt(edited_duration) . " / " . time_total, "x20 y10 +center +w500 +h60")
			save_duration := edited_duration
		} else if (line_num = "1")
			ui_add_text(time_total, "y10 +w500 +center +h60")
		else
			ui_add_text(time_secToAlt(save_duration) . " / " . time_total, "y10 +w500 +center +h60")
		wingetpos, x, y, w, h, basic
		ypos := h + 141
		gui, show, NA y%ypos% x%xpos%, basic2
		if (timer = 1)
			gosub, seek_steps
	return
	
	openCSV:
		run(file_path_csv)
	return
	
	reloadVideo:
		reload
	return
	
	quitVideo:
		window_kill("MPC-BE")
		ui_destroy("basic")
		ui_destroy("basic2")
		reload
	return
	
	undoVideo:
		iniwrite, 1, C:\users\Chris\Desktop\new working files\working\mpcbe-video-clipper-v1\compile_data.ini, stored_data, bookmark_removed
		edit_bookmarks("erase")
		iniwrite, 0, C:\users\Chris\Desktop\new working files\working\mpcbe-video-clipper-v1\compile_data.ini, stored_data, bookmark_removed
		reload
	return
	
	clearVideo:
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
			run("C:\users\Chris\Desktop\new working files\working\mpcbe-video-clipper-v1\compile_merge.bat " . file_path_create . " " . ffmpeg_time)
		}
	return
	
	seek_steps:
		if (a_guievent = "Normal" && a_guicontrol ~= "^(Button\d+)$") {
			if regexmatch(a_guicontrol, "imO)Static(\d+)", update_button)
				control := "Static" . update_button[1] - 1
			size := control_getSize(control, "basic")
			width := size.width // 2
			coordmode, mouse, relative
			mousegetpos, xpos, ypos, id, control
			coordmode, mouse, screen
			get_time := regexreplace(regexreplace(%a_guicontrol%, "imO)\s\(.+\)"), "imO)\[\d+\]\s")
			if regexmatch(get_time, "imO)(.+)\s\-\s(.+)", time) {
				get_time1 := time[1], get_time2 := time[2]
				if (xpos > width) 
					get_time := get_time2
				else 
					get_time := get_time1
			} else if regexmatch(get_time, "imO)(.+)", time) {
				get_time := time[1]
				bookmark_status := "incomplete"
			}
		}	
		window_activate("MPC-BE")
		file_path := window_title("MPC-BE")
		if regexmatch(file_path, "imO)MPC-BE\sx64\s(\d+\.\d+\.\d+)", num_match)
			version := num_match[1]
		file_path := regexreplace(file_path, "imO) - MPC-BE x64 " . version, "")
		total_time := mpc_getTotalTime(time_total)
		new_time := time_LongToBookmark(get_time, total_time)
		send("{control}{g}", "down")
		wait(1)
		window_waitActive("Go To...")
		send(new_time)
		send("{enter}")
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
	
edit_video() {
	global file_path
		Loop, Parse, ffmpeg_time, %A_Space%
		{
		if (a_loopfield != "") {
			timestampCounts+=1
		}
	}
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
	result := number_oddeven(timestampCount)
	if (result = "odd") {
		msgbox,37, Error, There is an odd number of timestamps`r`r%ffmpeg_time%
		ifmsgbox, cancel
			{
			reload
		}
		ifmsgbox, retry
			{
			reload
		}
	}
	pretty_print := ""
	ui_destroy("choose")
	ui_setup("choose")
	ui_color("+wcblack")
	ui_font("+white +s13")
	ui_font("+black")
	ui_add_button("run", "x0 +h30 +vRun")
	GuiControl, +Tip, Run, Run editing process.
	ui_add_button("?", "+vFormatName +gFormat_Name cYellow +w40 +h30 x+ Default")
	GuiControl, +Tip, FormatName, Format + update filename.
	ui_add_edit(file_name, "+vChangedName x+ w600 r1 WantReturn")
	ui_add_button("X", "+gX x+0 +h30 +vDelete")
	GuiControl, +Tip, Delete, Delete video + csv file.
	ui_add_button("quit", "x+ +h30 +vQuit")
	GuiControl, +Tip, Quit, Close this window.
	ui_font("+white")
	ui_add_checkbox("[done]", "+vSelectedDone +gToggleDone x130")
	ui_add_checkbox("delete", "+vSelectedDelete x+10 Disabled")
	GuiControl, +Tip, SelectedDelete, Delete original video after editing.
	ui_add_checkbox("split", "+vSelectedSplit +gToggleSplit x+10")
	GuiControl, +Tip, SelectedSplit, Create separate video for each bookmark.
	ui_options("+alwaysontop")
	ui_show("choose")
	if regexmatch(file_directory, "imO)\s|\)|\()") {
		file_directory_new := regexreplace(file_directory, "imO)(\s|\)|\()")
		file_move(file_directory, file_directory_new)
		file_directory := file_directory_new
	}
	gosub, format_name
	return

	x:
		file_recycle(file_path_csv,file_path)
		reload
	return

	edit:
		gui, submit, nohide
		guicontrolget, file_name_old, , changedname
		file_name := rename_file(file_name_old)
		guicontrol, text, changedname, %file_name%
		file_move(file_path, path_join(file_directory, file_name, file_extension_dot))
		file_path := path_join(file_directory, file_name, file_extension_dot)
		file_move(file_path_csv, path_join(file_directory, file_name, ".csv"))
		file_path_csv := path_join(file_directory, file_name, ".csv")
	return

	quit:
		ffmpeg_time := ""
		reload
	return

	ToggleDone:
		Gui, Submit, NoHide
		if (SelectedDone = 1) {
			GuiControl, Disable, SelectedSplit
			GuiControl, Enable, selectedDelete
			GuiControl, +BackgroundGreen, run
		} else {
			GuiControl, Disable, selectedDelete
			GuiControl, Enable, SelectedSplit
		}
	return

	ToggleSplit:
		Gui, Submit, NoHide
		if (SelectedSplit = 1) {
			GuiControl, +BackgroundGreen, run
			GuiControl, Disable, SelectedDone
			GuiControl, Enable, selectedDelete
		} else {
			GuiControl, Enable, SelectedDone
			GuiControl, Disable, selectedDelete
		}
	return

	format_name:
		guicontrolget, file_name_old, , changedname
		file_name := rename_file(file_name_old)
		guicontrol, text, changedname, %file_name%
		if FileExist(path_join(file_directory, file_name, "[done]" . file_extension_dot))
			gui, color, , red
		else {	
			file_move(file_path, path_join(file_directory,file_name,file_extension_dot))
			file_path := path_join(file_directory,file_name,file_extension_dot)
			file_move(file_path_csv, path_join(file_directory,file_name,".csv"))
			file_path_csv := path_join(file_directory,file_name,".csv")
			gui, color, , green
		}
	return

	run:
		gui, submit
		ffmpeg_time := RegExReplace(ffmpeg_time, "(\d{2}:\d{2}:\d{2})(?=\d{2}:\d{2}:\d{2})", "$1 ")
		clipboard := ffmpeg_time
		file_path_create_done := " " . file_path . " " . file_directory . file_name . "[done]" . file_extension_dot . " "
		if (selectedDone = 1) {
			run("C:\users\Chris\Desktop\new working files\working\mpcbe-video-clipper-v1\compile_merge.bat" . file_path_create_done . ffmpeg_time)
			window_waitActive("ahk_exe cmd.exe")
			window_waitNotActive("ahk_exe cmd.exe")
		}
		if (selectedSplit = 1) {
			Loop {
				if regexmatch(ffmpeg_time, "imO)(\d+:\d+:\d+\s\d+:\d+:\d+)", pairs) {
					file_path_create := " " . file_path . " " . file_directory . file_name . "[split" . a_index . "]" . file_extension_dot . " "
					new_ffmpeg_time := pairs[0]
					ffmpeg_time := regexreplace(ffmpeg_time, "imO)" . pairs[0])
					run("C:\users\Chris\Desktop\new working files\working\mpcbe-video-clipper-v1\compile_merge.bat" . file_path_create . new_ffmpeg_time)
					window_waitActive("ahk_exe cmd.exe")
					window_waitNotActive("ahk_exe cmd.exe")
				} else
					break
			}
		}
		if (selectedDone = 0 AND selectedSplit = 0) {
			winhide, choose
			error("nothing selected")
			winshow, choose
			return
		}
		if (selectedDelete = 1)
			file_recycle(path_join(file_directory, file_name, file_extension_dot))
		file_recycle(file_path_csv,file_path_mkv)
		ffmpeg_time := ""
		ffmpeg_time_last := ""
		reload
	return
}

rename_file(var) {
	sites := "(naughtyamerica|naughty america|foxxedup|bangbros|bang bros|teenslovehugecocks|brazzers|pervcity|realitykings|julesjordan|evilangel|mrstrokesxxx|tiny4k|premiumbukakke|putalocura|lubed|21sexutry|lethalhardcore|manojob|legalporno|analized|exotic4k|hdlove|hugecockgloryholes|pawg|onlyfans|peternorth|throated|cumbang)"
	cats := "(amateur|blowbang|fucking|compilation|cumshot|voodoo|clover|strokes|black|3d|vr|burbanks|compilations|danny|darya|parker|salon|3some|4some|gloryhole|keiran|keiran lee|glory hole|gangbang|gang bang|blow bang)"
	stars1 := "Chris(\s|\-|\_)Strokes|Aaliyah(\s|\-|\_)Hadid|Aaliyah(\s|\-|\_)Love|Abbey(\s|\-|\_)Brooks|Abbie(\s|\-|\_)Cat|Abby(\s|\-|\_)Cross|Abby(\s|\-|\_)Lee(\s|\-|\_)Brazil|Abella(\s|\-|\_)Danger|Abigail(\s|\-|\_)Day|Abigail(\s|\-|\_)Fraser|Abigail(\s|\-|\_)Mac|Abigaile(\s|\-|\_)Johnson|Adam(\s|\-|\_)Sharps|Adel(\s|\-|\_)Morel|Adele(\s|\-|\_)Sunshine|Adira(\s|\-|\_)Allure|Adria(\s|\-|\_)Rae|Adriana(\s|\-|\_)Chechik|Adriana(\s|\-|\_)Maya|Adrianna(\s|\-|\_)Luna|Adrianna(\s|\-|\_)Nicole|Aften(\s|\-|\_)Opal|Aiden(\s|\-|\_)Ashley|Aiden(\s|\-|\_)Starr|Aidra(\s|\-|\_)Fox|AJ(\s|\-|\_)Applegate|Alaina(\s|\-|\_)Kristar|Alan(\s|\-|\_)Stafford|Alana(\s|\-|\_)Cruise|Alana(\s|\-|\_)Evans|Alanah(\s|\-|\_)Rae|Alberto(\s|\-|\_)Blanco|Alec(\s|\-|\_)Knight|Alecia(\s|\-|\_)Fox|Alektra(\s|\-|\_)Blue|Alena(\s|\-|\_)Croft|Aleska(\s|\-|\_)Diamond|Alessandra(\s|\-|\_)Jane|Aletta(\s|\-|\_)Ocean|Alex(\s|\-|\_)Blake|Alex(\s|\-|\_)Chance|Alex(\s|\-|\_)Coal|Alex(\s|\-|\_)De(\s|\-|\_)La(\s|\-|\_)Flor|Alex(\s|\-|\_)Gonz|Alex(\s|\-|\_)Grey|Alex(\s|\-|\_)Jett|Alex(\s|\-|\_)Legend|Alex(\s|\-|\_)Tanner|Alex(\s|\-|\_)Victor|Alexa(\s|\-|\_)Grace|Alexa(\s|\-|\_)Nicole|Alexa(\s|\-|\_)Nova|Alexa(\s|\-|\_)Tomas|Alexa(\s|\-|\_)Von(\s|\-|\_)Tess|Alexis(\s|\-|\_)Adams|Alexis(\s|\-|\_)Blond|Alexis(\s|\-|\_)Brill|Alexis(\s|\-|\_)Crystal|Alexis(\s|\-|\_)Fawx|Alexis(\s|\-|\_)Ford|Alexis(\s|\-|\_)Grace|Alexis(\s|\-|\_)Monroe|Alexis(\s|\-|\_)Silver|Alexis(\s|\-|\_)Tae|Alexis(\s|\-|\_)Texas|Alia(\s|\-|\_)Janine|Alice(\s|\-|\_)Green|Alice(\s|\-|\_)March|Alice(\s|\-|\_)Romain|Alicia(\s|\-|\_)Silver|Alina(\s|\-|\_)Li|Alina(\s|\-|\_)Lopez|Alina(\s|\-|\_)West|Alison(\s|\-|\_)Rey|Alison(\s|\-|\_)Star|Alison(\s|\-|\_)Tyler|Alix(\s|\-|\_)Lynx|Aliya(\s|\-|\_)Brynn|Alli(\s|\-|\_)Rae|Alli(\s|\-|\_)Rea|Allie(\s|\-|\_)Haze|Allie(\s|\-|\_)James|Allison(\s|\-|\_)Moore|Ally(\s|\-|\_)Breelsen|Ally(\s|\-|\_)Kay|Ally(\s|\-|\_)Style|Alura(\s|\-|\_)Jenson|Alysa(\s|\-|\_)Gap|Alyssa(\s|\-|\_)Branch|Alyssa(\s|\-|\_)Lynn|Alyssa(\s|\-|\_)Reece|Alyssia(\s|\-|\_)Kent|Amabella|Amai(\s|\-|\_)Liu|Amanda(\s|\-|\_)Tate|Amanda(\s|\-|\_)Verhooks|Amara(\s|\-|\_)Romani|Amarna(\s|\-|\_)Miller|Amber(\s|\-|\_)Jayne|Amber(\s|\-|\_)Lynn|Amber(\s|\-|\_)Lynn(\s|\-|\_)Bach|Amber(\s|\-|\_)Rayne|Amia(\s|\-|\_)Miley|Amirah(\s|\-|\_)Adara|Amy(\s|\-|\_)Anderssen|Amy(\s|\-|\_)Brooke|Amy(\s|\-|\_)Goodhead|Amy(\s|\-|\_)Green|Amy(\s|\-|\_)Reid|Ana(\s|\-|\_)Foxxx|Ana(\s|\-|\_)Luz|Ananta(\s|\-|\_)Shakti|Anastasia(\s|\-|\_)Morna|Andi(\s|\-|\_)Land|Andreea(\s|\-|\_)Mantea|Andy(\s|\-|\_)San(\s|\-|\_)Dimas|Anetta(\s|\-|\_)Keys|Angel(\s|\-|\_)Dark|Angel(\s|\-|\_)Long|Angel(\s|\-|\_)Piaff|Angel(\s|\-|\_)Rivas|Angel(\s|\-|\_)Smalls|Angel(\s|\-|\_)Wicky|Angela(\s|\-|\_)White|Angelica(\s|\-|\_)Heart|Angelica(\s|\-|\_)Saige|Angelika(\s|\-|\_)Grays|Angelina(\s|\-|\_)Chung|Angelina(\s|\-|\_)Crow|Angelina(\s|\-|\_)Torres|Angelina(\s|\-|\_)Valentine|Angell(\s|\-|\_)Summers|Angelo(\s|\-|\_)Godshack|Ani(\s|\-|\_)Black(\s|\-|\_)Fox|Anie(\s|\-|\_)Darling|Anikka(\s|\-|\_)Albrite|Anina(\s|\-|\_)Silk|Anissa(\s|\-|\_)Kate|Anita(\s|\-|\_)Bellini|Anita(\s|\-|\_)Pearl|Anita(\s|\-|\_)Queen|Ann(\s|\-|\_)Marie|Anna(\s|\-|\_)Bell|Anna(\s|\-|\_)Bell(\s|\-|\_)Peaks|Anna(\s|\-|\_)De(\s|\-|\_)Ville|Anna(\s|\-|\_)Joy|Anna(\s|\-|\_)Morna|Anna(\s|\-|\_)Rose|Annabelle(\s|\-|\_)Lee|Annette(\s|\-|\_)Schwarz|Annie(\s|\-|\_)Cruz|Anny(\s|\-|\_)Aurora|Anthony(\s|\-|\_)Rosano|Antonia(\s|\-|\_)Sainz|Anya(\s|\-|\_)Ivy|Anya(\s|\-|\_)Olsen|Apolonia(\s|\-|\_)Lapiedra|April(\s|\-|\_)O(\s|\-|\_)Neil|Aria(\s|\-|\_)Alexander|Aria(\s|\-|\_)Giovanni|Ariana(\s|\-|\_)Marie|Arianna(\s|\-|\_)Sinn|Ariel(\s|\-|\_)X|Ariella(\s|\-|\_)Ferrera|Aruna(\s|\-|\_)Aghora|Arya(\s|\-|\_)Fae|Asa(\s|\-|\_)Akira|Ash(\s|\-|\_)Hollywood|Ashley(\s|\-|\_)Adams|Ashley(\s|\-|\_)Bulgari|Ashley(\s|\-|\_)Fires|Ashley(\s|\-|\_)George|Ashley(\s|\-|\_)Graham|Ashley(\s|\-|\_)Lane|Ashli(\s|\-|\_)Orion|Ashlyn(\s|\-|\_)Molloy|Ashlyn(\s|\-|\_)Rae|Ashlynn(\s|\-|\_)Brooke|Ashlynn(\s|\-|\_)Leigh|Aspen(\s|\-|\_)Ora|Asphyxia(\s|\-|\_)Noir|Athina(\s|\-|\_)Love|Aubrey(\s|\-|\_)Addams|Aubrey(\s|\-|\_)Kate|Audrey(\s|\-|\_)Bitoni|Audrey(\s|\-|\_)Hollander|Audrey(\s|\-|\_)Rose|August(\s|\-|\_)Ames|August(\s|\-|\_)Taylor|Aurora(\s|\-|\_)Snow|Austin(\s|\-|\_)Kincaid|Autumn(\s|\-|\_)Jade|Ava(\s|\-|\_)Addams|Ava(\s|\-|\_)Dalush|Ava(\s|\-|\_)Devine|Ava(\s|\-|\_)Taylor|Avalon|Avi(\s|\-|\_)Love|Avril(\s|\-|\_)Hall|Avril(\s|\-|\_)Sun|Avy(\s|\-|\_)Scott|Baby(\s|\-|\_)Doll|Bailey(\s|\-|\_)Brooke|Bailey(\s|\-|\_)Jay|Barbie(\s|\-|\_)Cummings|Barra(\s|\-|\_)Brass|Barry(\s|\-|\_)Scott|Beata(\s|\-|\_)Undine|Bella(\s|\-|\_)Anne|Bella(\s|\-|\_)Blond|Bella(\s|\-|\_)Rolland|Bella(\s|\-|\_)Rose|Bella(\s|\-|\_)Rossi|Belle(\s|\-|\_)Claire|Belle(\s|\-|\_)Noire|Beretta(\s|\-|\_)James|Bethany(\s|\-|\_)Benz|Bettina(\s|\-|\_)DiCapri|Beverly(\s|\-|\_)Hills|Bianca(\s|\-|\_)Breeze|Bibi(\s|\-|\_)Fox|Bibi(\s|\-|\_)Noel|Bill(\s|\-|\_)Bailey|Billie(\s|\-|\_)Star|Billy(\s|\-|\_)Glide|Black(\s|\-|\_)Angelika|Black(\s|\-|\_)Fox|Blair(\s|\-|\_)Williams|Blake(\s|\-|\_)Eden|Blanche(\s|\-|\_)Bradburry|Blond(\s|\-|\_)Angel|Blond(\s|\-|\_)Dream|Blue(\s|\-|\_)Angel|Bobbi(\s|\-|\_)Dylan|Bobbi(\s|\-|\_)Starr|Bonnie(\s|\-|\_)Kinz|Bonnie(\s|\-|\_)Rotten|Boroka(\s|\-|\_)Borres|Brad(\s|\-|\_)Armstrong|Brad(\s|\-|\_)Knight|Brandi(\s|\-|\_)Love|Brandy(\s|\-|\_)Aniston|Brandy(\s|\-|\_)Smile|Brandy(\s|\-|\_)Talore|Breanne(\s|\-|\_)Benson|Bree(\s|\-|\_)Daniels|Bree(\s|\-|\_)Olson|Brett(\s|\-|\_)Rossi|Briana(\s|\-|\_)Banks|Briana(\s|\-|\_)Blair|Briana(\s|\-|\_)Lee|Brick(\s|\-|\_)Danger|Bridgette(\s|\-|\_)B|Britney(\s|\-|\_)Amber|Britney(\s|\-|\_)Angel|Britney(\s|\-|\_)Stevens|Brittany(\s|\-|\_)Andrews|Brittany(\s|\-|\_)Bardot|Brittany(\s|\-|\_)Shae|Brook(\s|\-|\_)Little|Brooke(\s|\-|\_)Banner|Brooke(\s|\-|\_)Belle|Brooke(\s|\-|\_)Haven|Brooke(\s|\-|\_)Wylde|Brooklyn(\s|\-|\_)Chase|Brooklyn(\s|\-|\_)Lee|Bruce(\s|\-|\_)Venture|Bruno(\s|\-|\_)Dickemz|Bruno(\s|\-|\_)SX|Bryan(\s|\-|\_)Gozzling|Bryci|Brynn(\s|\-|\_)Tyler|Bryoni(\s|\-|\_)Kate|Bunny(\s|\-|\_)Blond|Busty(\s|\-|\_)Bliss|Butterfly|Byron(\s|\-|\_)Long|Cadence(\s|\-|\_)Lux|Cadey(\s|\-|\_)Mercury|Cali(\s|\-|\_)Carter|Callie(\s|\-|\_)Calypso|Cameron(\s|\-|\_)Canada|Cameron(\s|\-|\_)Dee|Candee(\s|\-|\_)Licious|Candi(\s|\-|\_)Love|Candice(\s|\-|\_)Brielle|Candice(\s|\-|\_)Dare|Candice(\s|\-|\_)Luca|Candy(\s|\-|\_)Alexa|Candy(\s|\-|\_)Licious|Candy(\s|\-|\_)Manson|Candy(\s|\-|\_)Monroe|Candy(\s|\-|\_)Sweet|Capri(\s|\-|\_)Anderson|Capri(\s|\-|\_)Cavanni|Carla(\s|\-|\_)Cox|Carli(\s|\-|\_)Banks|Carlosinka|Carmella(\s|\-|\_)Bing|Carmen(\s|\-|\_)Caliente|Carmen(\s|\-|\_)Callaway|Carmen(\s|\-|\_)Croft|Carmen(\s|\-|\_)Gemini|Carmen(\s|\-|\_)McCarthy|Carmen(\s|\-|\_)Valentina|Carol(\s|\-|\_)Goldnerova|Carolina(\s|\-|\_)Abril|Carolina(\s|\-|\_)Sweets|Carolyn(\s|\-|\_)Reese|Carter(\s|\-|\_)Cruise|Casey(\s|\-|\_)Calvert|Casey(\s|\-|\_)Cumz|Casey(\s|\-|\_)Kisses|Casi(\s|\-|\_)James|Cassandra(\s|\-|\_)Calogera|Cassandra(\s|\-|\_)Cruz|Cassandra(\s|\-|\_)Nix|Cassidy(\s|\-|\_)Banks|Cassidy(\s|\-|\_)Klein|Cassie(\s|\-|\_)Fire|Cassie(\s|\-|\_)Laine|Catalina(\s|\-|\_)Cruz|Cate(\s|\-|\_)Harrington|Cathy(\s|\-|\_)Heaven|Cayenne(\s|\-|\_)Klein|Cayla(\s|\-|\_)Lyons|CeCe(\s|\-|\_)Capella|CeCe(\s|\-|\_)Stone|Celeste(\s|\-|\_)Star|Celine(\s|\-|\_)Noiret|Chad(\s|\-|\_)Alva|Chad(\s|\-|\_)Diamond|Chad(\s|\-|\_)Rockwell|Chad(\s|\-|\_)White|Chanel(\s|\-|\_)Preston|Chanel(\s|\-|\_)Santini|Chanell(\s|\-|\_)Heart|Chanta(\s|\-|\_)Rose|Charisma|Charisma(\s|\-|\_)Cappelli|Charity|Charity(\s|\-|\_)Bangs|Charlee(\s|\-|\_)Chase|Charles(\s|\-|\_)Dera|Charley(\s|\-|\_)Chase|Charlie(\s|\-|\_)Dean|Charlie(\s|\-|\_)Laine|Charlie(\s|\-|\_)Mac|Charlotte(\s|\-|\_)Cross|Charlotte(\s|\-|\_)Sartre|Charlotte(\s|\-|\_)Springer|Charlotte(\s|\-|\_)Stokely|Charlotte(\s|\-|\_)Vale|Charlyse(\s|\-|\_)Bella|Charmane(\s|\-|\_)Star|Chase(\s|\-|\_)Ryder|Chastity(\s|\-|\_)Lynn|Chayse(\s|\-|\_)Evans|Cherie(\s|\-|\_)Deville|Cherokee|Cherry(\s|\-|\_)Jul|Cherry(\s|\-|\_)Kiss|Cherry(\s|\-|\_)Torn|Chessie(\s|\-|\_)Kay|Cheyenne(\s|\-|\_)Jewel|Chikita|Chloe(\s|\-|\_)Amour|Chloe(\s|\-|\_)Camilla|Chloe(\s|\-|\_)Cherry|Chloe(\s|\-|\_)Foster|Chloe(\s|\-|\_)Temple|Chloe(\s|\-|\_)Vevrier|Choky(\s|\-|\_)Ice|Chris(\s|\-|\_)Charming|Chris(\s|\-|\_)Johnson|Chris(\s|\-|\_)Strokes|Chrissy(\s|\-|\_)Fox|Christen(\s|\-|\_)Courtney|Christian(\s|\-|\_)Clay|Christian(\s|\-|\_)XXX|Christiana(\s|\-|\_)Cinn|Christie(\s|\-|\_)Stevens|Christina(\s|\-|\_)Bella|Christina(\s|\-|\_)Carter|Christina(\s|\-|\_)Lee|Christine(\s|\-|\_)Diamond|Christy(\s|\-|\_)Mack|Christy(\s|\-|\_)Marks|Cindy(\s|\-|\_)Dollar|Cindy(\s|\-|\_)Hope|Cindy(\s|\-|\_)Shine|Cindy(\s|\-|\_)Starfall|Cipriana|Claire(\s|\-|\_)Adams|Claire(\s|\-|\_)Dames|Claire(\s|\-|\_)Robbins|Clara(\s|\-|\_)G|Claudia(\s|\-|\_)Mac|Claudia(\s|\-|\_)Macc|Claudia(\s|\-|\_)Rossi|Claudia(\s|\-|\_)Valentine|Clea(\s|\-|\_)Gaultier|Coco(\s|\-|\_)De(\s|\-|\_)Mal|Codey(\s|\-|\_)Steele|Connie(\s|\-|\_)Carter|Conny(\s|\-|\_)Carter|Corinna(\s|\-|\_)Blake|Cory(\s|\-|\_)Chase|Courtney(\s|\-|\_)Cummz|Courtney(\s|\-|\_)Taylor|Crystal(\s|\-|\_)Clear|Crystal(\s|\-|\_)Nicole|Crystal(\s|\-|\_)Rush|Curvy(\s|\-|\_)Claire|Cynthia(\s|\-|\_)Vellons|Cytherea|Dahlia(\s|\-|\_)Sky|Daisy(\s|\-|\_)Ducati|Daisy(\s|\-|\_)Dukes|Daisy(\s|\-|\_)Marie|Daisy(\s|\-|\_)Stone|Daisy(\s|\-|\_)Summers|Daisy(\s|\-|\_)Watts|Dakota(\s|\-|\_)James|Dakota(\s|\-|\_)Skye|Damon(\s|\-|\_)Dice|Dana(\s|\-|\_)DeArmond|Dana(\s|\-|\_)Lightspeed|Dana(\s|\-|\_)Vespoli|Dana(\s|\-|\_)Weyron|Dane(\s|\-|\_)Cross|Dani(\s|\-|\_)Daniels|Dani(\s|\-|\_)Jensen|Danica(\s|\-|\_)Collins|Danica(\s|\-|\_)Dillon|Danielle(\s|\-|\_)Delaunay|Danielle(\s|\-|\_)Maye|Danny(\s|\-|\_)D|Danny(\s|\-|\_)Mountain|Danny(\s|\-|\_)Wylde|Dante(\s|\-|\_)Colle|Daphne(\s|\-|\_)Klyde|Daphne(\s|\-|\_)Rosen|Darcia(\s|\-|\_)Lee|Darcie(\s|\-|\_)Dolce|Darcy(\s|\-|\_)Tyler|Daria(\s|\-|\_)Glower|Darla(\s|\-|\_)Crane|Darryl(\s|\-|\_)Hanah|Dasha|Dava(\s|\-|\_)Foxx|David(\s|\-|\_)Loso|David(\s|\-|\_)Perry|Daya(\s|\-|\_)Knight|Dayna(\s|\-|\_)Vendetta|Deauxma|Debbie(\s|\-|\_)White|Debora(\s|\-|\_)A(\s|\-|\_)Kelly|Dee(\s|\-|\_)Delmar|Dee(\s|\-|\_)Siren|Dee(\s|\-|\_)Williams|Delilah(\s|\-|\_)Strong|Denisa(\s|\-|\_)Heaven|Derrick(\s|\-|\_)Pierce|Desirae|Destiny(\s|\-|\_)Dixon|Desyra(\s|\-|\_)Noir|Devon|Devon(\s|\-|\_)Lee|Dia(\s|\-|\_)Zerva|Diamond(\s|\-|\_)Foxxx|Diamond(\s|\-|\_)Jackson|Diamond(\s|\-|\_)Kitty|Diana(\s|\-|\_)Doll|Diana(\s|\-|\_)Prince|Dick(\s|\-|\_)Chibbles|Diddylicious|Dillion(\s|\-|\_)Carter|Dillion(\s|\-|\_)Harper|Dino(\s|\-|\_)Bravo|Dirty(\s|\-|\_)Angie|Diva|Dolly(\s|\-|\_)Diore|Dolly(\s|\-|\_)Leigh|Dominica(\s|\-|\_)Phoenix|Dominika(\s|\-|\_)C|Dominno|Donna(\s|\-|\_)Bell|Donnie(\s|\-|\_)Rock|Donny(\s|\-|\_)Sins|Dorina|Dorothy(\s|\-|\_)Black|Dragon(\s|\-|\_)Lily|Draven(\s|\-|\_)Star|Dredd|Dylan(\s|\-|\_)Ryan|Dylan(\s|\-|\_)Ryder|Ebony(\s|\-|\_)Sweetness|Eden(\s|\-|\_)Adams|Eden(\s|\-|\_)Sinclair|Edyn(\s|\-|\_)Blair|Eileen(\s|\-|\_)Sue|Elaina(\s|\-|\_)Raye|Elektra(\s|\-|\_)Rose|Elena(\s|\-|\_)Koshka|Elexis(\s|\-|\_)Monroe|Elina(\s|\-|\_)Mikki|Eliss(\s|\-|\_)Fire|Eliza(\s|\-|\_)Ibarra|Eliza(\s|\-|\_)Jane|Ella(\s|\-|\_)Hughes|Ella(\s|\-|\_)Knox|Ella(\s|\-|\_)Milano|Ella(\s|\-|\_)Nova|Elle(\s|\-|\_)Alexandra|Elsa(\s|\-|\_)Jean|Ember(\s|\-|\_)Snow|Emily(\s|\-|\_)Addison|Emily(\s|\-|\_)Bloom|Emily(\s|\-|\_)Grey|Emily(\s|\-|\_)Thorne|Emily(\s|\-|\_)Willis|Emma(\s|\-|\_)Blond|Emma(\s|\-|\_)Brown|Emma(\s|\-|\_)Butt|Emma(\s|\-|\_)Haize|Emma(\s|\-|\_)Heart|Emma(\s|\-|\_)Hix|Emma(\s|\-|\_)Leigh|Emma(\s|\-|\_)Starletto|Emma(\s|\-|\_)Starr|Emy(\s|\-|\_)Reyes|Envy|Eric(\s|\-|\_)John|Eric(\s|\-|\_)Masterson|Erica(\s|\-|\_)Bee|Erica(\s|\-|\_)Fontes|Erica(\s|\-|\_)Lauren|Erik(\s|\-|\_)Everhard|Erin(\s|\-|\_)Moore|Esmi(\s|\-|\_)Lee|Estelle|Eufrat|Eva(\s|\-|\_)Angelina|Eva(\s|\-|\_)Antonyia|Eva(\s|\-|\_)Berger|Eva(\s|\-|\_)Elfie|Eva(\s|\-|\_)Karera|Eva(\s|\-|\_)Lin|Eva(\s|\-|\_)Long|Eva(\s|\-|\_)Lovia|Eva(\s|\-|\_)Notty|Eva(\s|\-|\_)Parcker|Evan(\s|\-|\_)Stone|Eve(\s|\-|\_)Angel|Eve(\s|\-|\_)Laurence|Evelina(\s|\-|\_)Darling|Eveline(\s|\-|\_)Dellai|Evelyn(\s|\-|\_)Lin|Evelyn(\s|\-|\_)Neill|Evi(\s|\-|\_)Fox|Evilyn(\s|\-|\_)Fierce|Faye(\s|\-|\_)Reagan|Felony|Ferrara(\s|\-|\_)Gomez|Filthy(\s|\-|\_)Rich|Flash(\s|\-|\_)Brown|Flower(\s|\-|\_)Tucci|Foxy(\s|\-|\_)Di|Francesca(\s|\-|\_)Felucci|Francesca(\s|\-|\_)Le|Franceska(\s|\-|\_)Jaimes|Franco(\s|\-|\_)Roccaforte|Francys(\s|\-|\_)Belle|Frankie(\s|\-|\_)Babe|Frida(\s|\-|\_)Sante|Gabriella(\s|\-|\_)Paltrova|Gabrielle(\s|\-|\_)Gucci|Galina(\s|\-|\_)A|Gemma(\s|\-|\_)Massey|Genevieve(\s|\-|\_)Gandi|George(\s|\-|\_)Uhl|Georgia(\s|\-|\_)Jones|Georgie(\s|\-|\_)Lyall|Gia(\s|\-|\_)Derza|Gia(\s|\-|\_)Dimarco|Gia(\s|\-|\_)Love|Gia(\s|\-|\_)Paige|Gianna(\s|\-|\_)Dior|Gianna(\s|\-|\_)Lynn|Gianna(\s|\-|\_)Michaels|Gianna(\s|\-|\_)Nicole|Gigi(\s|\-|\_)Allens|Gigi(\s|\-|\_)Rivera|Gilda(\s|\-|\_)Roberts|Gina(\s|\-|\_)Devine|Gina(\s|\-|\_)Gerson|Gina(\s|\-|\_)Lynn|Gina(\s|\-|\_)Valentina|Giselle(\s|\-|\_)Leon|Giselle(\s|\-|\_)Palmer|Gloria(\s|\-|\_)Sol|Goldie(\s|\-|\_)Coxx|Goldie(\s|\-|\_)Rush|Grabby(\s|\-|\_)Sin|Gracie(\s|\-|\_)Glam|Grandma(\s|\-|\_)Libby|Green(\s|\-|\_)Eyes|Hailey(\s|\-|\_)Young|Haili(\s|\-|\_)Sanders|Haley(\s|\-|\_)Cummings|Haley(\s|\-|\_)Reed|Halle(\s|\-|\_)Von|Hanna(\s|\-|\_)Hilton|Hannah(\s|\-|\_)Hays|Harley(\s|\-|\_)Dean|Harley(\s|\-|\_)Jade|Harmony(\s|\-|\_)Reigns|Harmony(\s|\-|\_)Rose|Harmony(\s|\-|\_)Wonder|Hayley(\s|\-|\_)Marie(\s|\-|\_)Coppin|Heather(\s|\-|\_)Starlet|Heather(\s|\-|\_)Vahn|Heather(\s|\-|\_)Wild|Helly(\s|\-|\_)Mae(\s|\-|\_)Hellfire|Henessy|Hillary(\s|\-|\_)Scott|Hime(\s|\-|\_)Marie|Hollie(\s|\-|\_)Stevens|Holly(\s|\-|\_)Deacon|Holly(\s|\-|\_)Gibbons|Holly(\s|\-|\_)Halston|Holly(\s|\-|\_)Heart|Holly(\s|\-|\_)Hendrix|Holly(\s|\-|\_)Kiss|Holly(\s|\-|\_)Michaels|Holly(\s|\-|\_)Wellin|Holly(\s|\-|\_)West|Honey(\s|\-|\_)Demon|Honey(\s|\-|\_)Foxxx|Honey(\s|\-|\_)Gold|Honour(\s|\-|\_)May|"
	stars2 := "Hope(\s|\-|\_)Howell|Hot(\s|\-|\_)Wife(\s|\-|\_)Rio|Hottie(\s|\-|\_)Tracy|Houston|Ian(\s|\-|\_)Scott|Ice(\s|\-|\_)Cold|Ike(\s|\-|\_)Diezel|India(\s|\-|\_)Summer|Ines(\s|\-|\_)Cudna|Iona(\s|\-|\_)Grace|Irene(\s|\-|\_)Rouse|Irina(\s|\-|\_)Bruni|Irina(\s|\-|\_)J|Iris(\s|\-|\_)Rose|Isabella(\s|\-|\_)Chrystin|Isabella(\s|\-|\_)Clark|Isabella(\s|\-|\_)De(\s|\-|\_)Santos|Isabella(\s|\-|\_)Nice|Isiah(\s|\-|\_)Maxwell|Isis(\s|\-|\_)Love|Isis(\s|\-|\_)Taylor|Ivana(\s|\-|\_)Sugar|Ivy(\s|\-|\_)Lebelle|Ivy(\s|\-|\_)Winters|Ivy(\s|\-|\_)Wolfe|Izamar(\s|\-|\_)Gutierrez|Izzy(\s|\-|\_)Delphine|J(\s|\-|\_)Mac|Jack(\s|\-|\_)Lawrence|Jack(\s|\-|\_)Napier|Jack(\s|\-|\_)Vegas|Jacky(\s|\-|\_)Joy|Jaclyn(\s|\-|\_)Taylor|Jada(\s|\-|\_)Fire|Jada(\s|\-|\_)Stevens|Jade(\s|\-|\_)Indica|Jade(\s|\-|\_)Jantzen|Jade(\s|\-|\_)Marxxx|Jade(\s|\-|\_)Nile|Jaelyn(\s|\-|\_)Fox|Jake(\s|\-|\_)Adams|James(\s|\-|\_)Brossman|James(\s|\-|\_)Deen|Jamie(\s|\-|\_)Jenkins|Jamie(\s|\-|\_)Lynn|Jan(\s|\-|\_)Burton|Jana(\s|\-|\_)Cova|Jana(\s|\-|\_)Jordan|Jana(\s|\-|\_)Mrazkova|Jane(\s|\-|\_)Wilde|Janet(\s|\-|\_)Mason|Janice(\s|\-|\_)Griffith|Jasmine(\s|\-|\_)Black|Jasmine(\s|\-|\_)Jae|Jasmine(\s|\-|\_)Jewels|Jasmine(\s|\-|\_)Rouge|Jasmine(\s|\-|\_)Webb|Jason(\s|\-|\_)Brown|Jassie|Jax(\s|\-|\_)Slayher|Jay(\s|\-|\_)Crew|Jay(\s|\-|\_)Romero|Jay(\s|\-|\_)Smooth|Jay(\s|\-|\_)Taylor|Jayden(\s|\-|\_)Cole|Jayden(\s|\-|\_)Jaymes|Jayden(\s|\-|\_)Lee|Jaye(\s|\-|\_)Rose|Jaye(\s|\-|\_)Summers|Jaylene(\s|\-|\_)Rio|Jayme(\s|\-|\_)Langford|Jazmin|Jean(\s|\-|\_)Val(\s|\-|\_)Jean|Jeanie(\s|\-|\_)Marie(\s|\-|\_)Sullivan|Jelena(\s|\-|\_)Jensen|Jenaveve(\s|\-|\_)Jolie|Jenifer|Jenna(\s|\-|\_)Ashley|Jenna(\s|\-|\_)Foxx|Jenna(\s|\-|\_)Haze|Jenna(\s|\-|\_)Ivory|Jenna(\s|\-|\_)J(\s|\-|\_)Ross|Jenna(\s|\-|\_)Presley|Jenna(\s|\-|\_)Reid|Jenna(\s|\-|\_)Sativa|Jenni(\s|\-|\_)Gregg|Jenni(\s|\-|\_)Lee|Jennifer(\s|\-|\_)Dark|Jennifer(\s|\-|\_)Jane|Jennifer(\s|\-|\_)Love|Jennifer(\s|\-|\_)Stone|Jennifer(\s|\-|\_)White|Jenny(\s|\-|\_)Appach|Jenny(\s|\-|\_)Hendrix|Jenny(\s|\-|\_)Simons|Jeny(\s|\-|\_)Baby|Jess(\s|\-|\_)West|Jessa(\s|\-|\_)Rhodes|Jesse(\s|\-|\_)Jane|Jessi(\s|\-|\_)Palmer|Jessica(\s|\-|\_)Bangkok|Jessica(\s|\-|\_)Dawson|Jessica(\s|\-|\_)Drake|Jessica(\s|\-|\_)Fox|Jessica(\s|\-|\_)Jaymes|Jessica(\s|\-|\_)Lynn|Jessica(\s|\-|\_)Moore|Jessica(\s|\-|\_)Rex|Jessica(\s|\-|\_)Ryan|Jessie(\s|\-|\_)Andrews|Jessie(\s|\-|\_)Cox|Jessie(\s|\-|\_)Jazz|Jessie(\s|\-|\_)Lee|Jessie(\s|\-|\_)Rogers|Jessie(\s|\-|\_)Saint|Jessie(\s|\-|\_)Volt|Jessy(\s|\-|\_)Dubai|Jessy(\s|\-|\_)Jones|Jessyka(\s|\-|\_)Swan|Jewels(\s|\-|\_)Jade|Jezebelle(\s|\-|\_)Bond|Jia(\s|\-|\_)Lissa|Jill(\s|\-|\_)Kassidy|Jillian(\s|\-|\_)Janson|Jim(\s|\-|\_)Slip|Jo(\s|\-|\_)Evans|Joachim(\s|\-|\_)Kessef|Joana(\s|\-|\_)Redgrave|Joanna(\s|\-|\_)Angel|Joanna(\s|\-|\_)Bliss|Joanna(\s|\-|\_)Jet|Jodi(\s|\-|\_)Taylor|Jodie(\s|\-|\_)Gasson|Joey(\s|\-|\_)Brass|Joey(\s|\-|\_)Silvera|Johane(\s|\-|\_)Johanson|John(\s|\-|\_)E(\s|\-|\_)Depth|John(\s|\-|\_)Strong|Johnny(\s|\-|\_)Castle|Johnny(\s|\-|\_)Fender|Johnny(\s|\-|\_)Sins|Jojo(\s|\-|\_)Kiss|Jon(\s|\-|\_)Jon|Jonelle(\s|\-|\_)Brooks|Jonni(\s|\-|\_)Darkko|Jordan(\s|\-|\_)Ash|Jordan(\s|\-|\_)Kingsley|Joseline(\s|\-|\_)Kelly|Josie(\s|\-|\_)Jagger|Joslyn(\s|\-|\_)James|Joss(\s|\-|\_)Lescaf|Juan(\s|\-|\_)Largo|Juan(\s|\-|\_)Lucho|Juelz(\s|\-|\_)Ventura|Juicey(\s|\-|\_)Janey|Jules(\s|\-|\_)Jordan|Julia(\s|\-|\_)Ann|Julia(\s|\-|\_)De(\s|\-|\_)Lucia|Julia(\s|\-|\_)Roca|Juliana(\s|\-|\_)Kincaid|Julie(\s|\-|\_)Night|Juliette(\s|\-|\_)March|Julius(\s|\-|\_)Ceazher|Justin(\s|\-|\_)Hunt|Justin(\s|\-|\_)Long|Justine(\s|\-|\_)Joli|Jynx(\s|\-|\_)Maze|Kacey(\s|\-|\_)Jordan|Kaci(\s|\-|\_)Starr|Kacy(\s|\-|\_)Lane|Kagney(\s|\-|\_)Linn(\s|\-|\_)Karter|Kai(\s|\-|\_)Taylor|Kaira(\s|\-|\_)18|Kali(\s|\-|\_)Roses|Kalina(\s|\-|\_)Ryu|Kara(\s|\-|\_)Carter|Karen(\s|\-|\_)Dreams|Karen(\s|\-|\_)Fisher|Karina(\s|\-|\_)Baru|Karina(\s|\-|\_)Grand|Karina(\s|\-|\_)Hart|Karina(\s|\-|\_)White|Karla(\s|\-|\_)Kush|Karlee(\s|\-|\_)Grey|Karlie(\s|\-|\_)Montana|Karlie(\s|\-|\_)Simon|Karlo(\s|\-|\_)Karrera|Karma(\s|\-|\_)Rx|Karmen(\s|\-|\_)Karma|Karol(\s|\-|\_)Lilien|Karter(\s|\-|\_)Foxx|Kasey(\s|\-|\_)Warner|Kassey(\s|\-|\_)Krystal|Kat(\s|\-|\_)Dior|Katarina(\s|\-|\_)Muti|Kate(\s|\-|\_)Crush|Kate(\s|\-|\_)England|Kate(\s|\-|\_)Rich|Katerina(\s|\-|\_)Hartlova|Katerina(\s|\-|\_)Kay|Kates(\s|\-|\_)Playground|Kathia(\s|\-|\_)Nobili|Kathy(\s|\-|\_)Anderson|Katie(\s|\-|\_)Banks|Katie(\s|\-|\_)Jordin|Katie(\s|\-|\_)Kox|Katie(\s|\-|\_)Morgan|Katie(\s|\-|\_)St(\s|\-|\_)Ives|Katie(\s|\-|\_)Summers|Katie(\s|\-|\_)Thomas|Katja(\s|\-|\_)Kassin|Katrin(\s|\-|\_)Tequila|Katrina(\s|\-|\_)Jade|Katsuni|Kattie(\s|\-|\_)Gold|Katy(\s|\-|\_)Borman|Katy(\s|\-|\_)Rose|Katya(\s|\-|\_)Clover|Kay(\s|\-|\_)Jay|Kayden(\s|\-|\_)Kross|Kayla(\s|\-|\_)Carrera|Kayla(\s|\-|\_)Green|Kayla(\s|\-|\_)Kayden|Kayla(\s|\-|\_)Paige|Kaylani(\s|\-|\_)Lei|Kaylee(\s|\-|\_)Haze|Kaylee(\s|\-|\_)Hilton|Kaylynn|Keeani(\s|\-|\_)Lei|Keira(\s|\-|\_)Nicole|Keiran(\s|\-|\_)Lee|Keisha(\s|\-|\_)Grey|Kelly(\s|\-|\_)Clare|Kelly(\s|\-|\_)Divine|Kelly(\s|\-|\_)Kay|Kelly(\s|\-|\_)Klass|Kelly(\s|\-|\_)Madison|Kelsi(\s|\-|\_)Monroe|Kendall(\s|\-|\_)Karson|Kendra(\s|\-|\_)James|Kendra(\s|\-|\_)Lust|Kendra(\s|\-|\_)Spade|Keni(\s|\-|\_)Styles|Kenna(\s|\-|\_)James|Kennedy(\s|\-|\_)Leigh|Kenzie(\s|\-|\_)Reeves|Kenzie(\s|\-|\_)Taylor|Kerry(\s|\-|\_)Louise|Kerry(\s|\-|\_)Marie|Kevin(\s|\-|\_)Moore|Khloe(\s|\-|\_)Kapri|Kianna(\s|\-|\_)Dior|Kiara(\s|\-|\_)Cole|Kiara(\s|\-|\_)Diane|Kiara(\s|\-|\_)Lord|Kiara(\s|\-|\_)Mia|Kiera(\s|\-|\_)King|Kiera(\s|\-|\_)Winters|Kiki(\s|\-|\_)Daire|Kiki(\s|\-|\_)Klement|Kiki(\s|\-|\_)Minaj|Kim(\s|\-|\_)B|Kimber(\s|\-|\_)James|Kimber(\s|\-|\_)Lee|Kimber(\s|\-|\_)Woods|Kimberly(\s|\-|\_)Kane|Kimmy(\s|\-|\_)Granger|Kira(\s|\-|\_)Noir|Kira(\s|\-|\_)Queen|Kirsten(\s|\-|\_)Price|Kissa(\s|\-|\_)Sins|Kita(\s|\-|\_)Zen|Kitana(\s|\-|\_)Lure|Kitty(\s|\-|\_)Cat|Kitty(\s|\-|\_)Jane|Klarisa(\s|\-|\_)Leone|Kleio(\s|\-|\_)Valentien|Koko(\s|\-|\_)Amaris|Kortney(\s|\-|\_)Kane|Kris(\s|\-|\_)Slater|Krissy(\s|\-|\_)Lynn|Kristal(\s|\-|\_)Summers|Kristen(\s|\-|\_)Scott|Kristin|Kristina(\s|\-|\_)Rose|Kristine(\s|\-|\_)Crystalis|Kristof(\s|\-|\_)Cale|Kristy(\s|\-|\_)Black|Krysta(\s|\-|\_)Kaos|Krystal(\s|\-|\_)Boyd|Krystal(\s|\-|\_)Orchid|Krystal(\s|\-|\_)Swift|Kurt(\s|\-|\_)Lockwood|Kyle(\s|\-|\_)Mason|Kyler(\s|\-|\_)Quinn|Kylie(\s|\-|\_)Page|Kylie(\s|\-|\_)Quinn|Kylie(\s|\-|\_)Wylde|Kym(\s|\-|\_)Wilde|Kyra(\s|\-|\_)Hot|Lacey(\s|\-|\_)Starr|Lacy(\s|\-|\_)Channing|Lacy(\s|\-|\_)Lennon|Lady(\s|\-|\_)Dee|Lady(\s|\-|\_)Sonia|Ladyboy(\s|\-|\_)Amy|Ladyboy(\s|\-|\_)Far(\s|\-|\_)2|Ladyboy(\s|\-|\_)Many|Ladyboy(\s|\-|\_)May|Ladyboy(\s|\-|\_)New|Ladyboy(\s|\-|\_)Oil|Ladyboy(\s|\-|\_)Top|Laela(\s|\-|\_)Pryce|Laetitia|Lana(\s|\-|\_)Ess|Lana(\s|\-|\_)Rhoades|Lana(\s|\-|\_)Violet|Lance(\s|\-|\_)Hart|Lane(\s|\-|\_)Sisters|Lara(\s|\-|\_)Brookes|Lara(\s|\-|\_)Latex|Laura(\s|\-|\_)Hollyman|Laura(\s|\-|\_)Orsolya|Lauren(\s|\-|\_)Crist|Lauren(\s|\-|\_)Louise|Lauren(\s|\-|\_)Phillips|Lavana(\s|\-|\_)Lou|Lavish(\s|\-|\_)Styles|Layla(\s|\-|\_)London|Layla(\s|\-|\_)Rose|Layla(\s|\-|\_)Sin|Lea(\s|\-|\_)Lexis|Leah(\s|\-|\_)Gotti|Leah(\s|\-|\_)Jaye|Leanna(\s|\-|\_)Sweet|Leanne(\s|\-|\_)Crow|Leanne(\s|\-|\_)Lace|Lee(\s|\-|\_)Anne|Lee(\s|\-|\_)Stone|Leigh(\s|\-|\_)Darby|Leigh(\s|\-|\_)Raven|Leila(\s|\-|\_)Smith|Leilani(\s|\-|\_)Leeane|Lela(\s|\-|\_)Star|Lena(\s|\-|\_)Anderson|Lena(\s|\-|\_)Love|Lena(\s|\-|\_)Nicole|Lena(\s|\-|\_)Paul|Leona(\s|\-|\_)Mia|Leony(\s|\-|\_)April|Leslie(\s|\-|\_)Taylor|Levi(\s|\-|\_)Cash|Lexi(\s|\-|\_)Belle|Lexi(\s|\-|\_)Bloom|Lexi(\s|\-|\_)Dona|Lexi(\s|\-|\_)Lore|Lexi(\s|\-|\_)Love|Lexi(\s|\-|\_)Lowe|Lexi(\s|\-|\_)Luna|Lexi(\s|\-|\_)Swallow|Lexie(\s|\-|\_)Cummings|Leya(\s|\-|\_)Falcon|Leyla(\s|\-|\_)Black|Lezley(\s|\-|\_)Zen|Li(\s|\-|\_)Moon|Lia(\s|\-|\_)Lor|Lickable(\s|\-|\_)Stylez|Lilly(\s|\-|\_)Banks|Lilly(\s|\-|\_)Ford|Lilu(\s|\-|\_)Moon|Lily(\s|\-|\_)Adams|Lily(\s|\-|\_)Cade|Lily(\s|\-|\_)Carter|Lily(\s|\-|\_)Jordan|Lily(\s|\-|\_)LaBeau|Lily(\s|\-|\_)Lane|Lily(\s|\-|\_)Larimar|Lily(\s|\-|\_)Love|Lily(\s|\-|\_)May|Lily(\s|\-|\_)Rader|Linda(\s|\-|\_)Sweet|Lindsey(\s|\-|\_)Howorth|Lindsey(\s|\-|\_)Olsen|Linsey(\s|\-|\_)Dawn(\s|\-|\_)Mckenzie|Lisa(\s|\-|\_)Ann|Lisa(\s|\-|\_)Dawn|Little(\s|\-|\_)Caprice|Liv(\s|\-|\_)Aguilera|Liv(\s|\-|\_)Revamped|Liza(\s|\-|\_)B|Liza(\s|\-|\_)Del(\s|\-|\_)Sierra|Liza(\s|\-|\_)Rowe|Lizz(\s|\-|\_)Tayler|Logan(\s|\-|\_)Long|Logan(\s|\-|\_)Pierce|Lola(\s|\-|\_)Foxx|Lola(\s|\-|\_)Myluv|Lola(\s|\-|\_)Taylor|Lolly(\s|\-|\_)Hardcore|Lolly(\s|\-|\_)Ink|London(\s|\-|\_)Keyes|London(\s|\-|\_)RIver|Long(\s|\-|\_)Mint|Loni|Loni(\s|\-|\_)Evans|Loni(\s|\-|\_)Legend|Loni(\s|\-|\_)Sanders|Lorelei(\s|\-|\_)Lee|Lorena(\s|\-|\_)Garcia|Lori(\s|\-|\_)Anderson|Lou(\s|\-|\_)Charmelle|Louise(\s|\-|\_)Pearce|LouLou|Lovenia(\s|\-|\_)Lux|Lovita(\s|\-|\_)Fate|Lucas(\s|\-|\_)Frost|Lucas(\s|\-|\_)Stone|Lucia(\s|\-|\_)Love|Luciana|Lucie(\s|\-|\_)Theodorova|Lucie(\s|\-|\_)Wilde|Lucky(\s|\-|\_)Starr|Lucy(\s|\-|\_)Anne(\s|\-|\_)Brooks|Lucy(\s|\-|\_)Bell|Lucy(\s|\-|\_)Belle|Lucy(\s|\-|\_)Doll|Lucy(\s|\-|\_)Gresty|Lucy(\s|\-|\_)Heart|Lucy(\s|\-|\_)Lee|Lucy(\s|\-|\_)Li|Lucy(\s|\-|\_)Love|Lucy(\s|\-|\_)Zara|Luke(\s|\-|\_)Hardy|Luna(\s|\-|\_)Corazon|Luna(\s|\-|\_)Kitsuen|Luna(\s|\-|\_)Rival|Luna(\s|\-|\_)Star|Luscious(\s|\-|\_)Lopez|Lycia(\s|\-|\_)Sharyl|Lyen(\s|\-|\_)Parker|Lyla(\s|\-|\_)Storm|Lylith(\s|\-|\_)Lavey|Lyra(\s|\-|\_)Law|Maci(\s|\-|\_)Winslett|Mackenzee(\s|\-|\_)Pierce|Maddy(\s|\-|\_)Oreilly|Madelyn(\s|\-|\_)Marie|Madelyn(\s|\-|\_)Monroe|Madison(\s|\-|\_)Ivy|Madison(\s|\-|\_)Parker|Madison(\s|\-|\_)Scott|Madison(\s|\-|\_)Young|Mae(\s|\-|\_)Meyers|Magdalene(\s|\-|\_)St(\s|\-|\_)Michaels|Maggie(\s|\-|\_)Green|Mai(\s|\-|\_)Ly|Maia(\s|\-|\_)Davis|Maitresse(\s|\-|\_)Madeline|Malena(\s|\-|\_)Morgan|Malibu|Mandingo|Mandy(\s|\-|\_)Bright|Mandy(\s|\-|\_)Dee|Mandy(\s|\-|\_)Mitchell|Mandy(\s|\-|\_)Muse|Manuel(\s|\-|\_)Ferrara|Marco(\s|\-|\_)Banderas|Marcus(\s|\-|\_)London|Maria(\s|\-|\_)Bellucci|Mariah(\s|\-|\_)Milano|Marica(\s|\-|\_)Hase|Marie(\s|\-|\_)Luv|Marie(\s|\-|\_)McCray|Marina(\s|\-|\_)Angel|Marina(\s|\-|\_)Visconti|Mark(\s|\-|\_)Ashley|Mark(\s|\-|\_)Davis|Mark(\s|\-|\_)Wood|Mark(\s|\-|\_)Zane|Markus(\s|\-|\_)Dupree|Marley(\s|\-|\_)Brinx|Marry(\s|\-|\_)Queen|Marsha(\s|\-|\_)May|Mary(\s|\-|\_)Jane|Mary(\s|\-|\_)Kalisy|Mary(\s|\-|\_)Rock|Maserati(\s|\-|\_)XXX|Mason(\s|\-|\_)Moore|Matt(\s|\-|\_)Bird|Maya(\s|\-|\_)Bijou|Maya(\s|\-|\_)Hills|Maya(\s|\-|\_)Kendrick|McKenzie(\s|\-|\_)Lee|Mea(\s|\-|\_)Melone|Meet(\s|\-|\_)Madden|Megan(\s|\-|\_)Coxxx|Megan(\s|\-|\_)Rain|Megan(\s|\-|\_)Salinas|Megumi(\s|\-|\_)Shino|Mel|Melanie(\s|\-|\_)Hicks|Melanie(\s|\-|\_)Memphis|Melanie(\s|\-|\_)Rios|Melena(\s|\-|\_)Maria(\s|\-|\_)Rya|Melina(\s|\-|\_)Mason|Melissa(\s|\-|\_)Lauren|Melissa(\s|\-|\_)Mendiny|Melissa(\s|\-|\_)Moore|Melissa(\s|\-|\_)Ria|Mellanie(\s|\-|\_)Monroe|Melody(\s|\-|\_)Jordan|Memphis(\s|\-|\_)Monroe|Mercedes(\s|\-|\_)Carrera|Mercy(\s|\-|\_)West|Merilyn(\s|\-|\_)Sakova|Mia(\s|\-|\_)Bangg|Mia(\s|\-|\_)Gold|Mia(\s|\-|\_)Isabella|Mia(\s|\-|\_)Lee|Mia(\s|\-|\_)Lelani|Mia(\s|\-|\_)Li|Mia(\s|\-|\_)Malkova|Mia(\s|\-|\_)Manarote|Mia(\s|\-|\_)Sollis|Michael(\s|\-|\_)Stefano|Michael(\s|\-|\_)Vegas|Michaela(\s|\-|\_)Isizzu|Michelle(\s|\-|\_)Lay|Michelle(\s|\-|\_)Moist|Michelle(\s|\-|\_)Thorne|Michelles(\s|\-|\_)Nylons|Mick(\s|\-|\_)Blue|Mickey(\s|\-|\_)Mod|Micky(\s|\-|\_)Bells|Mika(\s|\-|\_)Tan|Mikayla|Mike(\s|\-|\_)Adriano|Mike(\s|\-|\_)Angelo|Mike(\s|\-|\_)Mancini|Mikey(\s|\-|\_)Butders|Mila(\s|\-|\_)Azul|Mila(\s|\-|\_)Jade|Milena(\s|\-|\_)Angel|Milena(\s|\-|\_)D|Miley(\s|\-|\_)Mae|Mindi(\s|\-|\_)Mink|Minka|Mira(\s|\-|\_)Sunset|Miranda(\s|\-|\_)Miller|Mischa(\s|\-|\_)Brooks|Misha(\s|\-|\_)Cross|Miss(\s|\-|\_)Melrose|Missy(\s|\-|\_)Martinez|Mistress(\s|\-|\_)Kara|Misty(\s|\-|\_)Stone|Moka(\s|\-|\_)Mora|Molly(\s|\-|\_)Bennett|Molly(\s|\-|\_)Jane|Molly(\s|\-|\_)Maracas|Mona(\s|\-|\_)Lee|Mona(\s|\-|\_)Wales|Monica(\s|\-|\_)Sweet|Monique(\s|\-|\_)Alexander|Monique(\s|\-|\_)Woods|Morgan(\s|\-|\_)Bailey|Morgan(\s|\-|\_)Lee|Morgan(\s|\-|\_)Rodriguez|Mr(\s|\-|\_)Marcus|Mya(\s|\-|\_)Diamond|Mya(\s|\-|\_)Nichole|Mz(\s|\-|\_)Berlin|Nacho(\s|\-|\_)Vidal|Nadia(\s|\-|\_)Styles|Nancy(\s|\-|\_)A|Naomi(\s|\-|\_)Bennet|Naomi(\s|\-|\_)Nevena|Naomi(\s|\-|\_)Woods|Nat(\s|\-|\_)Turnher|Natalia(\s|\-|\_)Forrest|Natalia(\s|\-|\_)Starr|Natalie(\s|\-|\_)Heart|Natalie(\s|\-|\_)K|Natalie(\s|\-|\_)Mars|Natalli(\s|\-|\_)Di(\s|\-|\_)Angelo|Nataly(\s|\-|\_)Gold|Nataly(\s|\-|\_)Von|Natasha(\s|\-|\_)Marley|Natasha(\s|\-|\_)Nice|Natasha(\s|\-|\_)Starr|Natasha(\s|\-|\_)Teen|Natasha(\s|\-|\_)White|Natassia(\s|\-|\_)Dreams|Nathaly(\s|\-|\_)Cherie|Nathan(\s|\-|\_)Bronson|Naughty(\s|\-|\_)Allie|Naughty(\s|\-|\_)Alysha|Nekane|Nelya(\s|\-|\_)Jorden|Nessa(\s|\-|\_)Devil|Nesty|Nia(\s|\-|\_)Nacci|Nick(\s|\-|\_)Manning|Nickey(\s|\-|\_)Huntsman|Nicki(\s|\-|\_)Blue|Nicky(\s|\-|\_)Angel|Nicole(\s|\-|\_)Aniston|Nicole(\s|\-|\_)Evans|Nicole(\s|\-|\_)Love|Nicole(\s|\-|\_)Montero|Nicole(\s|\-|\_)Peters|Nicole(\s|\-|\_)Ray|Nicole(\s|\-|\_)Vice|Nicolette(\s|\-|\_)Shea|Niemira|Nika(\s|\-|\_)Blond|Nika(\s|\-|\_)N(\s|\-|\_)Vesna|Nika(\s|\-|\_)Noire|Niki(\s|\-|\_)Snow|Nikia(\s|\-|\_)Ahe|Nikita(\s|\-|\_)Bellucci|Nikita(\s|\-|\_)Von(\s|\-|\_)James|Nikki(\s|\-|\_)Anne|Nikki(\s|\-|\_)Benz|Nikki(\s|\-|\_)Daniels|Nikki(\s|\-|\_)Delano|Nikki(\s|\-|\_)Friend|Nikki(\s|\-|\_)Hearts|Nikki(\s|\-|\_)Hunter|Nikki(\s|\-|\_)Rhodes|Nikki(\s|\-|\_)Sexx|Nikki(\s|\-|\_)Sims|Nikki(\s|\-|\_)Sweet|Nikky(\s|\-|\_)Dream|Nikky(\s|\-|\_)Thorne|Nimfa|Nina(\s|\-|\_)Elle|Nina(\s|\-|\_)Hartley|Nina(\s|\-|\_)North|Noelle(\s|\-|\_)Easton|Noemie(\s|\-|\_)Bilas|Nyomi(\s|\-|\_)Banxxx|Nyrobi(\s|\-|\_)Knights|Octavia(\s|\-|\_)Red|Olga(\s|\-|\_)Shkabarnya|Oliver(\s|\-|\_)Flynn|Olivia(\s|\-|\_)Jayy|Olivia(\s|\-|\_)Love|Olivia(\s|\-|\_)Sparkle|Olivia(\s|\-|\_)Westervelt|Omar(\s|\-|\_)Galanti|Ophelia(\s|\-|\_)H(\s|\-|\_)Flowers|Ophelia(\s|\-|\_)Kaan|Owen(\s|\-|\_)Michaels|Oxi(\s|\-|\_)Bendini|Paige(\s|\-|\_)Owens|Paige(\s|\-|\_)Turnah|Panther|Paris(\s|\-|\_)White|Pascal(\s|\-|\_)White|Patritcy(\s|\-|\_)A|Patty(\s|\-|\_)Michova|Paula(\s|\-|\_)Shy|Payton(\s|\-|\_)Leigh|Peaches|Penelope(\s|\-|\_)Reed|Penny(\s|\-|\_)Barber|Penny(\s|\-|\_)Flame|Penny(\s|\-|\_)Pax|Persia(\s|\-|\_)Monir|Peta(\s|\-|\_)Jensen|Peter(\s|\-|\_)Green|Peter(\s|\-|\_)North|Phoebe|Phoenix(\s|\-|\_)Marie|Pinky|Pinky(\s|\-|\_)June|Piper(\s|\-|\_)Fawn|Piper(\s|\-|\_)Perri|Porchia(\s|\-|\_)Watson|"
	stars3 := "Sara(\s|\-|\_)Bell|payton(\s|\-|\_)preslee|savannah(\s|\-|\_)bond|Porno(\s|\-|\_)Dan|Presley(\s|\-|\_)Parker|Prince(\s|\-|\_)Yahshua|Princess(\s|\-|\_)Donna|Pristine(\s|\-|\_)Edge|Priya(\s|\-|\_)Rai|Promesita|Proxy(\s|\-|\_)Paige|Puma(\s|\-|\_)Swede|Qiana(\s|\-|\_)Chase|Queen(\s|\-|\_)Christin|Queen(\s|\-|\_)Rogue|Quinn(\s|\-|\_)Ahe|Quinn(\s|\-|\_)Helix|Quinn(\s|\-|\_)Lindemann|Quinn(\s|\-|\_)Quest|Quinn(\s|\-|\_)Rain|Quinn(\s|\-|\_)Waters|Quinn(\s|\-|\_)Wilde|Quinton(\s|\-|\_)James|Qutie(\s|\-|\_)Quinn|Rachael(\s|\-|\_)Cavalli|Rachael(\s|\-|\_)Madori|Rachel(\s|\-|\_)Aldana|Rachel(\s|\-|\_)Aziani|Rachel(\s|\-|\_)Evans|Rachel(\s|\-|\_)James|Rachel(\s|\-|\_)Love|Rachel(\s|\-|\_)Roxxx|Rachel(\s|\-|\_)Starr|Rachele(\s|\-|\_)Richey|Rahyndee(\s|\-|\_)James|Rain(\s|\-|\_)Degrey|Ralph(\s|\-|\_)Long|Ramon(\s|\-|\_)Nomar|Randy(\s|\-|\_)Spears|Raquel(\s|\-|\_)Devine|Raul(\s|\-|\_)Costa|Raven(\s|\-|\_)Bay|Raven(\s|\-|\_)Rockette|Raylene|Raylene(\s|\-|\_)Richards|RayVeness|Reagan(\s|\-|\_)Foxx|Rebeca(\s|\-|\_)Linares|Rebecca(\s|\-|\_)Blue|Rebecca(\s|\-|\_)Moore|Rebecca(\s|\-|\_)More|Rebecca(\s|\-|\_)Volpetti|Rebel(\s|\-|\_)Lynn|Red(\s|\-|\_)Fox|Red(\s|\-|\_)XXX|Reena(\s|\-|\_)Sky|Remy(\s|\-|\_)LaCroix|Renata(\s|\-|\_)Fox|Richelle(\s|\-|\_)Ryan|Richie(\s|\-|\_)Calhoun|Ricki(\s|\-|\_)White|Ricky(\s|\-|\_)Johnson|Ricky(\s|\-|\_)Spanish|Rico(\s|\-|\_)Strong|Rihanna(\s|\-|\_)Samuel|Rikki(\s|\-|\_)Six|Riley(\s|\-|\_)Evans|Riley(\s|\-|\_)Nixon|Riley(\s|\-|\_)Reid|Riley(\s|\-|\_)Reyes|Riley(\s|\-|\_)Shy|Riley(\s|\-|\_)Star|Riley(\s|\-|\_)Steele|Rilynn(\s|\-|\_)Rae|Rina(\s|\-|\_)Ellis|Rio(\s|\-|\_)Lee|Rion(\s|\-|\_)King|Rita(\s|\-|\_)Faltoyano|Rob(\s|\-|\_)Piper|Robby(\s|\-|\_)Echo|Rocco(\s|\-|\_)Reed|Rocco(\s|\-|\_)Siffredi|Romeo(\s|\-|\_)Price|Romi(\s|\-|\_)Rain|Roni(\s|\-|\_)Ford|Rosalyn(\s|\-|\_)Sphinx|Rose(\s|\-|\_)Monroe|Rosie(\s|\-|\_)Whiteman|Roxanne(\s|\-|\_)Hall|Roxanne(\s|\-|\_)Rae|Roxy(\s|\-|\_)Jezel|Roxy(\s|\-|\_)Mendez|Roxy(\s|\-|\_)Raye|Ruby(\s|\-|\_)Knox|Ruth(\s|\-|\_)Blackwell|Ryan(\s|\-|\_)Conner|Ryan(\s|\-|\_)Driller|Ryan(\s|\-|\_)Keely|Ryan(\s|\-|\_)Madison|Ryan(\s|\-|\_)Mclane|Ryan(\s|\-|\_)Ryans|Ryan(\s|\-|\_)Ryder|Ryder(\s|\-|\_)Skye|Sabby(\s|\-|\_)Van(\s|\-|\_)Ryan|Sabrina(\s|\-|\_)Banks|Sabrina(\s|\-|\_)Blond|Sabrina(\s|\-|\_)Moore|Sabrina(\s|\-|\_)Sweet|Sabrisse(\s|\-|\_)Aaliyah|Sade(\s|\-|\_)Mare|Sadie(\s|\-|\_)Santana|Sadie(\s|\-|\_)West|Sailor(\s|\-|\_)Luna|Salome|Sam(\s|\-|\_)Bourne|Sam(\s|\-|\_)Shock|Sam(\s|\-|\_)Tye|Samantha(\s|\-|\_)Bentley|Samantha(\s|\-|\_)Hayes|Samantha(\s|\-|\_)Jolie|Samantha(\s|\-|\_)Rone|Samantha(\s|\-|\_)Ryan|Samantha(\s|\-|\_)Saint|Samantha(\s|\-|\_)Sin|Samia(\s|\-|\_)Duarte|Sammie(\s|\-|\_)Daniels|Sammie(\s|\-|\_)Rhodes|Sammy(\s|\-|\_)Grand|Sandra(\s|\-|\_)Luberc|Sandra(\s|\-|\_)Romain|Sandra(\s|\-|\_)Shine|Sandy(\s|\-|\_)Beach|Sapphira(\s|\-|\_)A|Sara(\s|\-|\_)James|Sara(\s|\-|\_)Jay|Sara(\s|\-|\_)Luvv|Sara(\s|\-|\_)Stone|Sarah(\s|\-|\_)Banks|Sarah(\s|\-|\_)Blake|Sarah(\s|\-|\_)James|Sarah(\s|\-|\_)Jane|Sarah(\s|\-|\_)Jane(\s|\-|\_)Ceylon|Sarah(\s|\-|\_)Jessie|Sarah(\s|\-|\_)Kay|Sarah(\s|\-|\_)Nicola(\s|\-|\_)Randall|Sarah(\s|\-|\_)Shevon|Sarah(\s|\-|\_)Vandella|SaRenna(\s|\-|\_)Lee|Sarika(\s|\-|\_)Ahe|Sarina(\s|\-|\_)Valentina|Sasha(\s|\-|\_)Cane|Sasha(\s|\-|\_)Grey|Sasha(\s|\-|\_)Heart|Sasha(\s|\-|\_)Rose|Satin(\s|\-|\_)Bloom|Satine(\s|\-|\_)Phoenix|Sativa(\s|\-|\_)Rose|Savana(\s|\-|\_)Styles|Savannah(\s|\-|\_)Fox|Savannah(\s|\-|\_)Stern|Saya(\s|\-|\_)Song|Scarlet(\s|\-|\_)Red|Scarlett(\s|\-|\_)Sage|Scott(\s|\-|\_)Lyons|Scott(\s|\-|\_)Nails|Sea(\s|\-|\_)J(\s|\-|\_)Raw|Sean(\s|\-|\_)Lawless|Sean(\s|\-|\_)Michaels|Selena(\s|\-|\_)Rose|Sensual(\s|\-|\_)Jane|September(\s|\-|\_)Carrino|September(\s|\-|\_)Reign|Serena(\s|\-|\_)Blair|Serena(\s|\-|\_)Wood|Serene(\s|\-|\_)Siren|Serenity|Seth(\s|\-|\_)Dickens|Seth(\s|\-|\_)Gamble|Sex(\s|\-|\_)Kitten|Sexy(\s|\-|\_)Vanessa|Sexy(\s|\-|\_)Venera|Sha(\s|\-|\_)Rizel|Shalina(\s|\-|\_)Devine|Shana(\s|\-|\_)Lane|Shane(\s|\-|\_)Diesel|Shannon(\s|\-|\_)Kelly|Sharka(\s|\-|\_)Blue|Sharon(\s|\-|\_)Lee|Sharon(\s|\-|\_)Pink|Shawna(\s|\-|\_)Lenee|Shay(\s|\-|\_)Fox|Shay(\s|\-|\_)Laren|Shay(\s|\-|\_)Sights|Sheena(\s|\-|\_)Ryder|Sheena(\s|\-|\_)Shaw|Sheila(\s|\-|\_)Grant|Sheila(\s|\-|\_)Marie|Sheri(\s|\-|\_)Vi|Sheridan(\s|\-|\_)Love|Shino(\s|\-|\_)Aoi|Shione(\s|\-|\_)Cooper|Shona(\s|\-|\_)River|Shrima(\s|\-|\_)Malati|Shy(\s|\-|\_)Love|Shyla(\s|\-|\_)Jennings|Shyla(\s|\-|\_)Stylez|Sienna(\s|\-|\_)Day|Sienna(\s|\-|\_)West|Sierra(\s|\-|\_)Nevadah|Sierra(\s|\-|\_)Sanders|Silvia(\s|\-|\_)Saige|Silvia(\s|\-|\_)Saint|Silvie(\s|\-|\_)Deluxe|Simone(\s|\-|\_)Sonay|Simony(\s|\-|\_)Diamond|Sindee(\s|\-|\_)Jennings|Sinn(\s|\-|\_)Sage|Siri|Skin(\s|\-|\_)Diamond|Skye(\s|\-|\_)Blue|Skyla(\s|\-|\_)Novea|Skylar(\s|\-|\_)Green|Skylar(\s|\-|\_)Snow|Slim(\s|\-|\_)Poke|Small(\s|\-|\_)Hands|Sofi(\s|\-|\_)Goldfinger|Sofi(\s|\-|\_)Ryan|Sofi(\s|\-|\_)Shane|Sofia(\s|\-|\_)Santana|Sofie(\s|\-|\_)Marie|Sophia(\s|\-|\_)Delane|Sophia(\s|\-|\_)Grace|Sophia(\s|\-|\_)Knight|Sophia(\s|\-|\_)Leone|Sophia(\s|\-|\_)Lola|Sophia(\s|\-|\_)Lomeli|Sophia(\s|\-|\_)Smith|Sophie(\s|\-|\_)Dee|Sophie(\s|\-|\_)Lynx|Sophie(\s|\-|\_)Moone|Sophie(\s|\-|\_)Strauss|Sovereign(\s|\-|\_)Syre|Speedy(\s|\-|\_)Bee|Spencer(\s|\-|\_)Scott|Spring(\s|\-|\_)Thomas|Stacey(\s|\-|\_)Saran|Staci(\s|\-|\_)Carr|Staci(\s|\-|\_)Silverstone|Stacy(\s|\-|\_)Bloom|Stacy(\s|\-|\_)Cruz|Stacy(\s|\-|\_)Silver|Stacy(\s|\-|\_)Snake|Stella(\s|\-|\_)Cox|Stephanie(\s|\-|\_)Cane|Steve(\s|\-|\_)Holmes|Steve(\s|\-|\_)Q|Steven(\s|\-|\_)St(\s|\-|\_)Croix|Stevie(\s|\-|\_)Lix|Stevie(\s|\-|\_)Shae|Stormy(\s|\-|\_)Daniels|Strapon(\s|\-|\_)Jane|Strawberry|Subil(\s|\-|\_)Arch|Sugar(\s|\-|\_)Baby|Summer(\s|\-|\_)Breeze|Summer(\s|\-|\_)Brielle|Summer(\s|\-|\_)Day|Sunny(\s|\-|\_)Day|Sunny(\s|\-|\_)Leone|Susan(\s|\-|\_)Ayn|Susy(\s|\-|\_)Rocks|Suzanna(\s|\-|\_)Aye|Suzie(\s|\-|\_)Carina|Suzie(\s|\-|\_)Diamond|Sweet(\s|\-|\_)Cat|Sweet(\s|\-|\_)Hole|Sweet(\s|\-|\_)Krissy|Sweet(\s|\-|\_)Susi|Sybil(\s|\-|\_)A(\s|\-|\_)Kailena|Syd(\s|\-|\_)Blakovich|Sydnee(\s|\-|\_)Capri|Sydney(\s|\-|\_)Cole|Syren(\s|\-|\_)De(\s|\-|\_)Mer|Szilvia(\s|\-|\_)Lauren|T(\s|\-|\_)Stone|Taissia(\s|\-|\_)Shanti|Tali(\s|\-|\_)Dova|Talia(\s|\-|\_)Mint|Talia(\s|\-|\_)Shepard|Tanner(\s|\-|\_)Mayes|Tanya(\s|\-|\_)James|Tanya(\s|\-|\_)Tate|Tara(\s|\-|\_)Holiday|Tara(\s|\-|\_)Lynn|Tara(\s|\-|\_)Lynn(\s|\-|\_)Foxx|Tara(\s|\-|\_)Pink|Tarra(\s|\-|\_)White|Tasha(\s|\-|\_)Reign|Taylor(\s|\-|\_)Sands|Taylor(\s|\-|\_)Vixen|Taylor(\s|\-|\_)Wane|Taylor(\s|\-|\_)Whyte|Teagan(\s|\-|\_)Summers|Teal(\s|\-|\_)Conrad|Teanna(\s|\-|\_)Trump|Teen(\s|\-|\_)Lola(\s|\-|\_)18|Tera(\s|\-|\_)Patrick|Terry(\s|\-|\_)Nova|Tess(\s|\-|\_)Lyndon|Tessa(\s|\-|\_)Taylor|Thalia|The(\s|\-|\_)Pope|Thomas(\s|\-|\_)Stone|Tia(\s|\-|\_)Cyrus|Tia(\s|\-|\_)Ling|Tiffany(\s|\-|\_)Brookes|Tiffany(\s|\-|\_)Doll|Tiffany(\s|\-|\_)Fox|Tiffany(\s|\-|\_)Rousso|Tiffany(\s|\-|\_)Star|Tiffany(\s|\-|\_)Starr|Tiffany(\s|\-|\_)Tatum|Tiffany(\s|\-|\_)Taylor|Tiffany(\s|\-|\_)Tyler|Tiffany(\s|\-|\_)Watson|Tigerr(\s|\-|\_)Benson|Timea(\s|\-|\_)Bella|Timo(\s|\-|\_)Hardy|Tina(\s|\-|\_)Blade|Tina(\s|\-|\_)Hot|Tina(\s|\-|\_)Kay|Tommy(\s|\-|\_)Gunn|Tommy(\s|\-|\_)Pistol|Tone(\s|\-|\_)Capone|Toni(\s|\-|\_)Ribas|Tony(\s|\-|\_)De(\s|\-|\_)Sergio|Tony(\s|\-|\_)Martinez|Tony(\s|\-|\_)Profane|Tony(\s|\-|\_)Rubino|Tori(\s|\-|\_)Avano|Tori(\s|\-|\_)Black|Tori(\s|\-|\_)Lux|Tory(\s|\-|\_)Lane|Tracey(\s|\-|\_)Lain|Tracy(\s|\-|\_)Gold|Tracy(\s|\-|\_)Lindsay|Trillium|Trina(\s|\-|\_)Michaels|Trinity(\s|\-|\_)St(\s|\-|\_)Clair|Trisha(\s|\-|\_)Parks|Ts(\s|\-|\_)Foxxy|Ts(\s|\-|\_)Kimber(\s|\-|\_)Lee|Tyler(\s|\-|\_)Faith|Tyler(\s|\-|\_)Nixon|Tyler(\s|\-|\_)Steel|Uika(\s|\-|\_)Hoshikawa|Ulorin(\s|\-|\_)Vex|Ulya(\s|\-|\_)I|Uma(\s|\-|\_)Jolie|Uma(\s|\-|\_)Masome|Uncle(\s|\-|\_)George|Undress(\s|\-|\_)Jess|Unique(\s|\-|\_)Lasage|Unique(\s|\-|\_)Murcielago|Urszula(\s|\-|\_)Jade|Uta(\s|\-|\_)Kohaku|Utah(\s|\-|\_)Sweet|Valentina(\s|\-|\_)Nappi|Valentina(\s|\-|\_)Ross|Valentina(\s|\-|\_)Rossini|Valentina(\s|\-|\_)Rush|Valory(\s|\-|\_)Irene|Van(\s|\-|\_)Wylde|Vanda(\s|\-|\_)Bee|Vanda(\s|\-|\_)Lust|Vanessa(\s|\-|\_)Cage|Vanessa(\s|\-|\_)Decker|Vanessa(\s|\-|\_)Hell|Vanessa(\s|\-|\_)Veracruz|Vaniity|Vanilla(\s|\-|\_)DeVille|Vanity|Vanna(\s|\-|\_)Bardot|Venice|Venus|Venus(\s|\-|\_)Lux|Vera(\s|\-|\_)King|Veronica(\s|\-|\_)Avluv|Veronica(\s|\-|\_)Da(\s|\-|\_)Souza|Veronica(\s|\-|\_)Leal|Veronica(\s|\-|\_)Radke|Veronica(\s|\-|\_)Rayne|Veronica(\s|\-|\_)Rodriguez|Veronica(\s|\-|\_)Vain|Veronica(\s|\-|\_)Vanoza|Veronika(\s|\-|\_)Fasterova|Veruca(\s|\-|\_)James|Vicki(\s|\-|\_)Chase|Vicky(\s|\-|\_)Love|Victoria(\s|\-|\_)Blaze|Victoria(\s|\-|\_)Daniels|Victoria(\s|\-|\_)Lawson|Victoria(\s|\-|\_)Puppy|Victoria(\s|\-|\_)Pure|Victoria(\s|\-|\_)Rae|Victoria(\s|\-|\_)Rose|Victoria(\s|\-|\_)Summers|Victoria(\s|\-|\_)Sweet|Victoria(\s|\-|\_)Valentino|Victoria(\s|\-|\_)White|Vienna(\s|\-|\_)Black|Vina(\s|\-|\_)Sky|Vince(\s|\-|\_)Karter|Vinna(\s|\-|\_)Reed|Viola(\s|\-|\_)Bailey|Violet(\s|\-|\_)Monroe|Violet(\s|\-|\_)Starr|Violette(\s|\-|\_)Pure|Violla(\s|\-|\_)A|Virgo(\s|\-|\_)Peridot|Vyxen(\s|\-|\_)Steel|Wendy(\s|\-|\_)Moon|Wendy(\s|\-|\_)Williams|Wenona|Wesley(\s|\-|\_)Pipes|Whitney(\s|\-|\_)Conroy|Whitney(\s|\-|\_)Stevens|Whitney(\s|\-|\_)Westgate|Whitney(\s|\-|\_)Wright|Wifey|Will(\s|\-|\_)Havoc|Will(\s|\-|\_)Powers|Winnie|Wiska|Wolf(\s|\-|\_)Hudson|Xander(\s|\-|\_)Corvus|Xandra(\s|\-|\_)Brill|Xandra(\s|\-|\_)Sixx|Xandy|Xanthia(\s|\-|\_)Doll|Xavi(\s|\-|\_)Tralla|Xeena(\s|\-|\_)Mae|Xenia(\s|\-|\_)Blue|Xev(\s|\-|\_)Delexx|Xianna(\s|\-|\_)Hill|Xu(\s|\-|\_)Jinglei|XxLayna(\s|\-|\_)Marie|Yago(\s|\-|\_)Ribeiro|Yasmin(\s|\-|\_)Andrade|Yasmin(\s|\-|\_)Dornelles|Yasmin(\s|\-|\_)Hernandez|Yasmin(\s|\-|\_)Pires|Yasmine(\s|\-|\_)De(\s|\-|\_)Castro|Yris(\s|\-|\_)Schimit"
	stars4 := "(Yukki(\s|\-|\_)Amey|Yume(\s|\-|\_)Farias|Yumi(\s|\-|\_)Sin|Yummy(\s|\-|\_)Niki|Yuri(\s|\-|\_)Himeno|Zac(\s|\-|\_)Wild|Zafira|Zarina|Zazie(\s|\-|\_)Skymm|Zelda(\s|\-|\_)Bee|Zena(\s|\-|\_)Little|Ziggy(\s|\-|\_)Star|nana(\s|\-|\_)garnet|florane(\s|\-|\_)russell|nadja(\s|\-|\_)stone|anita(\s|\-|\_)peida|sia(\s|\-|\_)siberia|purple(\s|\-|\_)bitch|octokuro|aeries(\s|\-|\_)steele)"
	file_name := string_caseLower(var), save_name := file_name
	file_name := regexreplace(file_name, "imO)(\s)", "-")
	file_name := regexreplace(file_name, "imO)--", "-")
	file_name := regexreplace(file_name, "imO)---", "-")
	file_name := regexreplace(file_name, "imO)(-|\s)$")
	file_name := regexreplace(file_name, "imO)^(-|\s)")
	file_name := regexreplace(file_name, "imO),", "-")
	file_name := regexreplace(file_name, "imO)'") 
	file_name := regexreplace(file_name, "imO)(\s)", "-")
	file_name := regexreplace(file_name, "imO)---", "-")
	file_name := regexreplace(file_name, "imO)_-_", "-")
	file_name := regexreplace(file_name, "imO)-_", "-")
	file_name := regexreplace(file_name, "imO)-(?=\....$)")
	file_name := regexreplace(file_name, "imO),", "-")
	file_name := regexreplace(file_name, "imO)&")
	file_name := regexreplace(file_name, "imO)!")
	var := file_name
	return var
}
