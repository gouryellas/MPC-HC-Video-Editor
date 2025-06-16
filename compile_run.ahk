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
