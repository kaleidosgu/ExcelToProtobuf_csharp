cd E:\WorkSpace\SourceCode\Tools\ExcelToProtobuf_csharp
$secureKey = Read-Host "Publisher Key" -AsSecureString
$env:STEAM_REPORT_PUBLISHER_KEY = [System.Net.NetworkCredential]::new("", $secureKey).Password
dotnet run --project .\src\SteamReportSystem\SteamReportSystem.csproj

http://127.0.0.1:5079

2D7EF7AF940DAE40BDD8544A3C169091