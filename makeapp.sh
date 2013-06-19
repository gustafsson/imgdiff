#!/bin/bash

set -e

# Build the latest Release build
/Applications/MonoDevelop.app/Contents/MacOS/mdtool build -c:"Release|x86" ImgDiff.sln

# couldn't get macpack to work
# macpack -m:2 -o:.  -r:/Library/Frameworks/Mono.framework/Versions/Current/lib/ -n:ImgDiff -a:ImgDiff/bin/Release/ImgDiff.exe

# manually creating an app instead
rm -rf ImgDiff.app/Contents/MacOS
rm -rf ImgDiff.app/Contents/Resources
mkdir -p ImgDiff.app/Contents/MacOS
mkdir -p ImgDiff.app/Contents/Resources
cp ImgDiff/bin/Release/ImgDiff.exe ImgDiff.app/Contents/Resources
cp imgdiff.sh ImgDiff.app/Contents/MacOS/ImgDiff
