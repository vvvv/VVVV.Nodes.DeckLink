#!/usr/bin/bash
assemblyInfo=$(echo $1 | sed 's/\\/\//g') # replace windows \ with unix /
echo $assemblyInfo
sed -i "37s/([^()]*)/\(\"$(git describe --long)\"\)/g" $assemblyInfo
#sleep 10 # Use for debugging bash script
