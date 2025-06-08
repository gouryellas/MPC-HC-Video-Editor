window_waitExist(title:="a", time:="") {	listlines, off
	WinWait, %title%, , %time%
	return errorlevel
	listlines, on}
window_waitClose(title:="a", time:="") {
	WinWaitClose, %title%, , %time%
	return errorlevel}
window_waitNotActive(title:="a", time:="") {
	WinWaitNotActive, %title%, , %time%
	return errorlevel}
window_waitActive(title:="", time:="") {	if title = ""
		WinWaitActive, , , %time%	else		WinWaitActive, %title%, , %time%
	return errorlevel}
window_maximize(title:="a") {
	winmaximize, %title%
	winget, max_status, minmax, %title%
	if (max_status = 0) 
		winactivate, %title%}
window_minimize(title:="a") {
	winminimize, %title%	winget, max_status, minmax, %title%		if (max_status != -1) 		winminimize, %title%}window_minmax(title:="a") {	winget, max_status, minmax, %title%	if max_status = -1		max_status := "minimized"	else if max_status = 1		max_status := "maximized"	return max_status}
window_activate(title:="a") {
	winactivate, %title%}
window_kill(title:="a") {	listlines, off
	winkill, %title%
	listlines, on}
window_hide(title:="a") {	listlines, off
	winhide, %title%
	listlines, on}
window_show(tile:="a") {	listlines, off
	winshow, %title%
	listlines, on}
window_exist(title:="a") {	listlines, off
	if winexist(title) 
		return 1
	else
		return 0
	listlines, on}
window_active(title:="a") {
	if winactive(title) 
		return 1
	return 0}
window_getActive(ByRef var:="") {	listlines, off
	WinGetActiveTitle, var
	return var
	listlines, on}
window_id(title:="a") {	listlines, off
	winget, uservar, id, %title%
	return uservar
	listlines, on}
window_pid(title:="a") {	listlines, off
	winget, uservar, pid, %title%
	return uservar
	listlines, on}
window_path(title:="a") {	listlines, off
	winget, uservar, processpath, %title%
	return uservar
	listlines, on}
window_process(title:="a") {	listlines, off
	winget, uservar, processname, %title%
	return uservar
	listlines, on}
window_controls(title:="a") {	listlines, off
	winget, uservar, controllist, %title%
	return uservar
	listlines, on}window_title(title = "a") {    if InStr(title, "ahk_exe") {        title := StrReplace(title, "ahk_exe ", "")        WinGet, windowList, List, ahk_exe %title%        Loop, %windowList% {            windowID := windowList%A_Index%            WinGetTitle, currentTitle, ahk_id %windowID%            if (currentTitle != "")                return currentTitle        }    } else {        WinGetTitle, uservar, %title%        return uservar    }    return ""}window_setTitle(new_title, title:="a") {	listlines, off	winsettitle, %title%, , %new_title%	listlines, on}
window_text(title:="a") {	listlines, off
	wingettext, uservar, text, %title%
	return uservar
	listlines, on}
window_class(title:="a") {
	wingetclass, uservar, class, %title%
	return uservar}
window_stats(title:="a") {	wingetpos, x, y, width, height, %title%	if (title = "a")		wingetactivestats, blank, width, height, x, y	uservar := {}
	uservar.width := width - 5	uservar.height := height - 5	uservar.x := x	uservar.y := y
	return uservar}
window_resize(var, title:="a") {
	if regexmatch(var, "imO)(\d+)\s?(\d+)?", size) {
		width := size[1]
		height := size[2]
	}
	winmove, %title%, , , , %width%, %height%}
window_move(var, title:="a") {
	if regexmatch(var, "imO)(\d+)(\s\d+)?", pos) {
		xpos := pos[1]
		ypos := pos[2]
	}
	winmove, %title%, , %xpos%, %ypos%}window_ontop(title:="a") {	WinGet, exStyle, ExStyle, %title%	WS_EX_TOPMOST := 0x00000008	if (exStyle & WS_EX_TOPMOST) {		winset, alwaysontop, off, %title%		window_setTitle(regexreplace(window_title(), "imO)\*{8}"))	} else {		winset, alwaysontop, on, %title%		window_setTitle("******** " . window_title())	}}
