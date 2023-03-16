$User = New-Object System.Security.Principal.NTAccount($env:UserName)
$SID = $User.Translate([System.Security.Principal.SecurityIdentifier]).value
# The OS might force your game to read/write to the user's virtual store. This will write a key to the correct SID's virtual store.
New-ItemProperty -Path "HKU:\$SID\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\<Path>" -Name "KeyName" -Value "New Value" -Force