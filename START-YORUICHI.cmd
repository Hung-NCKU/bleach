@echo off
setlocal
if exist "%~dp0Build\StoryAssets\KidoTraining.exe" (
    start "" "%~dp0Build\StoryAssets\KidoTraining.exe" -kido-yoruichi -screen-width 1600 -screen-height 900 -screen-fullscreen 0
) else (
    echo Run Tools / Kido / Prepare downloaded character and build the Windows player first.
    pause
)
