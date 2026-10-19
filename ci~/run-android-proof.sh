#!/usr/bin/env bash
set -euo pipefail

apk="${1:?usage: run-android-proof.sh APK}"
package=com.yes2games.yes2sdk.ciproof
marker=YES2SDK_CONTROL_PLANE_PROOF

adb install -r "$apk"
adb logcat -c
adb shell monkey -p "$package" -c android.intent.category.LAUNCHER 1 >/dev/null

line=""
for _ in $(seq 1 180); do
  line=$(adb logcat -d -s Unity:I | grep -m1 "$marker" || true)
  [ -n "$line" ] && break
  sleep 1
done

if [ -z "$line" ]; then
  adb logcat -d -s Unity:V AndroidRuntime:E DEBUG:V libc:F ActivityManager:I | tail -200
  echo "::error title=Android control plane proof failed::no $marker line from $package within 180 s"
  exit 1
fi

result="${line#*"$marker" }"
echo "$result"
case "$result" in
  *'"result":"pass","mode":"full","platform":"Android"'*)
    echo "Android IL2CPP control plane proof passed on the emulator"
    ;;
  *)
    adb logcat -d -s Unity:V AndroidRuntime:E | tail -200
    echo "::error title=Android control plane proof failed::$result"
    exit 1
    ;;
esac
