param(
    [string]$CredentialsJsonPath = "docker/mosquitto/credentials.local.json",
    [string]$PasswordsPath = "docker/mosquitto/passwords",
    [string]$AclPath = "docker/mosquitto/acl",
    [switch]$Overwrite
)

if (-not (Get-Command mosquitto_passwd -ErrorAction SilentlyContinue)) {
    throw "Comando 'mosquitto_passwd' nao encontrado. Instale o cliente do Mosquitto para usar este script."
}

if (-not (Test-Path $CredentialsJsonPath)) {
    throw "Arquivo de credenciais nao encontrado em '$CredentialsJsonPath'. Use o template credentials.template.json."
}

$entries = Get-Content $CredentialsJsonPath | ConvertFrom-Json
if ($null -eq $entries -or $entries.Count -eq 0) {
    throw "Nenhuma credencial encontrada em '$CredentialsJsonPath'."
}

if ($Overwrite) {
    if (Test-Path $PasswordsPath) {
        Remove-Item -LiteralPath $PasswordsPath -Force
    }

    if (Test-Path $AclPath) {
        Remove-Item -LiteralPath $AclPath -Force
    }
}

$aclLines = New-Object System.Collections.Generic.List[string]
$firstCredential = -not (Test-Path $PasswordsPath)

foreach ($entry in $entries) {
    $username = "$($entry.username)".Trim()
    $secret = "$($entry.secret)"
    $topic = "$($entry.topic)".Trim()

    if ([string]::IsNullOrWhiteSpace($username) -or
        [string]::IsNullOrWhiteSpace($secret) -or
        [string]::IsNullOrWhiteSpace($topic)) {
        throw "Cada item deve conter username, secret e topic."
    }

    if ($firstCredential) {
        mosquitto_passwd -b -c $PasswordsPath $username $secret | Out-Null
        $firstCredential = $false
    }
    else {
        mosquitto_passwd -b $PasswordsPath $username $secret | Out-Null
    }

    $aclLines.Add("user $username")
    $aclLines.Add("topic write $topic")
    $aclLines.Add("")
}

$aclLines | Set-Content $AclPath
Write-Host "Arquivos gerados com sucesso:"
Write-Host "- $PasswordsPath"
Write-Host "- $AclPath"
Write-Host "Reinicie o broker para aplicar as alteracoes."
