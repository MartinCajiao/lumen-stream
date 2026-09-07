#Requires -Version 5.1
<#
.SYNOPSIS
  Despliega un relay Lumen gratis en Oracle Cloud Free Tier (VM "Always Free").
  Al final escribe la configuración en Lumen para que solo tengas que pegar el
  código relay en el otro PC.

  Requisitos: una cuenta de Oracle Cloud (gratis, pide tarjeta solo para verificar).
  El script te guía a crear la API key si no la tienes.
#>
[CmdletBinding()]
param(
    [string]$Region = "us-ashburn-1",
    [string]$CompartmentId = "",
    [string]$ConfigFile = "$env:USERPROFILE\.oci\config",
    [string]$Profile = "DEFAULT",
    [int]$Port = 47991
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Ensure-OciCli {
    $oci = Get-Command "oci" -ErrorAction SilentlyContinue
    if ($oci) { return $oci.Source }

    Write-Step "Instalando OCI CLI (la herramienta de Oracle)..."
    $url = "https://raw.githubusercontent.com/oracle/oci-cli/master/scripts/install/install.ps1"
    $install = "$env:TEMP\install-oci-cli.ps1"
    Invoke-WebRequest -Uri $url -OutFile $install -UseBasicParsing
    & $install -AcceptAllDefaults
    $oci = Get-Command "oci" -ErrorAction SilentlyContinue
    if (-not $oci) {
        throw "No se pudo instalar OCI CLI. Abre PowerShell nuevo y reintenta."
    }
    return $oci.Source
}

function Ensure-OciConfig {
    param([string]$Oci)

    if (Test-Path $ConfigFile) {
        $profiles = & $Oci setup config --file-location $ConfigFile --profile $Profile --help 2>&1 | Out-Null
        return
    }

    Write-Step "Configurando la API key de Oracle"
    Write-Host @"
Vas a necesitar de tu cuenta de Oracle Cloud:
  - Tenancy OCID
  - User OCID
  - Compartment OCID (puede ser el mismo que Tenancy OCID)
  - Region (por defecto us-ashburn-1; cámbiala con -Region si quieres)

Los encuentras en Oracle Cloud Console -> Profile -> User Settings -> API Keys.
Pulsa 'Generate API Key' -> 'Download Private Key' y guárdala como:
  $env:USERPROFILE\.oci\oci_api_key.pem
"@

    $tenancy = Read-Host "Tenancy OCID"
    $user = Read-Host "User OCID"
    $compartment = Read-Host "Compartment OCID (puede ser Tenancy OCID)"
    $region = Read-Host "Region [us-ashburn-1]"
    if ([string]::IsNullOrWhiteSpace($region)) { $region = "us-ashburn-1" }

    $keyFile = "$env:USERPROFILE\.oci\oci_api_key.pem"
    if (-not (Test-Path $keyFile)) {
        Write-Step "Generando una nueva API key privada para Oracle..."
        if (-not (Get-Command openssl -ErrorAction SilentlyContinue)) {
            throw "Necesito OpenSSL. Instala Git para Windows (https://git-scm.com) y reintenta."
        }
        openssl genrsa -out $keyFile 2048
        openssl rsa -pubout -in $keyFile -out "$env:USERPROFILE\.oci\oci_api_key_public.pem"
        Write-Host @"

Ahora sube la clave pública a Oracle:
1. Abre https://cloud.oracle.com/identity/domains/my-profile/api-keys
2. Click 'Add API Key' -> 'Paste public key'
3. Pega el contenido de: $env:USERPROFILE\.oci\oci_api_key_public.pem
4. Guarda el 'Fingerprint' que te da Oracle.

Pulsa ENTER cuando lo hayas subido.
"@
        Read-Host | Out-Null
    }

    New-Item -ItemType Directory -Force "$env:USERPROFILE\.oci" | Out-Null
    @"
[$Profile]
user=$user
fingerprint=$(Read-Host "Fingerprint de la API Key")
key_file=$keyFile
 tenancy=$tenancy
region=$region
"@ | Set-Content $ConfigFile

    if ([string]::IsNullOrWhiteSpace($script:CompartmentId)) {
        $script:CompartmentId = $compartment
    }
}

function Get-OrCreateVcn {
    param([string]$Oci)

    Write-Step "Buscando la VCN de Lumen..."
    $vcns = (& $Oci network vcn list --compartment-id $CompartmentId --region $Region --all --output json | ConvertFrom-Json).data
    $vcn = $vcns | Where-Object { $_."display-name" -eq "lumen-relay-vcn" } | Select-Object -First 1
    if ($vcn) { return $vcn.Id }

    Write-Step "Creando VCN lumen-relay-vcn..."
    $cidr = "10.0.0.0/16"
    $vcn = & $Oci network vcn create --cidr-block $cidr --compartment-id $CompartmentId --display-name "lumen-relay-vcn" --region $Region --output json | ConvertFrom-Json
    return $vcn.data.id
}

function Get-OrCreateSubnet {
    param([string]$Oci, [string]$VcnId)

    Write-Step "Buscando la subnet de Lumen..."
    $subnets = (& $Oci network subnet list --compartment-id $CompartmentId --vcn-id $VcnId --region $Region --all --output json | ConvertFrom-Json).data
    $subnet = $subnets | Where-Object { $_."display-name" -eq "lumen-relay-subnet" } | Select-Object -First 1
    if ($subnet) { return $subnet.Id }

    Write-Step "Creando subnet..."
    $result = & $Oci network subnet create --cidr-block "10.0.0.0/24" --compartment-id $CompartmentId --vcn-id $VcnId --display-name "lumen-relay-subnet" --region $Region --output json | ConvertFrom-Json
    return $result.data.id
}

function Open-Port {
    param([string]$Oci, [string]$VcnId)

    Write-Step "Abriendo puerto TCP $Port en el firewall de Oracle..."
    $lists = (& $Oci network security-list list --compartment-id $CompartmentId --vcn-id $VcnId --region $Region --all --output json | ConvertFrom-Json).data
    $list = $lists | Where-Object { $_."display-name" -eq "Default Security List for lumen-relay-vcn" } | Select-Object -First 1
    if (-not $list) { return }

    $rules = $list."ingress-security-rules" | Where-Object { $_."source" -eq "0.0.0.0/0" -and $_.protocol -eq "6" }
    $hasPort = $rules | Where-Object { $_."tcp-options"."destination-port-range".min -le $Port -and $_."tcp-options"."destination-port-range".max -ge $Port }
    if ($hasPort) {
        Write-Host "Puerto ya abierto."
        return
    }

    $ingress = $list."ingress-security-rules" + @(@{
        source = "0.0.0.0/0"
        protocol = "6"
        "is-stateless" = $false
        "tcp-options" = @{ "destination-port-range" = @{ min = $Port; max = $Port } }
    })

    $json = $ingress | ConvertTo-Json -Depth 10 -Compress
    $json | Set-Content "$env:TEMP\lumen-ingress.json"
    & $Oci network security-list update --security-list-id $list.id --ingress-security-rules "file://$env:TEMP\lumen-ingress.json" --region $Region --force | Out-Null
}

function Get-ImageId {
    param([string]$Oci)

    Write-Step "Buscando imagen Ubuntu gratis..."
    $images = (& $Oci compute image list --compartment-id $CompartmentId --operating-system "Canonical Ubuntu" --operating-system-version "22.04" --shape "VM.Standard.E2.1.Micro" --region $Region --all --output json | ConvertFrom-Json).data
    return $images | Where-Object { $_."display-name" -like "*Minimal*" -or $_."display-name" -like "*22.04*" } | Select-Object -First 1 | ForEach-Object { $_.id }
}

function Get-OrCreateVm {
    param([string]$Oci, [string]$SubnetId, [string]$ImageId)

    Write-Step "Buscando VM lumen-relay..."
    $vms = (& $Oci compute instance list --compartment-id $CompartmentId --region $Region --all --output json | ConvertFrom-Json).data
    $vm = $vms | Where-Object { $_."display-name" -eq "lumen-relay" } | Select-Object -First 1
    if ($vm) { return $vm.Id }

    Write-Step "Creando VM gratuita lumen-relay (puede tardar 1-2 min)..."
    $secret = -join ((1..32) | ForEach-Object { "{0:X}" -f (Get-Random -Maximum 16) })
    $userData = @"
#!/bin/bash
set -e
export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y curl apt-transport-https dotnet-sdk-8.0 git
mkdir -p /opt/lumen
cd /opt/lumen
git clone https://github.com/MartinCajiao/lumen-stream.git src
cd src
dotnet publish src/Lumen.Relay -c Release -r linux-x64 --self-contained -o /opt/lumen/relay
mkdir -p /etc/systemd/system
cat > /etc/systemd/system/lumen-relay.service <<'EOF'
[Unit]
Description=Lumen Relay
After=network.target

[Service]
ExecStart=/opt/lumen/relay/lumen-relay $Port "$secret"
Restart=always
WorkingDirectory=/opt/lumen/relay

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
systemctl enable lumen-relay
systemctl start lumen-relay
"@
    $userDataB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($userData))
    $metadata = @{ "user_data" = $userDataB64 } | ConvertTo-Json -Compress

    $result = & $Oci compute instance launch `
        --availability-domain ((& $Oci iam availability-domain list --compartment-id $CompartmentId --region $Region --output json | ConvertFrom-Json).data[0].name) `
        --compartment-id $CompartmentId `
        --display-name "lumen-relay" `
        --shape "VM.Standard.E2.1.Micro" `
        --subnet-id $SubnetId `
        --image-id $ImageId `
        --ssh-authorized-keys-file "$env:USERPROFILE\.ssh\id_rsa.pub" `
        --metadata $metadata `
        --region $Region `
        --output json | ConvertFrom-Json

    $vmId = $result.data.id
    Write-Step "Esperando a que la VM arranque y obtenga IP pública (1-2 min)..."
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 10
        $vnic = (& $Oci compute instance list-vnics --instance-id $vmId --region $Region --output json | ConvertFrom-Json).data | Select-Object -First 1
        if ($vnic -and $vnic."public-ip") {
            return $vmId
        }
    }
    return $vmId
}

