#!/bin/bash

# Get the current directory path
exeDirectory=$(pwd)

# Retrieve the current system PATH environment variable
currentPath=$PATH

# Check if the current directory is already in the PATH
if [[ ":$currentPath:" != *":$exeDirectory:"* ]]; then
    # Add the current directory to the PATH
    export PATH="$PATH:$exeDirectory"
    echo "Directory added to PATH: $exeDirectory"
else
    echo "Directory already in PATH: $exeDirectory"
fi
