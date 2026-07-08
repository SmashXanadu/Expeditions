Dim oShell, sDir
Set oShell = CreateObject("WScript.Shell")
sDir = Left(WScript.ScriptFullName, InStrRev(WScript.ScriptFullName, "\") - 1)

' Close any running instance
oShell.Run "taskkill /FI ""WINDOWTITLE eq Expeditions PDF Converter"" /F", 0, True
WScript.Sleep 300

' Launch with hidden console window (0 = hidden, False = don't wait)
oShell.Run "dotnet run --project """ & sDir & "\Tools\Tools2.csproj""", 0, False
