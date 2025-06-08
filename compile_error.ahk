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