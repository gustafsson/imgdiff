#!/bin/bash

set -e

# macpack -m:2 -o:.  -r:/Library/Frameworks/Mono.framework/Versions/Current/lib/ -n:ImgDiff -a:ImgDiff/bin/Release/ImgDiff.exe

rm -rf ImgDiff.app/Contents/MacOS
rm -rf ImgDiff.app/Contents/Resources
mkdir -p ImgDiff.app/Contents/MacOS
mkdir -p ImgDiff.app/Contents/Resources
cp ImgDiff/bin/Release/ImgDiff.exe ImgDiff.app/Contents/Resources
cp imgdiff.sh ImgDiff.app/Contents/MacOS/ImgDiff
