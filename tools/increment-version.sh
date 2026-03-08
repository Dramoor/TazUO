#!/bin/bash

set -e

CSPROJ_FILE="$(dirname "$0")/../src/ClassicUO.Client/ClassicUO.Client.csproj"

if [ ! -f "$CSPROJ_FILE" ]; then
    echo "Error: Could not find $CSPROJ_FILE"
    exit 1
fi

# Read current version
CURRENT_VERSION=$(grep -oP '<AssemblyVersion>\K[^<]+' "$CSPROJ_FILE")

if [ -z "$CURRENT_VERSION" ]; then
    echo "Error: Could not find current version in $CSPROJ_FILE"
    exit 1
fi

echo "Current version: $CURRENT_VERSION"
echo ""
echo "Select which component to increment:"
echo "1) Major (X.0.0)"
echo "2) Minor (X.Y.0)"
echo "3) Incremental/Patch (X.Y.Z)"
echo ""
read -p "Enter choice (1-3): " choice

# Parse version components
IFS='.' read -r major minor patch <<< "$CURRENT_VERSION"

case $choice in
    1)
        major=$((major + 1))
        minor=0
        patch=0
        echo "Incrementing Major version"
        ;;
    2)
        minor=$((minor + 1))
        patch=0
        echo "Incrementing Minor version"
        ;;
    3)
        patch=$((patch + 1))
        echo "Incrementing Incremental version"
        ;;
    *)
        echo "Invalid choice. Exiting."
        exit 1
        ;;
esac

NEW_VERSION="$major.$minor.$patch"

echo ""
echo "Updating version from $CURRENT_VERSION to $NEW_VERSION"

# Update both AssemblyVersion and FileVersion
sed -i "s|<AssemblyVersion>$CURRENT_VERSION</AssemblyVersion>|<AssemblyVersion>$NEW_VERSION</AssemblyVersion>|g" "$CSPROJ_FILE"
sed -i "s|<FileVersion>$CURRENT_VERSION</FileVersion>|<FileVersion>$NEW_VERSION</FileVersion>|g" "$CSPROJ_FILE"

echo "Version updated successfully!"
echo "New version: $NEW_VERSION"
