from pathlib import Path

from hatchling.builders.hooks.plugin.interface import BuildHookInterface

PLATFORM_TAGS = {"win-x64": "win_amd64", "win-arm64": "win_arm64", "win-x86": "win32"}


class NativeLibraryBuildHook(BuildHookInterface):
    """Tags the wheel with every platform whose native library is staged under src/deskband11lib/bin."""

    def initialize(self, version, build_data):
        if self.target_name != "wheel":
            return

        library_directory = Path(self.root) / "src" / "deskband11lib" / "bin"
        runtime_identifiers = sorted(path.parent.name for path in library_directory.glob("*/deskband11lib.dll") if path.parent.name in PLATFORM_TAGS)
        if not runtime_identifiers:
            raise RuntimeError("No native library is staged. Run scripts/stage_native.py before building a wheel.")

        build_data["pure_python"] = False
        build_data["tag"] = "py3-none-" + ".".join(PLATFORM_TAGS[runtime_identifier] for runtime_identifier in runtime_identifiers)
