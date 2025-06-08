time_LongToBookmark(hrTime, total_time) {

	regexmatch(total_time, "imO)(\d{2}):(\d{2}):(\d{2})", time_seek)
	if regexmatch(hrTime, "imO)^(.)$", time)
		new_time := "000" . time[1] . ".000"
	else if regexmatch(hrTime, "imO)^(..)$", time)
		new_time := "00" . time[1] . ".000"
	else if regexmatch(hrTime, "imO)^(.):(..)$", time) {
		if (time_seek[1] = 02 or time_seek[1] = 01)
			new_time := "000" . time[1] . time[2] . ".000"
		else
			new_time := "0" . time[1] . time[2] . ".000"
	} else if regexmatch(hrTime, "imO)^(..):(..)$", time) {
		if (time_seek[1] = 02 or time_seek[1] = 01)
			new_time := "00" . time[1] . time[2] . ".000"
		else
			new_time := time[1] . time[2] . ".000"
	} else if regexmatch(hrTime, "imO)^(.):(..):(..)$", time)
		new_time := "0" . time[1] . time[2] . time[3] . ".000"
	else if regexmatch(hrTime, "imO)(..):(..):(..)", time) {

		if (time_seek[1] = 02 or time_seek[1] = 01)
			new_time := time[1] . time[2] . time[3] . ".000"
		else
			new_time := time[1] . time[2] . time[3] . ".000"
	}
	return new_time
}

time_secToLong(var) {
    hours := Floor(var / 3600)
    rem := var - (hours * 3600)

    minutes := Floor(rem / 60)
    seconds := rem - (minutes * 60)

    hours := (hours < 10) ? "0" hours : hours
    minutes := (minutes < 10) ? "0" minutes : minutes
    seconds := (seconds < 10) ? "0" seconds : seconds

    return hours ":" minutes ":" seconds
}

time_secToShort(var) {
	var := time_secToLong(var)
	parts := StrSplit(var, ":")
    if (parts.Count() = 3) {
        hours := parts[1]
		minutes := parts[2]
		seconds := parts[3]
        hours := (hours = "00") ? "" : hours + 0
        minutes := minutes + 0
        seconds := seconds + 0
	    minutes := (minutes < 10) ? "0" minutes : minutes
        seconds := (seconds < 10) ? "0" seconds : seconds
		if (hours = "") {
			if regexmatch(minutes, "imO)0([1-9])", min)
				minutes := min[1]
        }
		if (hours = "" and minutes = "00") {
			minutes := ""
			if regexmatch(seconds, "imO)0([1-9])", sec)
				seconds := sec[1]
        }
        if (hours = "" and minutes = "")
			time := seconds
		else if (hours = "")
            time := minutes ":" seconds
        else
            time := hours ":" minutes ":" seconds
        return time
    }		
}


time_secToAlt(var) {
    hours := Floor(var / 3600)
    rem := var - (hours * 3600)

    minutes := Floor(rem / 60)
    seconds := rem - (minutes * 60)
	time := ""
    if (hours > 0)
        time .= hours "h "

    if (minutes > 0)
        time .= minutes "m "

    if (seconds > 0)
        time .= seconds "s"
    return Trim(time, ", ")
}

time_shortToLong(var) {
    parts := StrSplit(var, ":")
    if (parts.Count() = 3) {
        hours := parts[1]
		if (strlen(hours) = 1)
			hours := "0" . hours
        minutes := parts[2]
        seconds := parts[3]
    } else if (parts.Count() = 2) {
        hours := 00
        minutes := parts[1]
		if (strlen(minutes) = 1)
			minutes := "0" . minutes
        seconds := parts[2]
    } else {
        hours := 00
        minutes := 00
        seconds := parts[1]
		if (strlen(seconds) = 1)
			seconds := "0" . seconds
    }
	return hours ":" minutes ":" seconds
}

time_shortToSec(var) {
    parts := StrSplit(var, ":")
    if (parts.Count() = 3) {
        hours := parts[1]
        mins := parts[2]
        secs := parts[3]
    } else if (parts.Count() = 2) {
        hours := 0
        mins := parts[1]
        secs := parts[2]
    } else {
        hours := 0
        mins := 0
        secs := parts[1]
    }
    totalSeconds := (hours * 3600) + (mins * 60) + secs
    return totalSeconds
}

time_shortToAlt(var) {
	time1 := time_shortToSec(var)
	time := time_secToAlt(time1)
	return time
}

time_longToSec(var) {
	if regexmatch(var, "imO)(\d+)\:(\d+)\:(\d+)", getting_time) {
		hours := getting_time[1]
		minutes := getting_time[2]
		seconds := getting_time[3]
		if (seconds = "00")
			seconds := "0"
		if (minutes = "00")
			minutes := "0"
		if (hours = "00")
			hours := "0"
		hours_to_seconds := hours * 3600
		minutes_to_seconds := minutes * 60
		return hours_to_seconds + minutes_to_seconds + seconds
	}
}

time_longToShort(var) {
	time := time_longToSec(var)
	time := time_secToShort(time)
	return time
}

time_longToAlt(var) {
	if regexmatch(var, "imO)(\d+)\:(\d+)\:(\d+)", getting_time) {
		hours := getting_time[1]
		minutes := getting_time[2]
		seconds := getting_time[3]
		hours := (hours != "00") ? hours + 0 "h" : ""
        minutes := (minutes != "00") ? minutes + 0 "m" : ""
        seconds := (seconds != "00") ? seconds + 0 "s" : ""
        time := ""
        if (hours != "") 
            time .= hours
        if (minutes != "") 
            time .= (time != "" ? " " : "") minutes
        if (seconds != "") 
            time .= (time != "" ? " " : "") seconds
        return time
	}
}
