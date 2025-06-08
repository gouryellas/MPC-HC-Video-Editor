path_join(var1, var2:="", var3:="") {
	if !regexmatch(var1, "imO)(.+)\\$")
		var1 := var1 . "\"
	if regexmatch(var2, "imO)(.+)\.(mp4|avi|mpg|mpeg|mov|wmv|flv|csv|mkv)$", file) {
		file_ext := file[1] . "." . file[2]
		return var1 . file_ext
	} else if regexmatch(var3, "imO)(.+)\.(mp4|avi|mpg|mpeg|mov|wmv|flv|csv|mkv)$", file) {
		file_ext := file[1] . "." . file[2]
		if regexmatch(var2, "imO)[^\\](.+)[^\\]$")
			return var1 . var2 . file_ext
		if !regexmatch(var2, "imO)(.+)\\$")
			return var1 . var2 . "\" . file_ext
		else
			return var1 . var2 . file_ext
	} else if regexmatch(var3, "imO)\.(mp4|avi|mpg|mpeg|mov|wmv|flv|csv|mkv)$", file) {
		ext := "." . file[1]
		return var1 . var2 . ext
	} else {
		if !regexmatch(var2, "imO)(.+)\\$")
			return var1 . var2 . "\"
		else
			return var1 . var2
	}
	
}