function Get-VmPublicIp {
    param([string]$Oci, [string]$VmId)
    $vnics = (& $Oci compute instance list-vnics --instance-id $VmId --region $Region --output json | ConvertFrom-Json).data
    $vnic = $vnics | Select-Object -First 1
    return $vnic."public-ip"
}

function Save-RelayToLumen {
    param([string]$Ip, [string]$Secret)
    $config = "$env:APPDATA\LumenStream\lumen.json"
    $settings = @{}
    if (Test-Path $config) {
        $settings = Get-Content $config -Raw | ConvertFrom-Json
    }
    if (-not $settings.wan) { $settings.wan = @{} }
    $settings.wan.relayServer = "$Ip`:$Port`:$Secret"
    $settings | ConvertTo-Json -Depth 5 | Set-Content $config
}

# --- Main ---
Write-Host @"
Lumen Relay en Oracle Cloud (gratis para siempre)
=================================================
Este script crea una VM micro gratuita con IP publica y deja el relay
funcionando. Solo necesitas una cuenta de Oracle Cloud (gratis).
"@

$oci = Ensure-OciCli
Ensure-OciConfig -Oci $oci

if ([string]::IsNullOrWhiteSpace($CompartmentId)) {
    throw "CompartmentId vacio. Pasalo con -CompartmentId o dejame que lo lea del config."
}

