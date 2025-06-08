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