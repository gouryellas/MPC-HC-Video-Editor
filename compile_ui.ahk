ui_setup(name, options:="+owner +border -resize -maximizebox -sysmenu -caption -toolwindow -dpiscale -alwaysontop") {
	gui, %name%:default
	gui, margin, 0, 0
	gui, color, black, white
	gui, font, s14 cFFFFFF bold, Segoe UI
	gui, %options%
	return name}ui_basic(var) {	listlines, off	ui_destroy("basic")	gui, basic:default	gui, margin, 0, 0	gui, color, black, white	gui, font, s14 cFFFFFF bold, Segoe UI	gui, +owner +border -resize -maximizebox -sysmenu -caption -toolwindow -dpiscale -alwaysontop	ui_add_text(var)
	listlines, on	gui, show, NA, basic}ui_caret(var, num:="0") {	listlines, off	gui, destroy	gui, basic:default	gui, margin, 0, 0	gui, color, black, white	gui, font, s12 cFFFFFF bold, Segoe UI	gui, +owner +border -resize -maximizebox -sysmenu -caption -toolwindow -dpiscale +alwaysontop	xpos := "x" . a_caretx + 20 + num	ypos := "y" . a_carety + 20	ui_add_text(var, "+left")
	listlines, on	gui, show, %xpos% %ypos% NA, caret}
ui_size(options) {	listlines, off
	if regexmatch(options, "imO)(\d{1,4})x(\d{1,4})", ws) 
		options := window_size := "w" . ws[1] . " " . "h" . ws[2]
	listlines, on
	gui, show, %options% NA, %name%}
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
	gui, show, %options%, %name%}
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
	}}
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
	gui, %var%}
ui_show(name:="MyName") {
	gui, %name%:default
	gui, show, NA, %name%}
ui_hide(name:="MyName") {
	gui, %name%:default
	gui, hide}
ui_add_picture(value, options:="") {
	global
	listlines, off
	if regexmatch(options, "imO)(\+w)(\d+)", pcw) 
		options := strreplace(options, pcw[1], " w")
	if !regexmatch(options, "im)(\w)(\d+)") && regexmatch(gui_size, "imO)(\d+)x(\d+)", wh)
		options .= " w" . wh[1]
	options := strreplace(options, "transparent", "backgroundtrans 0x4000000")
	options := strreplace(options, "aspect", "h-1")
	if !regexmatch(options, "im)h-1")
		options .= " h-1"
	if !regexmatch(options, "im)backgroundtrans 0x4000000")
		options .= " backgroundtrans 0x4000000"
	listlines, on
	gui, add, picture, %options%, %value%}
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
	gui, add, text, %options%, %value%}
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
	gui, add, button, %options%, %value%}
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
	gui, add, radio, %options%, %value%}
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
	gui, add, edit, %options%, %value%}
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
	gui, add, progress, %options%, %percent%}

ui_dynamic_height(string, num:=26.5, delim:=",") {
	loop, parse, string, %delim%
		count := A_Index
	return count * num}
ui_dynamic_width(ByRef string, num:=11, delim:=",") {	listlines, off
	loop, parse, string, %delim% 
		new_string .= StrLen(A_Loopfield) . ", "
	string := strreplace(new_string, A_Space, "")
	string := regexreplace(string, "im)\,$")
	sort, string, nuD,
	found_pos := regexmatch(string, "im)\d+$", count)
	count += 5
	return count * num
	listlines, on}
ui_movedraw(control, options:="") {	listlines, off
	guicontrol, %name%:movedraw, %control%, %options%
}
ui_destroy(name:="") {	listlines, off
	if (name = "")
		name := this.name
	listlines, on
	gui, %name%:destroy}
ui_work_folder(working_dir:="A_WorkingDir", byref var:="") {
	local
	var := working_dir
	return var
	listlines, on}



	/*
	value(value, control:="") {	listlines, off
		found_progress := regexmatch(value, "im)(black|silver|gray|maroon|red|purple|fuschia|green|lime|olive|navy|blue|teal|yellow|aqua|orange)", color)
		found_picture := regexmatch(value, "im)^(C|D|E|F|G|H)\:\\.+")
		if (found_progress > 0) {
			if (control = "")
				control := "MyProgress"
			found_percent := regexmatch(value, "im)(\d+)", percent)
			get_color := object("black", "000000", "silver", "c0c0c0", "gray", "808080", "maroon", "800000", "red", "ff0000", "purple", "800080", "fuchsia", "ff00ff", "green", "008000", "lime", "00ff00", "olive", "808000", "yellow", "ffff00", "navy", "000080", "blue", "0000ff", "teal", "008080", "aqua", "00ffff", "orange", "ffa500")
			value := color = "yellow" ? get_color.yellow : color = "black" ? get_color.black : color = "silver" ? get_color.silver : color = "gray" ? get_color.gray : color = "maroon" ? get_color.maroon : color = "red" ? get_color.red : color = "purple" ? get_color.purple : color = "fuchsia" ? get_color.fuchsia : color = "green" ? get_color.green : color = "lime" ? get_color.lime : color = "olive" ? get_color.olive : color = "navy" ? get_color.navy : color = "blue" ? get_color.blue : color = "teal" ? get_color.teal : color = "aqua" ? get_color.aqua : color = "orange" ? get_color.orange	
			colors := bar_color . " " . font_color . " " . win_color
			guicontrol, , %control%, %percent%
			guicontrol, %value%, %control%	
		} else if (found_picture > 0) {
			if (control = "")
				control := "MyPicture"
			guicontrol, , %control%, %value%
		} else {
			if (control = "")
				control := "MyText"
			guicontrol, Text, %control%, %value%
			guicontrol, , %control%, %value%
		}
	}
	*/