#Requires -Version 5.1
<#
.SYNOPSIS
  Despliega un relay Lumen en CUALQUIER servidor Linux con IP publica.
  Solo necesitas una maquina barata (por ejemplo 3-5 USD/mes) o gratuita.
  El script te pide la IP, usuario y contrasena/SSH key, y lo instala todo.

  Mas facil aun si tienes Oracle Cloud Free Tier: usa Deploy-OracleRelay.ps1.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$HostIp,

    [Parameter(Mandatory = $true)]
    [string]$User = "ubuntu",

    [Parameter(Mandatory = $false)]
    [string]$Password = "",

    [Parameter(Mandatory = $false)]
    [string]$SshKeyFile = "$env:USERPROFILE\.ssh\id_rsa",

    [int]$Port = 47991
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

if (-not (Get-Command ssh -ErrorAction SilentlyContinue)) {
    throw "Necesito SSH. Instala Git para Windows (https://git-scm.com) o OpenSSH."
}

$secret = -join ((1..32) | ForEach-Object { "{0:X}" -f (Get-Random -Maximum 16) })

$sshArgs = @("-o", "StrictHostKeyChecking=no", "-o", "UserKnownHostsFile=/dev/null")
if (Test-Path $SshKeyFile) {
    $sshArgs += @("-i", $SshKeyFile)
}
if ($Password) {
    $sshArgs += @("-o", "PreferredAuthentications=password")
}

$remote = "$User@$HostIp"
$script = @"
set -e
export DEBIAN_FRONTEND=noninteractive
sudo apt-get update
sudo apt-get install -y curl dotnet-sdk-8.0 git || sudo apt-get install -y curl git

# If dotnet is not available from apt, install from Microsoft script
if ! command -v dotnet &> /dev/null; then
    curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0
    export PATH=\"\$HOME/.dotnet:\$PATH\"
fi

mkdir -p /opt/lumen
cd /opt/lumen
if [ ! -d src ]; then
    git clone https://github.com/MartinCajiao/lumen-stream.git src
fi
cd src
git pull
dotnet publish src/Lumen.Relay -c Release -r linux-x64 --self-contained -o /opt/lumen/relay

cat > /tmp/lumen-secret.txt <<EOF
$secret
EOF
sudo mv /tmp/lumen-secret.txt /opt/lumen/relay/secret.txt

cat > /tmp/lumen-relay.service <<EOF
[Unit]
Description=Lumen Relay
After=network.target

[Service]
ExecStart=/opt/lumen/relay/lumen-relay $Port \"$(cat /opt/lumen/relay/secret.txt)\"
Restart=always
WorkingDirectory=/opt/lumen/relay

[Install]
WantedBy=multi-user.target
EOF
sudo mv /tmp/lumen-relay.service /etc/systemd/system/lumen-relay.service
sudo systemctl daemon-reload
sudo systemctl enable lumen-relay
sudo systemctl restart lumen-relay

echo \"RELAY_OK\"
"@

Write-Step "Conectando a $remote e instalando relay..."
if ($Password) {
    $secure = ConvertTo-SecureString $Password -AsPlainText -Force
    $cred = New-Object System.Management.Automation.PSCredential($User, $secure)
    # PowerShell 5.1 doesn't have ssh - use ssh directly with echo pipe for password
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($script))
    ssh $sshArgs $remote "echo $encoded | base64 -d | bash"
} else {
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($script))
    ssh $sshArgs $remote "echo $encoded | base64 -d | bash"
}

$settings = "$env:APPDATA\LumenStream\lumen.json"
$cfg = @{}
if (Test-Path $settings) {
    $cfg = Get-Content $settings -Raw | ConvertFrom-Json
}
if (-not $cfg.wan) { $cfg.wan = @{} }
$cfg.wan.relayServer = "$HostIp`:$Port`:$secret"
$cfg | ConvertTo-Json -Depth 5 | Set-Content $settings

Write-Host "`n================================" -ForegroundColor Green
Write-Host "RELAY LISTO" -ForegroundColor Green
Write-Host "Configuracion guardada: $HostIp`:$Port`:$secret" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host "Abre Lumen y pulsa Compartir. El codigo saldra como relay:..."
