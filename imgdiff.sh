#!/bin/sh

export DYLD_FALLBACK_LIBRARY_PATH="/Library/Frameworks/Mono.framework/Versions/Current/lib:/usr/local/lib:/usr/lib"
APP_PATH=`echo $0 | awk '{split($0,patharr,"/"); idx=1; while(patharr[idx+3] != "") { if (patharr[idx] != "/") {printf("%s/", patharr[idx]); idx++ }} }'`
mono "$APP_PATH/Contents/Resources/ImgDiff.exe" || mono ImgDiff/bin/Release/ImgDiff.exe
