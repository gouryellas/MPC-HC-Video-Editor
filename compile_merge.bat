@echo off
setlocal enabledelayedexpansion
echo Starting script at %date% %time%

:: Arguments:
:: %1: Input Video File (quoted)
:: %2: Output Video File (quoted) - will be forced to .mp4 extension
:: %3+: Start/End times (HH:MM:SS) pairs for segments
set "INPUT_FILE=%~1"
set "OUTPUT_FILE=%~2"
set "FFMPEG_PATH=%APPDATA%\MPC-HC Video Editor\ffmpeg.exe"
set "FFPROBE_PATH=%APPDATA%\MPC-HC Video Editor\ffprobe.exe"

echo Debug: Parsed Input file: %INPUT_FILE%
echo Debug: Parsed Output file: %OUTPUT_FILE%
echo Debug: FFmpeg path: %FFMPEG_PATH%
echo Debug: FFprobe path: %FFPROBE_PATH%

:: Check if ffmpeg exists
if not exist "%FFMPEG_PATH%" (
    echo ERROR: FFmpeg not found at %FFMPEG_PATH%. Download from https://ffmpeg.org/download.html and place in %TEMP%.
    pause
    exit /b 1
)

:: Check if ffprobe exists
if not exist "%FFPROBE_PATH%" (
    echo WARNING: ffprobe not found at %FFPROBE_PATH%. Duration calculation and clipboard copy will be skipped. Download from https://ffmpeg.org/download.html.
)

:: Check arguments
if "%~1"=="" (
    echo ERROR: No arguments provided.
    echo Usage: %0 "input_file" "output_file" start1 end1 [start2 end2 ...]
    pause
    exit /b 1
)
if "%INPUT_FILE%"=="" (
    echo ERROR: Input file not specified.
    pause
    exit /b 1
)
if "%OUTPUT_FILE%"=="" (
    echo ERROR: Output file not specified.
    pause
    exit /b 1
)
if "%~3"=="" (
    echo ERROR: No start time provided.
    pause
    exit /b 1
)
if "%~4"=="" (
    echo ERROR: No end time provided for first segment.
    pause
    exit /b 1
)

:: Validate input file existence
if not exist "%INPUT_FILE%" (
    echo ERROR: Input file "%INPUT_FILE%" does not exist.
    pause
    exit /b 1
)

:: Determine directories
for %%I in ("%INPUT_FILE%") do set "input_dir=%%~dpI"
for %%O in ("%OUTPUT_FILE%") do set "output_dir=%%~dpO"

:: Ensure output directory exists
if not exist "%output_dir%" (
    echo ERROR: Output directory "%output_dir%" does not exist.
    pause
    exit /b 1
)

:: Save original input and output file names
set "orig_input=%INPUT_FILE%"
set "orig_output=%OUTPUT_FILE%"

:: Force output to .mp4 extension
for %%O in ("%orig_output%") do set "output=%%~dpnO.mp4"
echo Debug: Final output path: %output%

:: Check input audio codec
if exist "%FFPROBE_PATH%" (
    "%FFPROBE_PATH%" -v error -select_streams a:0 -show_entries stream=codec_name -of default=noprint_wrappers=1:nokey=1 "%orig_input%" > "%output_dir%codec.txt"
    if errorlevel 1 (
        echo WARNING: Failed to check audio codec.
    ) else (
        set /p audio_codec=<"%output_dir%codec.txt"
        if "!audio_codec!"=="wmav2" (
            echo Note: Input uses WMA v2 audio; re-encoding to AAC for MP4 compatibility.
        )
    )
    if exist "%output_dir%codec.txt" del "%output_dir%codec.txt"
)

:: Shift to process segment times (skip input/output args)
shift
shift

set "n=0"
set "concatfile=%output_dir%concat.txt"
if exist "%concatfile%" del "%concatfile%"

:loop
if "%~1"=="" goto concatenate
set /a "n+=1"
set "start=%~1"
set "end=%~2"
if "!start!"=="" (
    echo ERROR: Start time for segment !n! is missing.
    pause
    exit /b 1
)
if "!end!"=="" (
    echo ERROR: End time for segment !n! is missing.
    pause
    exit /b 1
)
echo Processing segment !n! from !start! to !end!
"%FFMPEG_PATH%" -fflags +igndts -ss !start! -to !end! -i "%orig_input%" -c:v libx264 -preset veryfast -c:a aac -b:a 192k -y "%output_dir%segment!n!.mp4"
if errorlevel 1 (
    echo ERROR: Trimming segment !n! failed. Check FFmpeg output above for details.
    pause
    exit /b 1
)
echo file '%output_dir%segment!n!.mp4' >> "%concatfile%"
shift
shift
goto loop

:concatenate
if !n! equ 0 (
    echo ERROR: No segments specified.
    pause
    exit /b 1
)
echo Concatenating segments...
"%FFMPEG_PATH%" -y -f concat -safe 0 -i "%concatfile%" -c copy "%output%"
if errorlevel 1 (
    echo ERROR: Concatenation failed. Check concat.txt and segments for issues.
    pause
    exit /b 1
)

echo Cleaning up temporary files...
for /l %%i in (1,1,!n!) do if exist "%output_dir%segment%%i.mp4" del "%output_dir%segment%%i.mp4"
if exist "%concatfile%" del "%concatfile%"
if exist "%output_dir%temp.mp4" del "%output_dir%temp.mp4"

echo Done! Output file: %output%
:: run ffmpeg and capture the last line that contains "time="
for /f "delims=" %%L in ('ffmpeg -i "%output%" -f null - 2^>^&1 ^| findstr /i "time="') do set "ffline=%%L"
if not defined ffline (echo No ffmpeg time output found & exit /b 1)
:: remove everything up through "time=" so the remainder starts with the timestamp
set "rest=!ffline:*time=!"
:: take the first space-delimited token from the remainder (that's the timestamp)
for /f "tokens=1 delims= " %%T in ("!rest!") do set "timevalue=%%T"
:: copy timestamp to clipboard and show confirmation
echo %timevalue% | clip
echo Copied to clipboard: %timevalue%

pause