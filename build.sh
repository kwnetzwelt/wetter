#!/bin/bash

# Get the latest Git tag or short SHA
GIT_VERSION=$(git describe --tags --always 2>/dev/null)
# Strip leading 'v' if present
CLEAN_VERSION=${GIT_VERSION#v}
export WETTER_BUILD_TAG=${CLEAN_VERSION:-4.5.4}

echo "Building and deploying version: $WETTER_BUILD_TAG"

# Run docker compose with the extracted version
WETTER_BUILD_TAG=$WETTER_BUILD_TAG docker compose up -d --build --force-recreate

echo "Done! Dashboard is running at http://localhost:8080"
echo "Current build version: $WETTER_BUILD_TAG"
