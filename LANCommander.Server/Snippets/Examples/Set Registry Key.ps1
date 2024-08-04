# Creates or updates a key in the registry
New-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\<Path>" -Name "KeyName" -Value "New Value" -Force