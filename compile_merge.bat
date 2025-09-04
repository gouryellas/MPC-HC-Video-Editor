@echo off
setlocal enabledelayedexpansion
:: Arguments:
:: %1: Input Video File (quoted)
:: %2: Output Video File (quoted) - will be forced to .mp4 extension
:: %3: Start Time (HH:MM:SS)
:: %4: End Time (HH:MM:SS) - Optional, for single segments
set "INPUT_FILE=%~1"
set "OUTPUT_FILE=%~2"
set "START_TIME=%~3"
set "END_TIME=%~4"
set "FFMPEG_PATH=%TEMP%\ffmpeg.exe"
set "FFPROBE_PATH=%TEMP%\ffprobe.exe"
echo Input file: %INPUT_FILE%
echo Output file: %OUTPUT_FILE% (will be saved as .mp4)
echo FFmpeg path: %FFMPEG_PATH%
echo FFprobe path: %FFPROBE_PATH%
if not exist "%FFMPEG_PATH%" (
    echo FFmpeg not found at %FFMPEG_PATH%.
    pause
    exit /b 1
)
:: Check if ffprobe exists (optional, skip codec check if missing)
if not exist "%FFPROBE_PATH%" (
    echo ffprobe not found at %FFPROBE_PATH%.
)
:: Check arguments
if "%~4"=="" (
echo Usage: %0 input_file output_file start1 end1 [start2 end2 ...]
exit /b 1
)
for %%I in ("%~1") do set "input_dir=%%~dpI"
for %%O in ("%~2") do set "output_dir=%%~dpO"
:: Save original input and output file names
set "orig_input=%~1"
set "orig_output=%~2"
set "input=%output_dir%temp.mp4"
:: Determine the directory for the output file
for %%F in ("%orig_output%") do set "output_dir=%%~dpF"
if not exist "%output_dir%" (
echo Output directory "%output_dir%" does not exist.
exit /b 1
)
:: Start with the original input file
set "input=%orig_input%"
:: Force output to .mp4 extension (strip any existing extension and add .mp4)
for %%O in ("%orig_output%") do set "output=%%~dpnO.mp4"
:: Quick check for input audio codec to warn about potential issues
"%FFPROBE_PATH%" -v error -select_streams a:0 -show_entries stream=codec_name -of default=noprint_wrappers=1:nokey=1 "%input%" > "%output_dir%codec.txt"
set /p audio_codec=<"%output_dir%codec.txt"
del "%output_dir%codec.txt"
if "%audio_codec%"=="wmav2" (
echo Note: Input uses WMA v2 audio; re-encoding to AAC for MP4 compatibility.
)
:: Remove the first two arguments (input & output)
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
echo Processing segment !n! from %start% to %end%
"%FFMPEG_PATH%" -ss %start% -to %end% -i "%input%" -c:v libx264 -preset veryfast -c:a aac -b:a 192k -y "%output_dir%segment!n!.mp4"
if errorlevel 1 (
echo Trimming segment !n! failed.
pause
exit /b 1
)
echo file '%output_dir%segment!n!.mp4' >> "%concatfile%"
shift
shift
goto loop
:concatenate
echo Concatenating segments...
"%FFMPEG_PATH%" -y -f concat -safe 0 -i "%concatfile%" -c copy "%output%"
if errorlevel 1 (
echo Concatenation failed.
pause
exit /b 1
)
echo Cleaning up temporary files...
for /l %%i in (1,1,%n%) do del "%output_dir%segment%%i.mp4"
del "%concatfile%"
:: Remove the temporary converted file if it exists
if exist "%output_dir%temp.mp4" del "%output_dir%temp.mp4"
echo Done! Output file: %output%
pause