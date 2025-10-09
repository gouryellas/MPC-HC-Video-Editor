@echo off
setlocal enabledelayedexpansion
echo Starting video conversion script at %date% %time%
:: Arguments:
:: %1: Full path to input video file (quoted), e.g., "c:\users\chris\desktop\video.avi"
:: Output will be automatically generated with the same name and directory, but .mp4 extension
set "INPUT_FILE=%~1"
set "FFMPEG_PATH=%APPDATA%\MPC-HC Video Editor\ffmpeg.exe"
set "FFPROBE_PATH=%APPDATA%\MPC-HC Video Editor\ffprobe.exe"
echo Debug: Input file: %INPUT_FILE%
echo Debug: FFmpeg path: %FFMPEG_PATH%
echo Debug: FFprobe path: %FFPROBE_PATH%
:: Check if ffmpeg exists
if not exist "%FFMPEG_PATH%" (
    echo ERROR: FFmpeg not found at %FFMPEG_PATH%. Download from https://ffmpeg.org/download.html and place in %APPDATA%\MPC-HC Video Editor\.
    pause
    exit /b 1
)
:: Check if ffprobe exists (optional)
if not exist "%FFPROBE_PATH%" (
    echo WARNING: ffprobe not found at %FFPROBE_PATH%. File validation will be limited.
)
:: Check arguments
if "%~1"=="" (
    echo ERROR: No input file provided.
    echo Usage: %0 "input_file_path"
    echo Example: %0 "c:\users\chris\desktop\video.avi"
    pause
    exit /b 1
)
if "%INPUT_FILE%"=="" (
    echo ERROR: Input file path is empty or invalid.
    pause
    exit /b 1
)
:: Derive output file path by changing extension to .mp4
for %%F in ("%INPUT_FILE%") do set "OUTPUT_FILE=%%~dpnF.mp4"
echo Debug: Derived output file: %OUTPUT_FILE%
:: Validate input file existence
if not exist "%INPUT_FILE%" (
    echo ERROR: Input file "%INPUT_FILE%" does not exist.
    pause
    exit /b 1
)
:: Determine output directory and ensure it exists
for %%O in ("%OUTPUT_FILE%") do set "output_dir=%%~dpO"
if not exist "%output_dir%" (
    echo ERROR: Output directory "%output_dir%" does not exist. Please create it or specify a valid input path.
    pause
    exit /b 1
)
:: Warn if output already exists
if exist "%OUTPUT_FILE%" (
    echo WARNING: Output file "%OUTPUT_FILE%" already exists. It will be overwritten.
)
echo This script will convert the input video "%INPUT_FILE%" to MP4 format at "%OUTPUT_FILE%".
echo.
set "converted=0"
set "failed=0"
echo Processing: %INPUT_FILE% -^> %OUTPUT_FILE%
"%FFMPEG_PATH%" -fflags +igndts -i "%INPUT_FILE%" -c:v libx264 -preset veryfast -c:a aac -b:a 192k -y "%OUTPUT_FILE%"
if errorlevel 1 (
    echo ERROR: Conversion failed. Check FFmpeg output above for details.
    set "failed=1"
) else (
    echo Successfully converted: %INPUT_FILE% -^> %OUTPUT_FILE%
    set "converted=1"
)
echo.
echo Conversion summary:
echo Converted: %converted%
echo Failed: %failed%
echo Script completed at %date% %time%.
pause