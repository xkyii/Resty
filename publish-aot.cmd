@echo off
call "D:\Scoop\apps\vsbuildtools2022\17.14.29\vs\Common7\Tools\VsDevCmd.bat" -arch=x64 -host_arch=x64 >nul 2>&1
dotnet publish d:\Code\xkyii\Resty\src\Resty.Cli\Resty.Cli.csproj /p:PublishProfile=win-x64-aot /p:IlcUseEnvironmentalTools=true %*
dotnet publish d:\Code\xkyii\Resty\src\Resty.Gui\Resty.Gui.csproj /p:PublishProfile=win-x64-aot /p:IlcUseEnvironmentalTools=true %*
