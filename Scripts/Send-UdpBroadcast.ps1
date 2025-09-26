<#
.SYNOPSIS
  Broadcast a UDP packet on port 35891 across all IPv4 network interfaces.

.DESCRIPTION
  For each up, non-loopback IPv4 interface this script computes the broadcast address
  (IP OR ~netmask) and sends the provided message as a UDP packet to that broadcast address.
  It uses .NET networking classes so it works in Windows PowerShell and PowerShell Core.

.PARAMETER Message
  The text message to send. Default: "Hello from PowerShell UDP broadcast".

.PARAMETER Port
  Destination UDP port. Default: 35891.

.PARAMETER Repeat
  Number of times to send on each interface. Default: 1.

.PARAMETER DelaySeconds
  Delay between repeats in seconds. Default: 0.5

.EXAMPLE
  .\Send-UdpBroadcast.ps1 -Message "Ping from my script" -Port 35891 -Repeat 3 -DelaySeconds 1

#>

param (
    [string]$Message = "Hello from PowerShell UDP broadcast",
    [int]$Port = 35891,
    [int]$Repeat = 1,
    [double]$DelaySeconds = 0.5
)

function IPToUInt32([System.Net.IPAddress]$ip) {
    $b = $ip.GetAddressBytes()
    # bytes are network (big-endian): b[0] is high-order
    return ([uint32]($b[0] -shl 24) -bor [uint32]($b[1] -shl 16) -bor [uint32]($b[2] -shl 8) -bor [uint32]($b[3]))
}

function UInt32ToIPAddress([uint32]$u) {
    $a = ($u -shr 24) -band 0xFF
    $b = ($u -shr 16) -band 0xFF
    $c = ($u -shr 8)  -band 0xFF
    $d = $u -band 0xFF
    return [System.Net.IPAddress]::Parse("$a.$b.$c.$d")
}

# Get all network interfaces
$interfaces = [System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() |
    Where-Object { $_.OperationalStatus -eq 'Up' -and $_.NetworkInterfaceType -ne 'Loopback' }

if (-not $interfaces) {
    Write-Warning "No active non-loopback network interfaces found."
    return
}

# Prepare payload bytes
$payload = [System.Text.Encoding]::UTF8.GetBytes($Message)

foreach ($iface in $interfaces) {
    $props = $iface.GetIPProperties()
    foreach ($unicast in $props.UnicastAddresses) {
        if ($unicast.Address.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork) { continue }

        $localIP = $unicast.Address
        $mask = $unicast.IPv4Mask
        if (-not $mask) {
            # sometimes IPv4Mask may be $null; skip if so
            Write-Verbose "Skipping $($localIP) on $($iface.Name) -- no IPv4 mask available."
            continue
        }

        try {
            $ipUInt  = IPToUInt32 $localIP
            $maskUInt = IPToUInt32 $mask
            # broadcast = (ip & mask) | (~mask & 0xFFFFFFFF)
            $invMask = $maskUInt -bxor 0xFFFFFFFF
            $network = $ipUInt -band $maskUInt
            $bcastUInt = $network -bor $invMask
            $bcastIP = UInt32ToIPAddress $bcastUInt

            Write-Host "Interface: $($iface.Name) - Local: $localIP  Mask: $mask  Broadcast: $bcastIP"

            for ($i = 0; $i -lt $Repeat; $i++) {
                # Create a socket bound to the local interface so the OS uses the correct source
                $udp = New-Object System.Net.Sockets.UdpClient
                try {
                    # Allow broadcast
                    $udp.Client.SetSocketOption([System.Net.Sockets.SocketOptionLevel]::Socket,
                                                [System.Net.Sockets.SocketOptionName]::Broadcast, $true)

                    # Bind to local IP on ephemeral port so outgoing packets use this interface
                    $localEP = New-Object System.Net.IPEndPoint $localIP, 0
                    $udp.Client.Bind($localEP)

                    $destEP = New-Object System.Net.IPEndPoint $bcastIP, $Port
                    $bytesSent = $udp.Send($payload, $payload.Length, $destEP)
                    Write-Host "  Sent $bytesSent bytes to $bcastIP`:$Port"
                } catch {
                    Write-Warning "  Failed sending on interface $($iface.Name) ($localIP): $($_.Exception.Message)"
                } finally {
                    $udp.Close()
                }

                if ($i -lt ($Repeat - 1) -and $DelaySeconds -gt 0) { Start-Sleep -Seconds $DelaySeconds }
            }

        } catch {
            Write-Warning "Error computing broadcast for $($localIP): $($_.Exception.Message)"
        }
    }
}
