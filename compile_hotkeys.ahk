#ifwinactive, ahk_exe mpc-be64.exe
	+space::edit_bookmarks("erase")
	MButton::edit_bookmarks("create")
#ifwinactive