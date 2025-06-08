run(var, var2:="") {
;	if !instr(var, "python.exe") {            
;		if instr(var, ".py") {
;			var := "python.exe ""c:\users\chris\desktop\new working files\working\python-script\rename_file2.py"""
;		}
;	}
	if (var = "regedit")
		Run *RunAs C:\Windows\Regedit.exe
	else if (var = "explorer")
		run, %ComSpec% /c "start explorer.exe"
	else if (var2 = "hide")
		run, % var, , hide
	else
		run, % var}

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

    ; For debugging, you might temporarily use /k to see what's happening:
    ; Run %comspec% /k %parameters%
    ;
    ; Once confirmed working, revert to /c:
    Run %comspec% /c %var1% %var2%
}

/*
run_command(var1, var2:="", var3:="", var4:="") {
	if (var2 != "") AND if (var3 = "ne") {
		var1 := """" . var1 . """"
		if (var4 = "nohide") 
			Run  %comspec% /c %var1% %var2%
		else
			Run  %comspec% /c %var1% %var2%, ,hide
	} else if (var2 != "") AND if (var3 != "ne") {
		var1 := """" . var1 . """"
		Run  %comspec% /c %var1% %var2%, ,hide
	}
	if (var2 = "") AND if (var3 = "ne") {
		Run  %comspec% /c "%var1%, ,hide
	}
	else if (var2 = "") AND if (var3 = "") {
		if instr(var1, "del") {
			Run *RunAs %comspec% /c %var1%, ,hide
		} else if instr(var1, " ") {
			var1 := """" . var1 . """"
			if (var4 = "nohide") 
				Run *RunAs %comspec% /c %var1%
			else
				Run *RunAs %comspec% /c %var1%, ,hide
		} else
			Run *RunAs %comspec% /c "%var1%, ,hide
	}}
*/