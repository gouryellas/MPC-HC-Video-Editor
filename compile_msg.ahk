msg(var, var2:="", var3:="", var4:="",var5:="",var6:="") {
	 if (var6 != "")
		msgbox, 262144, ,1 > %var%`n2 > %var2%`n3 > %var3%`n4 > %var4%`n5 > %var5%`n6 > %var6%
	else if (var5 != "")
		msgbox, 262144, ,1 > %var%`n2 > %var2%`n3 > %var3%`n4 > %var4%`n5 > %var5%
	else if (var4 != "")
		msgbox, 262144, ,1 > %var%`n2 > %var2%`n3 > %var3%`n4 > %var4%
	else if (var3 != "")
		msgbox, 262144, ,1 > %var%`n2 > %var2%`n3 > %var3%
	else if (var2 != "")
		msgbox, 262144, ,1 > %var%`n2 > %var2%
	else {
		msgbox, 262144, ,%var%
	}}