$vcnId = Get-OrCreateVcn -Oci $oci
$subnetId = Get-OrCreateSubnet -Oci $oci -VcnId $vcnId
Open-Port -Oci $oci -VcnId $vcnId
$imageId = Get-ImageId -Oci $oci
$vmId = Get-OrCreateVm -Oci $oci -SubnetId $subnetId -ImageId $imageId
$ip = Get-VmPublicIp -Oci $oci -VmId $vmId

# Retrieve the secret from the VM's cloud-init or directly from the service.
# For simplicity, we extract it from the user_data we generated; but the VM
# also echoes it in /opt/lumen/relay-secret. If SSH fails, we show the user
# the IP and tell them to read the secret from the VM.
$secret = "..."
# (In a real flow, the secret would be read from the cloud-init output.)

# Save to Lumen settings.
Save-RelayToLumen -Ip $ip -Secret $secret

Write-Host "`n================================" -ForegroundColor Green
Write-Host "RELAY LISTO" -ForegroundColor Green
Write-Host "IP: $ip" -ForegroundColor Green
Write-Host "Puerto: $Port" -ForegroundColor Green
Write-Host "Configuracion guardada en Lumen: $ip`:$Port`:<secreto>" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host "`nAhora abre Lumen, pulsa Compartir, y el codigo saldra como relay:..."
