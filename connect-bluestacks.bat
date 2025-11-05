@echo off
cd /d C:\Users\jose9\AppData\Local\Android\Sdk\platform-tools
adb connect 127.0.0.1:5555
adb devices
echo.
echo BlueStacks connected! Check Android Studio device dropdown.
pause
