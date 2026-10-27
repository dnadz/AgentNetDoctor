"""Build on macOS; never install or change host network settings."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import plistlib
import re
import shutil
import subprocess
import sys
import urllib.request
import xml.etree.ElementTree as ET


def run(*args):
    subprocess.run(args, check=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("rid", choices=["osx-arm64", "osx-x64"])
    rid = parser.parse_args().rid
    if sys.platform != "darwin":
        raise SystemExit("Mac packaging requires macOS for code-signature and bundle checks.")
    root = Path(__file__).resolve().parent.parent
    os.chdir(root)
    version = "0.1.0-macos-preview.1"
    output = root / "artifacts" / f"AgentNetDoctor-{version}-{rid}"
    if output.exists():
        raise SystemExit("Output already exists; use a fresh build directory to avoid stale files.")
    bundle = output / "AgentNetDoctor.app"
    binary = bundle / "Contents" / "MacOS"
    resources = bundle / "Contents" / "Resources"
    binary.mkdir(parents=True)
    resources.mkdir(parents=True)
    run("dotnet", "publish", "src/AgentNetDoctor.MacApp", "-c", "Release", "-r", rid,
        "--self-contained", "true", "-p:RuntimeFrameworkVersion=10.0.11",
        f"-p:Version={version}", "-p:DebugType=None", "-p:DebugSymbols=false", "-o", str(binary))
    host = binary / "AgentNetDoctor.MacApp"
    host.chmod(0o755)
    architecture = subprocess.check_output(["file", str(host)], text=True)
    if ("arm64" if rid == "osx-arm64" else "x86_64") not in architecture:
        raise SystemExit("App host architecture mismatch")
    info = dict(CFBundleExecutable=host.name, CFBundleIdentifier="io.github.dnadz.AgentNetDoctor",
                CFBundleName="Agent 网络医生", CFBundleDisplayName="Agent 网络医生",
                CFBundlePackageType="APPL", CFBundleShortVersionString="0.1.0", CFBundleVersion="1",
                LSMinimumSystemVersion="14.0", NSHighResolutionCapable=True)
    with (bundle / "Contents" / "Info.plist").open("wb") as destination:
        plistlib.dump(info, destination)
    shutil.copy2(root / "LICENSE", resources / "LICENSE")
    shutil.copy2(root / "docs" / "macos-guide.txt", resources / "使用说明.txt")
    notices = resources / "third-party"
    notices.mkdir()
    assets = json.loads((root / "src/AgentNetDoctor.MacApp/obj/project.assets.json").read_text())
    folders = [Path(folder) for folder in assets["packageFolders"]]
    # Runtime packs are downloadDependencies, not normal assets libraries.
    runtime = next((folder / f"microsoft.netcore.app.runtime.{rid}" / "10.0.11"
                    for folder in folders if (folder / f"microsoft.netcore.app.runtime.{rid}" / "10.0.11").is_dir()), None)
    if runtime is None:
        raise SystemExit("Missing self-contained runtime license source")
    runtime_notices = notices / "dotnet-runtime-10.0.11"
    runtime_notices.mkdir()
    for filename in ("LICENSE.TXT", "THIRD-PARTY-NOTICES.TXT"):
        shutil.copy2(runtime / filename, runtime_notices / filename)
    for name, library in assets["libraries"].items():
        if library["type"] != "package":
            continue
        package = next((folder / library["path"] for folder in folders if (folder / library["path"]).is_dir()), None)
        if package is None:
            raise SystemExit(f"Missing package license source: {name}")
        destination = notices / name.replace("/", "-")
        destination.mkdir()
        spec = next(package.glob("*.nuspec"))
        shutil.copy2(spec, destination / spec.name)
        for source in package.rglob("*"):
            if source.is_file() and any(word in source.name.lower() for word in ("license", "licence", "notice", "copying")):
                relative = source.relative_to(package)
                target = destination / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, target)
        license_node = ET.parse(spec).find(".//{*}license")
        if license_node is not None and license_node.get("type") == "expression":
            expression = (license_node.text or "").strip()
            if not re.fullmatch(r"[A-Za-z0-9.+-]+", expression):
                raise SystemExit(f"Review compound license before distribution: {name}: {expression}")
            with urllib.request.urlopen("https://licenses.nuget.org/" + expression, timeout=30) as response:
                (destination / "LICENSE-expression.txt").write_bytes(response.read(1_048_576))
    run("codesign", "--force", "--deep", "--sign", "-", str(bundle))
    run("codesign", "--verify", "--deep", "--strict", str(bundle))
    shutil.copy2(root / "docs" / "macos-guide.txt", output / "使用说明.txt")
    hashes = []
    for source in sorted(output.rglob("*")):
        if source.is_symlink():
            raise SystemExit("Unexpected symlink in portable package")
        if source.is_file():
            if source.suffix in (".pdb", ".log") or "Tests.dll" in source.name:
                raise SystemExit("Unexpected private/debug file in package")
            hashes.append(hashlib.sha256(source.read_bytes()).hexdigest() + "  " + source.relative_to(output).as_posix())
    (output / "SHA256SUMS.txt").write_text("\n".join(hashes) + "\n", encoding="utf-8")
    archive = output.with_suffix(output.suffix + ".zip")
    # Manifest stays outside the signed .app, so it cannot invalidate the resource seal.
    run("ditto", "-c", "-k", "--keepParent", str(output), str(archive))
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    Path(str(archive) + ".sha256").write_text(digest + "  " + archive.name + "\n", encoding="utf-8")
    print(architecture.strip())
    print(f"Verified bundle structure and ad-hoc signature; {len(hashes)} file hashes; ZIP SHA256: {digest}")


if __name__ == "__main__":
    main()
