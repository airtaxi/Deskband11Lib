"""Publishes deskband11lib.dll with NativeAOT and stages it in the package for each runtime identifier."""

from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
import sysconfig
import tempfile
from pathlib import Path

PACKAGE_ROOT = Path(__file__).resolve().parents[1]
REPOSITORY_ROOT = PACKAGE_ROOT.parents[1]
PROJECT_PATH = REPOSITORY_ROOT / "Deskband11Lib.Native" / "Deskband11Lib.Native.csproj"
LIBRARY_DIRECTORY = PACKAGE_ROOT / "src" / "deskband11lib" / "bin"
RUNTIME_IDENTIFIERS = {"win-amd64": "win-x64", "win-arm64": "win-arm64", "win32": "win-x86"}
VISUAL_STUDIO_INSTALLER_DIRECTORY = Path(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")) / "Microsoft Visual Studio" / "Installer"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("runtime_identifiers", nargs="*", choices=sorted(RUNTIME_IDENTIFIERS.values()), help="Runtime identifiers to publish. Defaults to the architecture of the running interpreter.")
    parser.add_argument("--configuration", default="Release", help="Build configuration. Defaults to Release.")
    arguments = parser.parse_args()

    runtime_identifiers = arguments.runtime_identifiers
    if not runtime_identifiers:
        platform = sysconfig.get_platform()
        if platform not in RUNTIME_IDENTIFIERS:
            parser.error(f"The {platform} platform is not supported. Pass a runtime identifier explicitly.")
        runtime_identifiers = [RUNTIME_IDENTIFIERS[platform]]

    # The NativeAOT linker lookup runs vswhere.exe, which is only on PATH in Visual Studio developer shells.
    environment = dict(os.environ)
    if VISUAL_STUDIO_INSTALLER_DIRECTORY.is_dir():
        environment["PATH"] = environment.get("PATH", "") + os.pathsep + str(VISUAL_STUDIO_INSTALLER_DIRECTORY)

    for runtime_identifier in runtime_identifiers:
        with tempfile.TemporaryDirectory() as publish_directory:
            subprocess.run(["dotnet", "publish", str(PROJECT_PATH), "-c", arguments.configuration, "-r", runtime_identifier, "-o", publish_directory, "-nologo"], check=True, env=environment)
            target_path = LIBRARY_DIRECTORY / runtime_identifier / "deskband11lib.dll"
            target_path.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(Path(publish_directory) / "deskband11lib.dll", target_path)
            print(f"Staged {runtime_identifier}: {target_path}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
