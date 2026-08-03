#!/bin/bash

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default values
PROJECT_PATH="."
ENVIRONMENT_FILTER="*"
VAULT_NAME=""

# Function to print usage
usage() {
    cat << EOF
Usage: $0 -v VAULT_NAME [-p PROJECT_PATH] [-f FILTER]

Options:
    -v, --vault VAULT_NAME      Azure Key Vault name (required)
    -p, --project PROJECT_PATH  Path to project directory (default: current directory)
    -f, --filter FILTER         Filter secrets by prefix/pattern (default: all)
    -h, --help                  Show this help message

Examples:
    $0 -v myapp-test-vault
    $0 -v myapp-test-vault -p ./src/MyApi
    $0 -v myapp-test-vault -f "Database*"

EOF
    exit 1
}

# Parse command-line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -v|--vault)
            VAULT_NAME="$2"
            shift 2
            ;;
        -p|--project)
            PROJECT_PATH="$2"
            shift 2
            ;;
        -f|--filter)
            ENVIRONMENT_FILTER="$2"
            shift 2
            ;;
        -h|--help)
            usage
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            usage
            ;;
    esac
done

# Validate required parameters
if [[ -z "$VAULT_NAME" ]]; then
    echo -e "${RED}Error: Vault name is required (-v or --vault)${NC}"
    usage
fi

# Resolve project path
if [[ ! -d "$PROJECT_PATH" ]]; then
    echo -e "${RED}Error: Project path does not exist: $PROJECT_PATH${NC}"
    exit 1
fi

PROJECT_PATH=$(cd "$PROJECT_PATH" && pwd)

# Find .csproj file
CSPROJ_FILE=$(find "$PROJECT_PATH" -maxdepth 1 -name "*.csproj" | head -1)

if [[ -z "$CSPROJ_FILE" ]]; then
    echo -e "${RED}Error: No .csproj file found in $PROJECT_PATH${NC}"
    exit 1
fi

CSPROJ_NAME=$(basename "$CSPROJ_FILE")

# Extract UserSecretsId from .csproj
# Try using xmllint first (more reliable), fall back to grep
if command -v xmllint &> /dev/null; then
    USER_SECRETS_ID=$(xmllint --xpath "string(//UserSecretsId)" "$CSPROJ_FILE" 2>/dev/null)
else
    # Fallback: use grep with regex (simple approach)
    USER_SECRETS_ID=$(grep -oP '(?<=<UserSecretsId>)[^<]+' "$CSPROJ_FILE" | head -1)
fi

if [[ -z "$USER_SECRETS_ID" ]]; then
    echo -e "${RED}Error: UserSecretsId not found in $CSPROJ_NAME${NC}"
    exit 1
fi

echo -e "${BLUE}📁 Project:${NC} $CSPROJ_NAME"
echo -e "${BLUE}🔑 User Secrets ID:${NC} $USER_SECRETS_ID"
echo -e "${BLUE}🔓 Vault:${NC} $VAULT_NAME"
echo ""

# Check Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo -e "${RED}Error: Azure CLI is not installed. Install it with: brew install azure-cli${NC}"
    exit 1
fi

# Check dotnet CLI is installed
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}Error: .NET CLI is not installed${NC}"
    exit 1
fi

# Authenticate with Azure
if ! az account show &> /dev/null; then
    echo -e "${YELLOW}🔐 Logging into Azure...${NC}"
    az login
    if [[ $? -ne 0 ]]; then
        echo -e "${RED}Error: Failed to authenticate with Azure${NC}"
        exit 1
    fi
fi

# Fetch all secrets from Key Vault
SECRETS=$(az keyvault secret list --vault-name "$VAULT_NAME" --query "[].name" -o tsv 2>/dev/null)

if [[ -z "$SECRETS" ]]; then
    echo -e "${RED}Error: No secrets found in vault '$VAULT_NAME' or vault not accessible${NC}"
    exit 1
fi

# Filter secrets if pattern provided
if [[ "$ENVIRONMENT_FILTER" != "*" ]]; then
    FILTERED_SECRETS=""
    while IFS= read -r secret; do
        if [[ "$secret" == $ENVIRONMENT_FILTER ]]; then
            FILTERED_SECRETS+="$secret"$'\n'
        fi
    done <<< "$SECRETS"
    SECRETS="$FILTERED_SECRETS"
fi

# Remove trailing newlines
SECRETS=$(echo "$SECRETS" | sed '/^$/d')

if [[ -z "$SECRETS" ]]; then
    echo -e "${YELLOW}⚠️  No secrets matched filter: $ENVIRONMENT_FILTER${NC}"
    exit 0
fi

COUNT=0

# Set each secret in the project's user-secrets store
while IFS= read -r secret_name; do
    [[ -z "$secret_name" ]] && continue
    
    # Fetch the secret value
    SECRET_VALUE=$(az keyvault secret show \
        --vault-name "$VAULT_NAME" \
        --name "$secret_name" \
        --query "value" \
        -o tsv 2>/dev/null)
    
    if [[ $? -eq 0 ]]; then
        # Set the secret using dotnet user-secrets with the project's UserSecretsId
        if dotnet user-secrets set "$secret_name" "$SECRET_VALUE" --id "$USER_SECRETS_ID" &> /dev/null; then
            echo -e "${GREEN}✓${NC} $secret_name"
            ((COUNT++))
        else
            echo -e "${RED}✗${NC} $secret_name - Failed to set secret"
        fi
    else
        echo -e "${RED}✗${NC} $secret_name - Failed to retrieve from vault"
    fi
done <<< "$SECRETS"

echo ""
echo -e "${GREEN}✅ Synced $COUNT secrets to $CSPROJ_NAME${NC}"
echo -e "${BLUE}💾 Secrets stored in:${NC} ~/.microsoft/usersecrets/$USER_SECRETS_ID/secrets.json"