@echo off
setlocal enabledelayedexpansion

echo PDF to PNG Converter Setup
echo -------------------------
echo.

REM Create a log file to track progress
echo Starting batch script execution > converter_log.txt

REM Get the current directory where the batch file is located
set CURRENT_DIR=%~dp0

echo Current directory: %CURRENT_DIR% >> converter_log.txt

REM Create tmp directory if it doesn't exist
set TMP_DIR=%CURRENT_DIR%tmp
if not exist "%TMP_DIR%" (
    echo Creating tmp directory...
    mkdir "%TMP_DIR%"
    echo Created tmp directory: %TMP_DIR% >> converter_log.txt
)

echo Created tmp directory >> converter_log.txt

REM Create .gitignore file in tmp directory
echo PdfToPngConverter/> "%TMP_DIR%\.gitignore"
echo PdfToPngConverter/bin/>> "%TMP_DIR%\.gitignore"
echo PdfToPngConverter/obj/>> "%TMP_DIR%\.gitignore"

echo Created .gitignore file >> converter_log.txt

REM Navigate to tmp directory
cd /d "%TMP_DIR%"

echo Changed to tmp directory >> converter_log.txt

REM Create or update the PdfToPngConverter project
if not exist PdfToPngConverter (
    echo Creating new .NET project...
    mkdir PdfToPngConverter
    cd PdfToPngConverter
    echo Created PdfToPngConverter directory >> "%CURRENT_DIR%converter_log.txt"
    
    dotnet new console
    if %ERRORLEVEL% NEQ 0 (
        echo Failed to create new console project: %ERRORLEVEL% >> "%CURRENT_DIR%converter_log.txt"
        echo ERROR: Failed to create new console project.
        goto error_exit
    )
    
    echo Created new console project >> "%CURRENT_DIR%converter_log.txt"
) else (
    echo Updating existing project...
    cd PdfToPngConverter
    echo Changed to PdfToPngConverter directory >> "%CURRENT_DIR%converter_log.txt"
    
    REM Clean up previous build artifacts
    if exist bin (
        rmdir /s /q bin
    )
    if exist obj (
        rmdir /s /q obj
    )
    echo Cleaned previous build artifacts >> "%CURRENT_DIR%converter_log.txt"
)

echo Installing required packages...
echo Installing packages... >> "%CURRENT_DIR%converter_log.txt"

REM Install packages one by one with error checking
dotnet add package PdfiumViewer
if %ERRORLEVEL% NEQ 0 (
    echo Failed to add PdfiumViewer package: %ERRORLEVEL% >> "%CURRENT_DIR%converter_log.txt"
    echo ERROR: Failed to add PdfiumViewer package.
    goto error_exit
)

echo Added PdfiumViewer package >> "%CURRENT_DIR%converter_log.txt"

dotnet add package PdfiumViewer.Native.x86_64.v8-xfa
if %ERRORLEVEL% NEQ 0 (
    echo Failed to add PdfiumViewer.Native package: %ERRORLEVEL% >> "%CURRENT_DIR%converter_log.txt"
    echo ERROR: Failed to add PdfiumViewer.Native package.
    goto error_exit
)

echo Added PdfiumViewer.Native package >> "%CURRENT_DIR%converter_log.txt"

dotnet add package System.Drawing.Common
if %ERRORLEVEL% NEQ 0 (
    echo Failed to add System.Drawing.Common package: %ERRORLEVEL% >> "%CURRENT_DIR%converter_log.txt"
    echo ERROR: Failed to add System.Drawing.Common package.
    goto error_exit
)

echo Added System.Drawing.Common package >> "%CURRENT_DIR%converter_log.txt"

dotnet add package Magick.NET-Q16-AnyCPU --version 14.6.0
if %ERRORLEVEL% NEQ 0 (
    echo Failed to add Magick.NET package: %ERRORLEVEL% >> "%CURRENT_DIR%converter_log.txt"
    echo ERROR: Failed to add Magick.NET package.
    goto error_exit
)

echo Added Magick.NET package >> "%CURRENT_DIR%converter_log.txt"

echo Copying program code...
copy /Y "%CURRENT_DIR%pdf-to-png-converter.cs" Program.cs
if %ERRORLEVEL% NEQ 0 (
    echo Failed to copy Program.cs: %ERRORLEVEL% >> "%CURRENT_DIR%converter_log.txt"
    echo ERROR: Failed to copy program code. Make sure pdf-to-png-converter.cs exists.
    goto error_exit
)

echo Copied program code >> "%CURRENT_DIR%converter_log.txt"

echo Building PDF to PNG Converter...
dotnet build -c Release
if %ERRORLEVEL% NEQ 0 (
    echo Build failed: %ERRORLEVEL% >> "%CURRENT_DIR%converter_log.txt"
    echo ERROR: Build failed.
    goto error_exit
)

echo Build completed successfully >> "%CURRENT_DIR%converter_log.txt"

echo.
echo Running PDF to PNG Converter...
echo.

echo Starting program execution >> "%CURRENT_DIR%converter_log.txt"

REM Run the application in a new console window to keep it open
start cmd /k "cd /d %CD% && dotnet run -c Release"

echo Program started in a new window >> "%CURRENT_DIR%converter_log.txt"
goto normal_exit

:error_exit
echo An error occurred during setup. Please check the log file: converter_log.txt
echo Error occurred >> "%CURRENT_DIR%converter_log.txt"

:normal_exit
echo.
echo Setup complete! The converter is running in a separate window.
echo If you don't see the converter window, check the log file: converter_log.txt
echo.
echo Press any key to exit this setup window...
pause > nul