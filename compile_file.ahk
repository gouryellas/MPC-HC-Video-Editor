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
	if regexmatch(source, "imO).+\.(...|..)$")		FileMove, %source%, %target%
	else
		FileMoveDir, %source%, %target%, r
}
file_recycle(source1, source2:="", source3:="", source4:="") {
	if instr(source1, " ") {
		new_source1 := strreplace(source1," ","-")
		file_move(source1, new_source1)
		source1 := new_source1
	}
	filerecycle %source1%	if (source2 != "") {
		if instr(source2, " ") {
			new_source2 := strreplace(source2," ","-")
			file_move(source2, new_source2)
			source2 := new_source2
		}		filerecycle %source2%
	}	if (source3 != "") {
		if instr(source3, " ") {
			new_source3 := strreplace(source3," ","-")
			file_move(source3, new_source3)
			source3 := new_source3
		}		filerecycle %source3%
	}	if (source4 != "") {
		if instr(source4, " ") {
			new_source4 := strreplace(source4," ","-")
			file_move(source4, new_source4)
			source4 := new_source4
		}		filerecycle %source4%
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