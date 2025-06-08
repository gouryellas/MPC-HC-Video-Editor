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