#!/bin/bash
# Publishes a NativeAOT build of ReProgman for Linux and packs it into a tarball.
# Requires clang and the zlib development files for the native linker.
#
#   ./build/publish-linux.sh [runtime-identifier]
#
# Output: artifacts/publish-linux/ReProgman-<rid>.tar.gz, containing a
# ReProgman-<rid>/ folder with the executable and its native libraries.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [ $# -ge 1 ]; then
    rid="$1"
elif [ "$(uname -m)" = "aarch64" ]; then
    rid="linux-arm64"
else
    rid="linux-x64"
fi

output="$root/artifacts/publish-linux"
staging="$output/.publish-$rid"
package="$output/ReProgman-$rid"

echo "Publishing $rid with NativeAOT..."
rm -rf "$staging" "$package" "$package.tar.gz"
dotnet publish "$root/src/ReProgman/ReProgman.csproj" \
    --configuration Release \
    --runtime "$rid" \
    --output "$staging"

echo "Assembling $package..."
mkdir -p "$package"
cp "$staging/ReProgman" "$package/"
# Skia and HarfBuzz stay next to the executable, where the default native
# library probing finds them. The .dbg/.pdb symbols are far bigger than the
# program and are left out.
cp "$staging"/*.so "$package/"

# tar rather than zip, because it keeps the executable bit.
tar -C "$output" -czf "$package.tar.gz" "ReProgman-$rid"
rm -rf "$staging"
echo "Done: $package.tar.gz"
