wait(var:="100") {	listlines, off
	if regexmatch(var, "imO)^[1-9][0-9]?$")
		var .= "000"
	sleep, %var%
	listlines, on}
