@echo off
setlocal
if exist "%~dp0Build\StoryAssets\KidoTraining.exe" (
    start "" "%~dp0Build\StoryAssets\KidoTraining.exe" -screen-width 1600 -screen-height 900 -screen-fullscreen 0
) else (
    echo Windows player not found in Build\StoryAssets.
    echo Open this project in Unity 2022.3 and press Play to enter Story Mode.
    pause
)
