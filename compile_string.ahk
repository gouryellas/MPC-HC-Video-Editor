string_random(length:="10") {    randomString := ""    validChars := "abcdefghijklmnopqrstuvwxyz0123456789"    Loop, %length% {        randomIndex := number_Random(StrLen(validChars))        randomChar := SubStr(validChars, randomIndex, 1)        randomString .= randomChar    }    return randomString}string_trimRight(string, num:=1) {
	Loop %num%
		dot .= "."
	regexmatch(string, "imO)(.+)(" . dot . ")$", match)
	string := match[1]
	return string	}	string_trimLeft(string, num:=1) {	Loop %num%		dot .= "."	regexmatch(string, "imO)(" . dot . ")(.+)$", match)	string := match[2]	return string}

string_trimSpaces(string) {
	string := strreplace(string, A_Space, "-")
	string := regexreplace(string, "im)\s+")
	string := regexreplace(string, "im)\s+$")
	string := regexreplace(string, "im)^\s+")
	return string
	listlines, on}
string_removeDuplicates(string, delim:=",") {	listlines, off
	sort, string, uD%delim%
	return string
	listlines, on}
string_removeSpaces(string) {	listlines, off
	return string := strreplace(string, A_Space, "")
	listlines, on}
string_case(string:="") {	listlines, off
	num := number_random(5)
	selection := num = 1 ? string_caseUpper(string) : num = 2 ? string_caseTitle(string) : num = 3 ? string_caseLower(string) : num = 4 ? string_caseBlend(string) : num = 5 ? string_caseSentence : selection
	return selection
	listlines, on}
string_caseUpper(string:="") {	listlines, off
	if (string != "") {
		StringUpper, caseUpper, string
		return caseUpper
	} else {
		copy(selected)
		wait(1)
		StringUpper, caseUpper, selected
		sendinput % caseUpper
	}
	listlines, on}
string_caseTitle(string:="") {	listlines, off
	if (string != "") {
		StringUpper, caseTitle, string, T
		return caseTitle
	} else {
		copy(selected)
		wait(1)
		StringUpper, caseTitle, string, T
		sendinput % caseTitle
	}
	listlines, on}
string_caseLower(string:="") {	listlines, off
	if (string != "") {
		StringLower caseLower, string
		return caseLower
	} else {
		copy(selected)		wait(1)
		StringLower, caseLower, string
		sendinput % caseLower
	}
	listlines, on}
string_caseBlend(string:="") {	listlines, off
	if (string != "") {
		string := string_caseLower(string)
		return RegExReplace(string, "(((^|([.!?]\s+))[a-z])| i | i')", "$u1")
	} else {
		copy(selected)
		ClipWait
		selected := string_caseLower(selected)
		selected := RegExReplace(selected, "(((^|([.!?]\s+))[a-z])| i | i')", "$u1")
		sendinput % selected
	}
	listlines, on}
string_caseSentence(string:="") {	listlines, off
	X = I,AHK,AutoHotkey

	string := RegExReplace(string, "[\.\!\?]\s+|\R+", "$0þ") ; mark 1st letters of sentences with char 254
	Loop Parse, string, þ
	{
		StringLower L, A_LoopField
		I := Chr(Asc(A_LoopField))
		StringUpper I, I
		S .= I SubStr(L,2)
	}
	Loop Parse, X, `,
		S := RegExReplace(S,"i)\b" A_LoopField "\b", A_LoopField)

	return S
	listlines, on}
string_JoinAfter(var1, var2) {	new_string := var1 . var2	return new_string}string_JoinBefore(var1, var2) {	new_string := var2 . var1	return new_string}string_CountCharacter(string, character) {    count := 0    loop, parse, string    {        if (a_loopfield = character)            count++    }    return count}





InvertCase(str) {	listlines, off
	Lab_Invert_Char_Out := ""
	Loop % Strlen(str) {
		Lab_Invert_Char := Substr(str, A_Index, 1)
		if Lab_Invert_Char is upper
			Lab_Invert_Char_Out := Lab_Invert_Char_Out Chr(Asc(Lab_Invert_Char) + 32)
		else if Lab_Invert_Char is lower
			Lab_Invert_Char_Out := Lab_Invert_Char_Out Chr(Asc(Lab_Invert_Char) - 32)
		else
			Lab_Invert_Char_Out := Lab_Invert_Char_Out Lab_Invert_Char
	}
	RETURN Lab_Invert_Char_Out
	listlines, on}
