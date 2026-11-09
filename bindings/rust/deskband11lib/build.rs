// Copies the native library for the target architecture next to the executables of the build.
use std::env;
use std::fs;
use std::path::PathBuf;

fn main() {
    println!("cargo:rerun-if-changed=bin");
    if env::var("CARGO_CFG_TARGET_OS").as_deref() != Ok("windows") {
        return;
    }

    let runtime_identifier = match env::var("CARGO_CFG_TARGET_ARCH").as_deref() {
        Ok("x86_64") => "win-x64",
        Ok("aarch64") => "win-arm64",
        _ => return,
    };

    let manifest_directory =
        PathBuf::from(env::var("CARGO_MANIFEST_DIR").expect("Cargo sets CARGO_MANIFEST_DIR."));
    let library_path = manifest_directory
        .join("bin")
        .join(runtime_identifier)
        .join("deskband11lib.dll");
    if !library_path.is_file() {
        println!(
            "cargo:warning=The deskband11lib native library for {runtime_identifier} is missing at {}.",
            library_path.display()
        );
        return;
    }

    // Debug builds fall back to this path, so tests and examples outside the profile directory find the library.
    println!(
        "cargo:rustc-env=DESKBAND11LIB_BUNDLED_LIBRARY_PATH={}",
        library_path.display()
    );

    // OUT_DIR is <target>/<profile>/build/<package>/out, so the profile directory that holds the executables is three levels up.
    let output_directory = PathBuf::from(env::var("OUT_DIR").expect("Cargo sets OUT_DIR."));
    let Some(profile_directory) = output_directory.ancestors().nth(3) else {
        return;
    };

    let destination_path = profile_directory.join("deskband11lib.dll");
    if let Err(error) = fs::copy(&library_path, &destination_path) {
        println!(
            "cargo:warning=Could not copy the deskband11lib native library to {}: {error}",
            destination_path.display()
        );
    }
}
