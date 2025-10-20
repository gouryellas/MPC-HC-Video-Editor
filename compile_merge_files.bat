@echo off
setlocal enabledelayedexpansion
echo Starting script at %date% %time%

:: Arguments:
:: %1: Output Video File (quoted) - will be forced to .mp4 extension
:: %2+: Input Video Files (quoted)
set "OUTPUT_FILE=%~1"
set "FFMPEG_PATH=%APPDATA%\MPC-HC Video Editor\ffmpeg.exe"
set "FFPROBE_PATH=%APPDATA%\MPC-HC Video Editor\ffprobe.exe"

echo Debug: Parsed Output file: %OUTPUT_FILE%
echo Debug: FFmpeg path: %FFMPEG_PATH%
echo Debug: FFprobe path: %FFPROBE_PATH%

:: Check if ffmpeg exists
if not exist "%FFMPEG_PATH%" (
    echo ERROR: FFmpeg not found at %FFMPEG_PATH%. Download from https://ffmpeg.org/download.html and place in %APPDATA%\MPC-HC Video Editor\.
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
    echo Usage: %0 "output_file" "input_file1" ["input_file2" ...]
    pause
    exit /b 1
)
if "%OUTPUT_FILE%"=="" (
    echo ERROR: Output file not specified.
    pause
    exit /b 1
)
if "%~2"=="" (
    echo ERROR: No input files provided.
    pause
    exit /b 1
)

:: Determine output directory
for %%O in ("%OUTPUT_FILE%") do set "output_dir=%%~dpO"

:: Ensure output directory exists
if not exist "%output_dir%" (
    echo ERROR: Output directory "%output_dir%" does not exist.
    pause
    exit /b 1
)

:: Save original output file name
set "orig_output=%OUTPUT_FILE%"

:: Force output to .mp4 extension
for %%O in ("%orig_output%") do set "output=%%~dpnO.mp4"
echo Debug: Final output path: %output%

:: Shift to process input files (skip output arg)
shift

set "n=0"
set "concatfile=%output_dir%concat.txt"
if exist "%concatfile%" del "%concatfile%"

:loop
if "%~1"=="" goto concatenate
set /a "n+=1"
set "input=%~1"
if "!input!"=="" (
    echo ERROR: Input file for position !n! is missing.
    pause
    exit /b 1
)

:: Validate input file existence
if not exist "!input!" (
    echo ERROR: Input file "!input!" does not exist.
    pause
    exit /b 1
)

echo Processing input file !n!: !input!
"%FFMPEG_PATH%" -fflags +igndts -i "!input!" -c:v libx264 -preset veryfast -c:a aac -b:a 192k -y "%output_dir%segment!n!.mp4"
if errorlevel 1 (
    echo ERROR: Processing input file !n! failed. Check FFmpeg output above for details.
    pause
    exit /b 1
)
echo file '%output_dir%segment!n!.mp4' >> "%concatfile%"
shift
goto loop

:concatenate
if !n! equ 0 (
    echo ERROR: No input files specified.
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
:: Run ffmpeg and capture the last line that contains "time="
