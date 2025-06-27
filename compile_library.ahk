	control_getText(var, title:="a", byref save:="") {
		controlgettext, save, %var%, %title%
		return save
	}
	
	control_getSize(control, title:="a") {
		WinGet, windowID, id, %title%
		controlgetpos, x, y, width, height, %control%, ahk_id %windowID%
		save := {}
		save.width := width
		save.height := height
		return save
	}
	
	control_getPos(control, title:="a") {
		WinGet, windowID, id, %title%
		controlgetpos, x, y, width, height, %control%, ahk_id %windowID%
		save := {}
		save.x := x
		save.y := y
		return save
	}

csv_lastline(var) {
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
csv_linecount(var) {
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
csv_data(var, line:="") {
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
csv_linedelete(var, number) {
        file := FileOpen(var, "r")
        data := file.Read()
        file.close()
        if instr(data, "`n")
                delim := "`n"
        else
                delim := "`r"
        loop, parse, % data, %delim%
                {
                if (a_index != number)
                        new_data .= a_loopfield . delim
        }
        file_write(var, new_data, "w")
        return new_data
}

csv_removeBlankLines(var) {
	data := file_read(var)
	Loop, Parse, data, `n, `r
		{
		if (StrLen(A_LoopField) >= 1)  ; Only keep lines that aren’t empty
			new_data .= A_LoopField
	}
	file_delete(var)
	file_write(var, new_data)
	return new_data
}

error(var :="") {
	if (var = "")
		msgbox,52, %a_scriptname% - error!, Script has encountered an error.`n`nWould you like to see variables?
	else 
		msgbox, 52, Error! - %a_scriptname%, %var%`n`n Would you like to see variables?
	ifmsgbox, yes 
		{
		listvars
		return
	}
	ifmsgbox, no
		{
		if !window_exist("choose")
			reload
	}
}

file_setAttributes(source, attribute) {
	if (attribute = "hidden")
		filesetattrib, +h, %source%
	if (attribute = "unhidden")
		filesetattrib, -h, %source%
}
file_getAttributes(source) {
	filegetattrib, var, %source%
	return var
}
file_delete(source) {
	filedelete, %source%
	return errorlevel
}
file_tag(option:="") {
	sendinput % "{lalt down}{enter down}{enter up}{lalt up}"
	winwaitactive, ahk_class #32770
	control, tabright, 2, systabcontrol321
	s(100)	
	wingettext, get_tab, Properties
	if regexmatch(get_tab, "im)previous", save_var)
		control, tableft, 1, systabcontrol321
	sendinput % "{down}{t}"
	s(100)
	if (option != "")
		sendinput % option
	else 
		sendinput % "cut"
	s(250)
	sendinput % "{enter down}{enter up}"
	controlsend, button1, {enter}, Properties
}

file_rename(var) {
	file_path := explorer_getSelected()
	file_name := explorer_getSelectedFile()
	file_directory := explorer_getPath()

}

file_move(source, target) {
	if regexmatch(source, "imO).+\.(...|..)$")
		FileMove, %source%, %target%
	else
		FileMoveDir, %source%, %target%, r
}
file_recycle(source1, source2:="", source3:="", source4:="") {
	if instr(source1, " ") {
		new_source1 := strreplace(source1," ","-")
		file_move(source1, new_source1)
		source1 := new_source1
	}
	filerecycle %source1%
	if (source2 != "") {
		if instr(source2, " ") {
			new_source2 := strreplace(source2," ","-")
			file_move(source2, new_source2)
			source2 := new_source2
		}
		filerecycle %source2%
	}
	if (source3 != "") {
		if instr(source3, " ") {
			new_source3 := strreplace(source3," ","-")
			file_move(source3, new_source3)
			source3 := new_source3
		}
		filerecycle %source3%
	}
	if (source4 != "") {
		if instr(source4, " ") {
			new_source4 := strreplace(source4," ","-")
			file_move(source4, new_source4)
			source4 := new_source4
		}
		filerecycle %source4%
	}
	return errorlevel
}
file_properties() {
	sendinput % "{lalt down}{enter down}{enter up}{lalt up}"
}
file_read(file, check:="") {
	file := FileOpen(file, "r")
	data := file.Read()
	if (check = "check") {
		if (data = "")
			error("Data is blank!")
	}
	file.close()
	return data
}
file_parse(data, var:="") {
	if (var = "lastline") {
		loop, parse, % data, `r
			{
			if (a_loopfield != "")
				line := a_loopfield
		}
		return line
	}
}
file_write(file, string, mode:="a") {
	file := FileOpen(file, "" . mode . "")
	file.write(string)
	file.close()
}
file_writeline(file, string, mode:="a") {
	file := FileOpen(file, "" . mode . "")
	file.writeline(string)
	file.close()
}
file_replace(file, string) {
	file_write(file, string, "w")
}

file_extension(string, extension) {
	return regexmatch(string, "imO)(.+)\." . extension)
}

mpc_getSource(byref var:="") {
	window_activate("ahk_class MPC-BE")
	regexmatch(window_title("ahk_class MPC-BE"), "imO)(.+\\)(.+)(?=\s-\sMPC-BE)", get_source)
	global videoplayer_global_path := get_source[1] . get_source[2]
	var := get_source[1] . get_source[2]
	return var
}
mpc_getName(byref var:="") {
	regexmatch(window_title("ahk_class MPC-BE"), "imO)(.+)\s\-\s", file_path)
	file_path := file_path[1]
	splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
	var := file_name
	return var
}
mpc_getPath(byref var:="") {
	regexmatch(window_title("ahk_class MPC-BE"), "imO)(.+)\s\-\s", file_path)
	file_path := file_path[1]
	splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
	var := file_path
	return var
}
mpc_getExt(byref var:="") {
	file_path := window_title("ahk_class MPC-BE")
	window_activate("ahk_class MPC-BE")
	regexmatch(window_title("ahk_class MPC-BE"), "imO)(\....$)", get_ext)
	var := get_ext[1]
	return var
}
mpc_getDuration(byref var:="") {
	controlgettext, get_duration, Static3, ahk_class MPC-BE
	regexmatch(get_duration, "imO)(\d{2})\:(\d{2})\:(\d{2})", time)
	raw_time := time[0]
	time_hours := time[1], time_minutes := time[2], time_seconds := time[3]
	total_seconds := (time_hours * 3600) + (time_minutes * 60) + time_seconds
	var := total_seconds
	return var
}
mpc_getTime(byref var:="") {
	controlgettext, get_duration, Static3, ahk_class MPC-BE
	if instr(get_duration,"-") {
		window := window_stats("ahk_class MPC-BE")
		window.width -= 20
		window.height -= 20
		click_relative(window.width . " " . window.height)
		controlgettext, get_duration, Static3, ahk_class MPC-BE
	}
	if regexmatch(get_duration, "imO)^(\d{2})\:(\d{2})\s.+", time)
		var := "00:" . time[1] . ":" . time[2]
	else if regexmatch(get_duration, "imO)(\d{2})\:(\d{2})\:(\d{2})\s.+", time)
		var := time[1] . ":" . time[2] . ":" . time[3]
	return var
}
mpc_getTotalTime(byref var:="") {
	controlgettext, get_duration, Static3, ahk_class MPC-BE
	if regexmatch(get_duration, "imO).+\/\s(.+)", var2)
		if regexmatch(var2[1], "imO)^(\d{2})\:(\d{2})$", time)
			var := "00:" . time[1] . ":" . time[2]
		else if regexmatch(var2[1], "imO)^(\d{2})\:(\d{2})\:(\d{2})$", time)
			var := time[1] . ":" . time[2] . ":" . time[3]	
	return var
}
mpc_fileDelete() {
	filedelete % file_path
	send("{alt}{right}", "down")
}
mpc_playNext() {
	sendinput % "{xbutton1}"
}
mpc_addBookmark() {
	sendinput % "{p down}{p up}"
}
mpc_seek(var) {
	send("ctrl g", "down")
	window_waitActive("Go To...")
	clipboard := var
	if regexmatch(var, "imO)^00(......\.000)$", change)
		var := change[1]
	send(var)
	send("{enter}")	
}

msg(var, var2:="", var3:="", var4:="",var5:="",var6:="") {
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

number_max(ByRef string, delim:=",") {
	sort, string, nuD,
	found_pos := regexmatch(string, "im)\d+$", string)
	return string
}
number_roundWhole(var) {
	if regexmatch(var, "imO)(\d+)\.(\d+)", get_num) {
		if (get_num[2] >= 5)
			num := get_num[1] + 1
		else
			num := get_num[1]
		return num
	}
}

number_roundTen(byref num) {
	listlines, off
	if (num >= 0) && if (num <= 10) 
		num := 10
	if (num > 10) && if (num <= 20)
		num := 20
	if (num > 20) && if (num <= 30)
		num := 30
	if (num > 30) && if (num <= 40)
		num := 40
	if (num > 40) && if (num <= 50)
		num := 50
	if (num > 50) && if (num <= 60)
		num := 60
	if (num > 60) && if (num <= 70)
		num := 70
	if (num > 70) && if (num <= 80)
		num := 80
	if (num > 80) && if (num <= 90)
		num := 90
	if (num > 90) && if (num <= 100)
		num := 100
	return num
	listlines, on
}
number_random(var) {
	listlines, off
	random, num, 1, var
	return num
	listlines, on
}
number_count(string,delim:="`,",omits:="") {
	listlines, off
	loop, parse, % string, %delim%, omits
		count := a_index
	return count
	listlines, on
}

number_oddeven(num) {
	listlines, off
	return ((num & 1) != 0) ? "Odd" : "Even"
	listlines, on
}

number_zeros(var) {
	if (var = 1) 
		zeros := "000"
	else if (var = 2)
		zeros := "00"
	else if (var = 3)
		zeros := "0"
	else if (var = 4) or if (var = 6)
		zeros := ""
	else if (var = 5)
		zeros := "0"
	return zeros
}

number_countDigits(num) {
	Loop, Parse, num
		{
		if A_LoopField is digit
			digits++
	}
	return digits
}

path_join(var1, var2:="", var3:="") {
	if !regexmatch(var1, "imO)(.+)\\$")
		var1 := var1 . "\"
	if regexmatch(var2, "imO)(.+)\.(mp4|avi|mpg|mpeg|mov|wmv|flv|csv|mkv)$", file) {
		file_ext := file[1] . "." . file[2]
		return var1 . file_ext
	} else if regexmatch(var3, "imO)(.+)\.(mp4|avi|mpg|mpeg|mov|wmv|flv|csv|mkv)$", file) {
		file_ext := file[1] . "." . file[2]
		if regexmatch(var2, "imO)[^\\](.+)[^\\]$")
			return var1 . var2 . file_ext
		if !regexmatch(var2, "imO)(.+)\\$")
			return var1 . var2 . "\" . file_ext
		else
			return var1 . var2 . file_ext
	} else if regexmatch(var3, "imO)\.(mp4|avi|mpg|mpeg|mov|wmv|flv|csv|mkv)$", file) {
		ext := "." . file[1]
		return var1 . var2 . ext
	} else {
		if !regexmatch(var2, "imO)(.+)\\$")
			return var1 . var2 . "\"
		else
			return var1 . var2
	}
	
}

run(var, var2:="") {
	if (var = "regedit")
		Run *RunAs C:\Windows\Regedit.exe
	else if (var = "explorer")
		Run, %ComSpec% /c "start explorer.exe"
	else if (var2 = "hide")
		Run, % var, , hide
	else
		Run, % var
}

run_command(var1, var2:="", var3:="", var4:="") {
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

send(var, options:="", speed:="600") {
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
	Loop, 26 
		{ ; a-z
		keyMapping[Chr(Asc("a") + A_Index - 1)] := true
	}
	Loop, 10 
		{ ; 0-9
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
	Loop, 12
		{
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


	if instr(var, "down") and instr(var, "up")
		send_keys := var
	else if instr(options, "down") and !instr(var, "{") {
		if regexmatch(var, "imO)(.+)\s(.+)", key_found)
			send_keys := "{" . key_found[1] . " down}{" . key_found[2] . " down}{" . key_found[2] . " up}{" . key_found[1] . " up}"
	} else if regexmatch(var, "imO)^\{?(.+)\}?(\,|\s)?\{?(.+)\}?(\,|\s)?\{?(.+)\}?$", key_found) {
		if keyMapping.HasKey(key_found[1]) and keyMapping.HasKey(key_found[3]) and keyMapping.HasKey(key_found[5]) {
			if instr(options, "down")
				send_keys := "{" . key_found[1] . " down}{" . key_found[3] . " down}{" . key_found[5] . " down}{" . key_found[5] . " up}{" . key_found[3] . " up}{" . key_found[1] . " up}"
			else
				send_keys := "{" . key_found[1] . "}{" . key_found[2] . "}{" . key_found[3] . "}"
		} else
			send_keys := var
	} else if regexmatch(var, "imO)^\{?([^,}]+)\}?(?:[,\s]+)?\{?([^}]+)\}?$", key_found) {
		if keyMapping.HasKey(key_found[1]) and keyMapping.HasKey(key_found[2]) {
			if instr(options, "down")
				send_keys := "{" . key_found[1] . " down}{" . key_found[2] . " down}{" . key_found[2] . " up}{" . key_found[1] . " up}"
			else
				send_keys := "{" . key_found[1] . "}{" . key_found[2] . "}"
		} else
			send_keys := var
	} else if regexmatch(var, "imO)^\{?[^,]+\}?$", key_found) {
		if keyMapping.HasKey(key_found[1])
			send_keys := "{" . key_found[1] . " down}{" . key_found[1] . " up}"
		else
			send_keys := var
	}
	sendinput % send_keys
}


string_random(length:="10") {
    randomString := ""
    validChars := "abcdefghijklmnopqrstuvwxyz0123456789"

    Loop, %length% {
        randomIndex := number_Random(StrLen(validChars))
        randomChar := SubStr(validChars, randomIndex, 1)
        randomString .= randomChar
    }
    return randomString
}
string_trimRight(string, num:=1) {
	Loop %num%
		dot .= "."
	regexmatch(string, "imO)(.+)(" . dot . ")$", match)
	string := match[1]
	return string
	}
	
string_trimLeft(string, num:=1) {
	Loop %num%
		dot .= "."
	regexmatch(string, "imO)(" . dot . ")(.+)$", match)
	string := match[2]
	return string
}

string_trimSpaces(string) {
	string := strreplace(string, A_Space, "-")
	string := regexreplace(string, "im)\s+")
	string := regexreplace(string, "im)\s+$")
	string := regexreplace(string, "im)^\s+")
	return string
	listlines, on
}
string_removeDuplicates(string, delim:=",") {
	listlines, off
	sort, string, uD%delim%
	return string
	listlines, on
}
string_removeSpaces(string) {
	listlines, off
	return string := strreplace(string, A_Space, "")
	listlines, on
}
string_case(string:="") {
	listlines, off
	num := number_random(5)
	selection := num = 1 ? string_caseUpper(string) : num = 2 ? string_caseTitle(string) : num = 3 ? string_caseLower(string) : num = 4 ? string_caseBlend(string) : num = 5 ? string_caseSentence : selection
	return selection
	listlines, on
}
string_caseUpper(string:="") {
	listlines, off
	if (string != "") {
		StringUpper, caseUpper, string
		return caseUpper
	} else {
		copy(selected)
		wait(1)
		StringUpper, caseUpper, selected
		sendinput % caseUpper
	}
	listlines, on
}
string_caseTitle(string:="") {
	listlines, off
	if (string != "") {
		StringUpper, caseTitle, string, T
		return caseTitle
	} else {
		copy(selected)
		wait(1)
		StringUpper, caseTitle, string, T
		sendinput % caseTitle
	}
	listlines, on
}
string_caseLower(string:="") {
	listlines, off
	if (string != "") {
		StringLower caseLower, string
		return caseLower
	} else {
		copy(selected)
		wait(1)
		StringLower, caseLower, string
		sendinput % caseLower
	}
	listlines, on
}
string_caseBlend(string:="") {
	listlines, off
	if (string != "") {
		string := string_caseLower(string)
		return RegExReplace(string, "(((^|([.!?]\s+))[a-z])| i | i')", "$u1")
	} else {
		copy(selected)
		ClipWait
		selected := string_caseLower(selected)
		selected := RegExReplace(selected, "(((^|([.!?]\s+))[a-z])| i | i')", "$u1")
		sendinput % selected
	}
	listlines, on
}
string_caseSentence(string:="") {
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

string_JoinAfter(var1, var2) {
	new_string := var1 . var2
	return new_string
}

string_JoinBefore(var1, var2) {
	new_string := var2 . var1
	return new_string
}

string_CountCharacter(string, character) {
    count := 0
    loop, parse, string
    {
        if (a_loopfield = character)
            count++
    }
    return count
}

InvertCase(str) {
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

special(hrtime, total_time) {
	num := number_countDigits(hrtime)
	if regexmatch(total_time, "imO)(\d{2}):(\d{2}):(\d{2})", time_seek) {
		time_seeks := time_seek[1]
	}
	if (num = 6)
		prefix := ""
	else if (num = 5)
		prefix := "0"
	else if (num = 4) {
		if (time_seeks != "00")
			prefix := "00"
		else
			prefix := ""
	} else if (num = 3) {
		if (time_seeks != "00")
			prefix := "000"
		else
			prefix := "0"
	} else if (num = 2) {
		if (time_seeks != "00")
			prefix := "0000"
		else
			prefix := "00"
	} else if (num = 1) {
		if (time_seeks != "00")
			prefix := "00000"
		else
			prefix := "000"
	}
	return prefix
}

time_LongToBookmark(hrTime, total_time) {

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

time_secToLong(var) {
    hours := Floor(var / 3600)
    rem := var - (hours * 3600)

    minutes := Floor(rem / 60)
    seconds := rem - (minutes * 60)

    hours := (hours < 10) ? "0" hours : hours
    minutes := (minutes < 10) ? "0" minutes : minutes
    seconds := (seconds < 10) ? "0" seconds : seconds

    return hours ":" minutes ":" seconds
}

time_secToShort(var) {
	var := time_secToLong(var)
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


time_secToAlt(var) {
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

time_shortToLong(var) {
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

time_shortToSec(var) {
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

time_shortToAlt(var) {
	time1 := time_shortToSec(var)
	time := time_secToAlt(time1)
	return time
}

time_longToSec(var) {
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

time_longToShort(var) {
	time := time_longToSec(var)
	time := time_secToShort(time)
	return time
}

time_longToAlt(var) {
	if regexmatch(var, "imO)(\d+)\:(\d+)\:(\d+)", getting_time) {
		hours := getting_time[1]
		minutes := getting_time[2]
		seconds := getting_time[3]
		hours := (hours != "00") ? hours + 0 "h" : ""
        minutes := (minutes != "00") ? minutes + 0 "m" : ""
        seconds := (seconds != "00") ? seconds + 0 "s" : ""
        time := ""
        if (hours != "") 
            time .= hours
        if (minutes != "") 
            time .= (time != "" ? " " : "") minutes
        if (seconds != "") 
            time .= (time != "" ? " " : "") seconds
        return time
	}
}

ui_setup(name, options:="+owner +border -resize -maximizebox -sysmenu -caption -toolwindow +DPIScale -alwaysontop") {
	gui, %name%:default
	gui, margin, 0, 0
	gui, color, black, white
	gui, font, s14 cFFFFFF bold, Segoe UI
	gui, %options%
	return name
}

ui_basic(var) {
	listlines, off
	ui_destroy("basic")
	gui, basic:default
	gui, margin, 0, 0
	gui, color, black, white
	gui, font, s14 cFFFFFF bold, Segoe UI
	gui, +owner +border -resize -maximizebox -sysmenu -caption -toolwindow -dpiscale -alwaysontop
	ui_add_text(var)
	listlines, on
	gui, show, NA, basic
}

ui_caret(var, num:="0") {
	listlines, off
	gui, destroy
	gui, basic:default
	gui, margin, 0, 0
	gui, color, black, white
	gui, font, s12 cFFFFFF bold, Segoe UI
	gui, +owner +border -resize -maximizebox -sysmenu -caption -toolwindow -dpiscale +alwaysontop
	xpos := "x" . a_caretx + 20 + num
	ypos := "y" . a_carety + 20
	ui_add_text(var, "+left")
	listlines, on
	gui, show, %xpos% %ypos% NA, caret
}

ui_size(options) {
	listlines, off
	if regexmatch(options, "imO)(\d{1,4})x(\d{1,4})", ws) 
		options := window_size := "w" . ws[1] . " " . "h" . ws[2]
	listlines, on
	gui, show, %options% NA, %name%
}
ui_pos(options) {
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
ui_color(options, control:="") {
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
ui_font(options) {
	listlines, off
	if ff = ""
		ff := "Segoe UI"
	if regexmatch(options, "imO)(?<=\+)?(bold|italic|underline|norm)(\d+)?", ft) {
		options := regexreplace(options, "im)(\+)(bold|italic|underline|norm)", "$2")
		if (ft[1] = "bold") && if (ft[2] != "")
			options := strreplace(options, ft[0], " w" . ft[2])
		if (ft[1] = "bold") && if (ft[2] = "")
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
ui_options(var:="") {
	if (var = "")
		var := "+owner +border -resize -maximizebox -sysmenu -caption -toolwindow -alwaysontop "
	gui, %var%
}
ui_show(name:="MyName") {
	gui, %name%:default
	gui, show, NA, %name%
}
ui_hide(name:="MyName") {
	gui, %name%:default
	gui, hide
}
ui_add_picture(value, options:="") {
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
ui_add_text(value, options:="") {
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
	else
		options .= " center"
	if regexmatch(options, "imO)(\+)?(black|silver|gray|maroon|red|purple|fuschia|green|lime|olive|navy|blue|teal|yellow|aqua|orange|white)", c)
		options := strreplace(options, "+", " c")
		options := strreplace(options, "+")
	listlines, on
	gui, add, text, %options%, %value%
}
ui_add_button(value:="OK", options:="") {
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
ui_add_radio(value:="", options:="Checked") {
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
ui_add_checkbox(value:="", options:="checkedGray") {
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

ui_add_edit(value:="", options:="") {
	global
	listlines, off
	if regexmatch(options, "imO)(\+w)(\d+)", bcw) 
		options := strreplace(options, bcw[1], "w")
	if !regexmatch(options, "imO)(w)(\d+)") && regexmatch(gui_size, "imO)(\d+)x(\d+)", wh)
		options .= " w" . wh[1]
	if regexmatch(options, "imO)(\+v)(\w+)", bv)
		options := strreplace(options, bv[1], " v")
	else
		options .= " v" . value
	listlines, on	
	gui, add, edit, %options%, %value%
}
ui_add_progress(options:="") {
	global
	listlines, off
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
	listlines, on
	gui, add, progress, %options%, %percent%
}

ui_dynamic_height(string, num:=26.5, delim:=",") {
	loop, parse, string, %delim%
		count := A_Index
	return count * num
}
ui_dynamic_width(ByRef string, num:=11, delim:=",") {
	listlines, off
	loop, parse, string, %delim% 
		new_string .= StrLen(A_Loopfield) . ", "
	string := strreplace(new_string, A_Space, "")
	string := regexreplace(string, "im)\,$")
	sort, string, nuD,
	found_pos := regexmatch(string, "im)\d+$", count)
	count += 5
	return count * num
	listlines, on
}
ui_movedraw(control, options:="") {
	listlines, off
	guicontrol, %name%:movedraw, %control%, %options%
}
ui_destroy(name:="") {
	listlines, off
	if (name = "")
		name := this.name
	listlines, on
	gui, %name%:destroy
}
ui_work_folder(working_dir:="A_WorkingDir", byref var:="") {
	local
	var := working_dir
	return var
	listlines, on
}

wait(var:="100") {
	listlines, off
	if regexmatch(var, "imO)^[1-9][0-9]?$")
		var .= "000"
	sleep, %var%
	listlines, on
}

window_waitExist(title:="a", time:="") {
	listlines, off
	WinWait, %title%, , %time%
	return errorlevel
	listlines, on
}
window_waitClose(title:="a", time:="") {
	WinWaitClose, %title%, , %time%
	return errorlevel
}
window_waitNotActive(title:="a", time:="") {
	WinWaitNotActive, %title%, , %time%
	return errorlevel
}
window_waitActive(title:="", time:="") {
	if title = ""
		WinWaitActive, , , %time%
	else
		WinWaitActive, %title%, , %time%
	return errorlevel
}
window_maximize(title:="a") {
	winmaximize, %title%
	winget, max_status, minmax, %title%
	if (max_status = 0) 
		winactivate, %title%
}
window_minimize(title:="a") {
	winminimize, %title%
	winget, max_status, minmax, %title%
		if (max_status != -1) 
		winminimize, %title%
}
window_minmax(title:="a") {
	winget, max_status, minmax, %title%
	if max_status = -1
		max_status := "minimized"
	else if max_status = 1
		max_status := "maximized"
	return max_status
}
window_activate(title:="a") {
	winactivate, %title%
}
window_kill(title:="a") {
	listlines, off
	winkill, %title%
	listlines, on
}
window_hide(title:="a") {
	listlines, off
	winhide, %title%
	listlines, on
}
window_show(tile:="a") {
	listlines, off
	winshow, %title%
	listlines, on
}
window_exist(title:="a") {
	listlines, off
	if winexist(title) 
		return 1
	else
		return 0
	listlines, on
}
window_active(title:="a") {
	if winactive(title) 
		return 1
	return 0
}
window_getActive(ByRef var:="") {
	listlines, off
	WinGetActiveTitle, var
	return var
	listlines, on
}
window_id(title:="a") {
	listlines, off
	winget, uservar, id, %title%
	return uservar
	listlines, on
}
window_pid(title:="a") {
	listlines, off
	winget, uservar, pid, %title%
	return uservar
	listlines, on
}
window_path(title:="a") {
	listlines, off
	winget, uservar, processpath, %title%
	return uservar
	listlines, on
}
window_process(title:="a") {
	listlines, off
	winget, uservar, processname, %title%
	return uservar
	listlines, on
}

window_controls(title:="a") {
	listlines, off
	winget, uservar, controllist, %title%
	return uservar
	listlines, on
}

window_title(title = "a") {
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

window_setTitle(new_title, title:="a") {
	listlines, off
	winsettitle, %title%, , %new_title%
	listlines, on
}

window_text(title:="a") {
	listlines, off
	wingettext, uservar, text, %title%
	return uservar
	listlines, on
}

window_class(title:="a") {
	wingetclass, uservar, class, %title%
	return uservar
}

window_stats(title:="a") {
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

window_resize(var, title:="a") {
	if regexmatch(var, "imO)(\d+)\s?(\d+)?", size) {
		width := size[1]
		height := size[2]
	}
	winmove, %title%, , , , %width%, %height%
}

window_move(var, title:="a") {
	if regexmatch(var, "imO)(\d+)(\s\d+)?", pos) {
		xpos := pos[1]
		ypos := pos[2]
	}
	winmove, %title%, , %xpos%, %ypos%
}

window_ontop(title:="a") {
	WinGet, exStyle, ExStyle, %title%
	WS_EX_TOPMOST := 0x00000008
	if (exStyle & WS_EX_TOPMOST) {
		winset, alwaysontop, off, %title%
		window_setTitle(regexreplace(window_title(), "imO)\*{8}"))
	} else {
		winset, alwaysontop, on, %title%
		window_setTitle("******** " . window_title())
	}
}


