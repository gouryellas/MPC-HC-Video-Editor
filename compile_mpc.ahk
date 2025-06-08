mpc_getSource(byref var:="") {
	window_activate("ahk_exe mpc-be64.exe")
	regexmatch(window_title("ahk_exe mpc-be64.exe"), "imO)(.+\\)(.+)(?=\s-\sMPC-BE)", get_source)
	global videoplayer_global_path := get_source[1] . get_source[2]
	var := get_source[1] . get_source[2]
	return var}
mpc_getName(byref var:="") {
	regexmatch(window_title("ahk_exe mpc-be64.exe"), "imO)(.+)\s\-\s", file_path)	file_path := file_path[1]	splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
	var := file_name
	return var}
mpc_getPath(byref var:="") {	regexmatch(window_title("ahk_exe mpc-be64.exe"), "imO)(.+)\s\-\s", file_path)	file_path := file_path[1]	splitpath, file_path, file_both, file_directory, file_extension, file_name, file_drive
	var := file_path
	return var}
mpc_getExt(byref var:="") {
	window_activate("ahk_exe mpc-be64.exe")
	regexmatch(window_title("ahk_exe mpc-be64.exe"), "imO)(\....$)", get_ext)
	var := get_ext[1]
	return var}
mpc_getDuration(byref var:="") {
	controlgettext, get_duration, Static3, MPC-BE
	regexmatch(get_duration, "imO)(\d{2})\:(\d{2})\:(\d{2})", time)
	raw_time := time[0]
	time_hours := time[1], time_minutes := time[2], time_seconds := time[3]	total_seconds := (time_hours * 3600) + (time_minutes * 60) + time_seconds
	var := total_seconds
	return var}
mpc_getTime(byref var:="") {
	controlgettext, get_duration, Static3, MPC-BE
	if instr(get_duration,"-") {
		window := window_stats("ahk_exe mpc-be64.exe")
		window.width -= 20
		window.height -= 20
		click_relative(window.width . " " . window.height)
		controlgettext, get_duration, Static3, MPC-BE
	}
	if regexmatch(get_duration, "imO)^(\d{2})\:(\d{2})\s.+", time)
		var := "00:" . time[1] . ":" . time[2]
	else if regexmatch(get_duration, "imO)(\d{2})\:(\d{2})\:(\d{2})\s.+", time)
		var := time[1] . ":" . time[2] . ":" . time[3]
	return var}
mpc_getTotalTime(byref var:="") {
	controlgettext, get_duration, Static3, MPC-BE
	if regexmatch(get_duration, "imO).+\/\s(.+)", var2)
		if regexmatch(var2[1], "imO)^(\d{2})\:(\d{2})$", time)
			var := "00:" . time[1] . ":" . time[2]
		else if regexmatch(var2[1], "imO)^(\d{2})\:(\d{2})\:(\d{2})$", time)
			var := time[1] . ":" . time[2] . ":" . time[3]	
	return var
}
mpc_fileDelete() {	filedelete % file_path
	send("{alt}{right}", "down")
}
mpc_playNext() {
	sendinput % "{xbutton1}"}
mpc_addBookmark() {
	sendinput % "{p down}{p up}"}
mpc_seek(var) {
	send("ctrl g", "down")
	window_waitActive("Go To...")
	clipboard := var
	if regexmatch(var, "imO)^00(......\.000)$", change)
		var := change[1]
	send(var)
	send("{enter}")	
}