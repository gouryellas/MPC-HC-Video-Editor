number_max(ByRef string, delim:=",") {
	sort, string, nuD,
	found_pos := regexmatch(string, "im)\d+$", string)
	return string}number_roundWhole(var) {	if regexmatch(var, "imO)(\d+)\.(\d+)", get_num) {		if (get_num[2] >= 5)			num := get_num[1] + 1		else			num := get_num[1]		return num	}}
number_roundTen(byref num) {	listlines, off
	if (num >= 0) && if (num <= 10) 
		num := 10
	if (num > 10) && if (num <= 20)
		num := 20
	if (num > 20) && if (num <= 30)
		num := 30
	if (num > 30) && if (num <= 40)
		num := 40
	if (num > 40) && if (num <= 50)
		num := 50
	if (num > 50) && if (num <= 60)
		num := 60
	if (num > 60) && if (num <= 70)
		num := 70
	if (num > 70) && if (num <= 80)
		num := 80
	if (num > 80) && if (num <= 90)
		num := 90
	if (num > 90) && if (num <= 100)
		num := 100
	return num
	listlines, on}
number_random(var) {	listlines, off
	random, num, 1, var
	return num
	listlines, on}
number_count(string,delim:="`,",omits:="") {	listlines, off
	loop, parse, % string, %delim%, omits
		count := a_index
	return count
	listlines, on}

number_oddeven(num) {	listlines, off
	return ((num & 1) != 0) ? "Odd" : "Even"
	listlines, on}

number_zeros(var) {
	if (var = 1) 
		zeros := "000"
	else if (var = 2)
		zeros := "00"
	else if (var = 3)
		zeros := "0"
	else if (var = 4) or if (var = 6)
		zeros := ""
	else if (var = 5)
		zeros := "0"
	return zeros
}

number_countDigits(var) {
	if regexmatch(var, "imO)^\d{1}$")
		digits := 1
	else if regexmatch(var, "imO)^\d{2}$")
		digits := 2
	else if (var = "") {
		digits := 0
	}
	return digits
}