# Available Options:
# WIN95
# WIN98
# WINXPSP2
# WINXPSP3
# VISTARTM
# VISTASP1
# VISTASP2
# WIN7RTM
# WIN8RTM
# See: https://ss64.com/nt/syntax-compatibility.html
New-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers" -Name "$InstallDirectory\<Executable>" -Value "~ WINXPSP2 HIGHDPIAWARE" -Force