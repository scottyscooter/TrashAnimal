param(
    [Parameter(Mandatory=$true)]
    [string]$VaultName,
    
    [Parameter(Mandatory=$true)]
    [string]$ProjectPath = ".",
    
    [string]$EnvironmentFilter = "*"  # Optional: filter secrets by prefix
)

# Resolve project path
$projectPath = Resolve-Path $ProjectPath
$csprojFile = Get-ChildItem -Path $projectPath -Filter "*.csproj" -Depth 1 | Select-Object -First 1

if (-not $csprojFile) {
    Write-Error "No .csproj file found in $projectPath"
    exit 1
}

# Extract UserSecretsId from .csproj
[xml]$csproj = Get-Content $csprojFile.FullName
$userSecretsId = $csproj.Project.PropertyGroup.UserSecretsId

if (-not $userSecretsId) {
    Write-Error "UserSecretsId not found in $($csprojFile.Name). Add it to your .csproj:"
    Write-Error "<PropertyGroup><UserSecretsId>YOUR-GUID-HERE</UserSecretsId></PropertyGroup>"
    exit 1
}

Write-Host "📁 Project: $($csprojFile.Name)"
Write-Host "🔑 User Secrets ID: $userSecretsId"
Write-Host "🔓 Vault: $VaultName"
Write-Host ""

# Authenticate with Azure
try {
    $account = az account show 2>$null
    if (-not $account) {
        Write-Host "🔐 Logging into Azure..."
        az login | Out-Null
    }
} catch {
    Write-Error "Failed to authenticate with Azure. Run 'az login' first."
    exit 1
}

# Fetch all secrets from Key Vault
$secrets = az keyvault secret list --vault-name $VaultName --query "[].name" -o tsv

if (-not $secrets) {
    Write-Error "No secrets found in vault '$VaultName' or vault not accessible"
    exit 1
}

# Filter secrets if pattern provided
if ($EnvironmentFilter -ne "*") {
    $secrets = $secrets | Where-Object { $_ -like $EnvironmentFilter }
}

$count = 0

# Set each secret in the project's user-secrets store
foreach ($secretName in $secrets) {
    try {
        $secretValue = az keyvault secret show `
            --vault-name $VaultName `
            --name $secretName `
            --query "value" `
            -o tsv
        
        # Set secret with the project's UserSecretsId
        dotnet user-secrets set "$secretName" "$secretValue" --id $userSecretsId 2>$null
        
        Write-Host "✓ $secretName"
        $count++
    } catch {
        Write-Host "✗ $secretName - Error: $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "✅ Synced $count secrets to $($csprojFile.Name)" -ForegroundColor Green
Write-Host "💾 Secrets stored in: ~/.microsoft/usersecrets/$userSecretsId/secrets.json"