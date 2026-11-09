// Publishes deskband11lib.dll with NativeAOT and stages it in bin/ for each Node.js architecture.
// Usage: node scripts/stage-native.mjs [x64] [arm64]. Defaults to the architecture of the running Node.js.
import { execFileSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const RUNTIME_IDENTIFIERS = { x64: 'win-x64', arm64: 'win-arm64' };

const packageRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const repositoryRoot = resolve(packageRoot, '..', '..');
const projectPath = join(repositoryRoot, 'Deskband11Lib.Native', 'Deskband11Lib.Native.csproj');
const visualStudioInstallerDirectory = join(process.env['ProgramFiles(x86)'] ?? 'C:\\Program Files (x86)', 'Microsoft Visual Studio', 'Installer');

const architectures = process.argv.slice(2);
if (architectures.length === 0) architectures.push(process.arch);
for (const architecture of architectures) {
    if (!(architecture in RUNTIME_IDENTIFIERS)) throw new Error(`Unsupported architecture ${architecture}. Use ${Object.keys(RUNTIME_IDENTIFIERS).join(' or ')}.`);
}

// The NativeAOT linker lookup runs vswhere.exe, which is only on PATH in Visual Studio developer shells.
const environment = { ...process.env };
const pathKey = Object.keys(environment).find(key => key.toUpperCase() === 'PATH') ?? 'PATH';
if (existsSync(visualStudioInstallerDirectory)) environment[pathKey] = `${environment[pathKey] ?? ''};${visualStudioInstallerDirectory}`;

for (const architecture of architectures) {
    const runtimeIdentifier = RUNTIME_IDENTIFIERS[architecture];
    const publishDirectory = mkdtempSync(join(tmpdir(), 'deskband11lib-'));
    try {
        execFileSync('dotnet', ['publish', projectPath, '-c', 'Release', '-r', runtimeIdentifier, '-o', publishDirectory, '-nologo'], { stdio: 'inherit', env: environment });
        const targetDirectory = join(packageRoot, 'bin', `win32-${architecture}`);
        mkdirSync(targetDirectory, { recursive: true });
        copyFileSync(join(publishDirectory, 'deskband11lib.dll'), join(targetDirectory, 'deskband11lib.dll'));
        console.log(`Staged win32-${architecture}: ${join(targetDirectory, 'deskband11lib.dll')}`);
    } finally {
        rmSync(publishDirectory, { recursive: true, force: true });
    }
}
