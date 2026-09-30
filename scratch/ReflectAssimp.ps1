$asm = [System.Reflection.Assembly]::LoadFrom('C:\Users\wesll\.nuget\packages\assimp.maui\6.0.5-rc4\lib\net10.0-windows10.0.19041\Assimp.Maui.dll')
$asm.GetTypes() | Where-Object { $_.IsPublic } | Select-Object -Property FullName
