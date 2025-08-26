; ======================================================================================================================
; MPC-BE Video Editor - Timers and Core Logic
; Description: This script initializes the GUI and manages the core monitoring loop for MPC-BE.
; It uses a single, efficient timer to prevent race conditions and handle all state changes.
; ======================================================================================================================

global last_known_video_path := "" ; Use a global variable to track the video state.
global g_manual_load_in_progress := 0 ; A flag to prevent the timer from interfering with manual loads.

; --- Call the main initialization routine on script startup.
Initialize()
return

; ======================================================================================================================
;  INITIALIZATION ROUTINE
;  - Runs once when the script is first launched.
; ======================================================================================================================
Initialize() {
    global ; Ensure all variables are accessible globally

    ; --- Load the basic GUI and menu immediately on a fresh file load.
    menu_show2("test") ; Build the menu structure
    show_gui2()   
    update_menus2()    ; Disable/Enable menu items based on current state

    ; --- If MPC-BE is running on startup, check for a video and update accordingly.
    if window_exist2("ahk_class MPC-BE") {
        current_video_path := mpc_getSource2()

        ; Check if the retrieved path is a valid video file and not just the default MPC-BE title.
        if (current_video_path && !InStr(current_video_path, "MPC-BE")) {
            file_path := current_video_path
            last_known_video_path := current_video_path ; Synchronize the tracker with the initial state.

            ; Now, check if a corresponding CSV file exists.
            associated_csv_path := csv_filepathToCSV2(file_path)
            if FileExist(associated_csv_path) {
                file_path_csv := associated_csv_path
				timer := "on"
                show_gui2() ; Display the bookmarks from the found CSV
				timer := "off"
            }
            update_menus2() ; Update menus to reflect the loaded video/CSV
        }
    }

    ; --- Start the single monitoring timer that will handle all future updates.
    SetTimer, MonitorMPC, 500 ; Check for changes every 500ms
    return
}

; ======================================================================================================================
;  SINGLE MONITORING TIMER
;  - This one timer handles detecting new videos, closed videos, and keeps the state synchronized.
; ======================================================================================================================
MonitorMPC:
    ; --- Check if MPC-BE is running.
    if window_exist2("ahk_class MPC-BE") {
        current_video_path := mpc_getSource2()

        ; Guard against invalid paths or the default "MPC-BE" title.
        if (!current_video_path || InStr(current_video_path, "MPC-BE")) {
            return ; Not a real video file, do nothing this cycle.
        }

        ; --- DETECT A NEW OR CHANGED VIDEO ---
        if (current_video_path != last_known_video_path) {
            
            ; If a manual load is in progress, the timer's only job is to update its
            ; internal tracker, reset the flag, and then stop. It will NOT call show_gui2().
            if (g_manual_load_in_progress = 1) {
                last_known_video_path := current_video_path
                g_manual_load_in_progress := 0 ; Reset the flag
                return ; Exit the subroutine to prevent a second GUI refresh
            }

            ; --- This block now only runs for AUTOMATICALLY detected video changes ---
            file_path := current_video_path
            last_known_video_path := current_video_path ; Update our tracker

            associated_csv_path := csv_filepathToCSV2(file_path)
            if FileExist(associated_csv_path) {
                file_path_csv := associated_csv_path
               	timer := "on"
                show_gui2() ; Display the bookmarks from the found CSV
				timer := "off"
            } else {
                file_path_csv := ""
           ;     clear_gui2()
            }
            update_menus2()
        }

    } else {
        ; --- MPC-BE IS NOT RUNNING ---
        if (last_known_video_path != "") {
            file_path := ""

            last_known_video_path := "" ; Reset the tracker
            update_menus2()
			reload
        }
    }
return
