@echo off
REM 拷贝 HiProtobuf.UI 的 exe, pdb
xcopy "src\HiProtobuf.UI\obj\Release\*.exe" "d:\GameProjects\GunCross\Tools\ExcelToProtobuf\HiProtobuf\" /Y
xcopy "src\HiProtobuf.UI\obj\Release\*.pdb" "d:\GameProjects\GunCross\Tools\ExcelToProtobuf\HiProtobuf\" /Y

REM 拷贝 HiProtobuf.Lib 的 dll, pdb
xcopy "src\HiProtobuf.Lib\obj\Release\*.dll" "d:\GameProjects\GunCross\Tools\ExcelToProtobuf\HiProtobuf\" /Y
xcopy "src\HiProtobuf.Lib\obj\Release\*.pdb" "d:\GameProjects\GunCross\Tools\ExcelToProtobuf\HiProtobuf\" /Y

echo 拷贝完成。
pause 