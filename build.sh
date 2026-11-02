#!/usr/bin/env bash
# Builds the mod with the Roslyn compiler from Visual Studio Build Tools (the game uses newer C# features), against the game's own Mono assemblies.
#   GAME=/d/SteamLibrary/steamapps/common/"Probably Stolen" CSC=/path/to/csc.exe ./build.sh [--install]
set -euo pipefail
cd "$(dirname "$0")"
GAME="${GAME:-/d/SteamLibrary/steamapps/common/Probably Stolen}"
M="$GAME/Probably Stolen_Data/Managed"
CSC="${CSC:-/c/Program Files (x86)/Microsoft Visual Studio/18/BuildTools/MSBuild/Current/Bin/Roslyn/csc.exe}"
mkdir -p out/window_workspaces

refs=()
for r in mscorlib netstandard System System.Core Assembly-CSharp Assembly-CSharp-firstpass 0Harmony \
         UnityEngine UnityEngine.CoreModule UnityEngine.UI UnityEngine.UIModule UnityEngine.InputLegacyModule UnityEngine.TextRenderingModule UnityEngine.IMGUIModule Unity.TextMeshPro; do
  [ -f "$M/$r.dll" ] && refs+=("-r:$M/$r.dll")
done

# The .NET Framework csc cannot create its temp files under long paths, so compile in a short folder.
# It also reads "/" as an option prefix, so sources and output use backslashes.
WORK="${TEMP:-/tmp}/ww_build"
rm -rf "$WORK" && mkdir -p "$WORK/src"
cp src/*.cs "$WORK/src/"
srcs=()
for f in src/*.cs; do srcs+=("src\\$(basename "$f")"); done
( cd "$WORK" && "$CSC" -nologo -nostdlib -noconfig -target:library -optimize+ -langversion:latest -warn:4 -nowarn:1701,1702 \
  "${refs[@]}" '-out:WindowWorkspaces.dll' "${srcs[@]}" )
cp "$WORK/WindowWorkspaces.dll" out/window_workspaces/

cp manifest.xml out/window_workspaces/
cp preview.png out/window_workspaces/
mkdir -p out/window_workspaces/Localization
cp Localization/*.csv out/window_workspaces/Localization/
echo "built out/window_workspaces"

if [ "${1:-}" = "--install" ]; then
  rm -rf "$GAME/Mods/window_workspaces"
  cp -r out/window_workspaces "$GAME/Mods/"
  echo "installed to $GAME/Mods/window_workspaces"
fi
