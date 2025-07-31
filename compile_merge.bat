@echo off
setlocal enabledelayedexpansion

:: Arguments:
:: %1: Input Video File (quoted)
:: %2: Output Video File (quoted)
:: %3: Start Time (HH:MM:SS)
:: %4: End Time (HH:MM:SS) - Optional, for single segments

set "INPUT_FILE=%~1"
set "OUTPUT_FILE=%~2"
set "START_TIME=%~3"
set "END_TIME=%~4"

echo Input file: %INPUT_FILE%
echo Output file: %OUTPUT_FILE%

:: Check if at least 4 arguments are passed (input, output, and at least one pair of start-end timestamps)
if "%~4"=="" (
    echo Usage: %0 input_file output_file start1 end1 [start2 end2 ...]
    exit /b 1
)

for %%I in ("%~1") do set "input_dir=%%~dpI"
for %%O in ("%~2") do set "output_dir=%%~dpO"

:: Save original input and output file names
set "orig_input=%~1"
set "orig_output=%~2"

:: Determine the directory for the output file
for %%F in ("%orig_output%") do set "output_dir=%%~dpF"
if not exist "%output_dir%" (
    echo Output directory "%output_dir%" does not exist.
    exit /b 1
)

:: Start with the original input file
set "input=%orig_input%"
set "output=%orig_output%"

:: Check the input file extension and convert if needed
set "ext=%~x1"
if /I "%ext%"==".flv" goto convert
if /I "%ext%"==".mpeg" goto convert
if /I "%ext%"==".wmv" goto convert
if /I "%ext%"==".avi" goto convert
goto after_convert

:convert
echo Converting %input% to MP4 format for compatibility...
"C:\Program Files\DownloadHelper CoApp\ffmpeg.exe" -i "%input%" -c:v libx264 -c:a aac -strict experimental -y "%output_dir%temp.mp4"
if errorlevel 1 (
    echo Conversion failed.
    exit /b 1
)
:: Use the converted file as the new input
set "input=%output_dir%temp.mp4"
goto after_convert

:after_convert
:: Force the final output filename to use .mp4 extension.
for %%F in ("%output%") do set "base=%%~nF"
set "output=%output_dir%%base%.mp4"

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
echo Using input: %input%
echo Output directory: %output_dir%
echo Concat file: %concatfile%

echo Trimming segment !n! from %start% to %end%
"C:\Program Files\DownloadHelper CoApp\ffmpeg.exe" -ss %start% -to %end% -i "%input%" -acodec copy -vcodec copy -async 1 -y "%output_dir%segment!n!.mp4"
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
"C:\Program Files\DownloadHelper CoApp\ffmpeg.exe" -y -f concat -safe 0 -i "%concatfile%" -c copy "%output%"
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
