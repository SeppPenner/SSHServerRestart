# Project rules for Claude

## What this is

SSHServerRestart is a tiny Windows application that reboots a Synology DiskStation over SSH. It
reads host, port, user and password from `Config.xml` next to the executable, connects with
[SSH.NET](https://www.nuget.org/packages/SSH.NET/), runs the Synology poweroff task, waits seven
seconds, sends `reboot` and waits for a key press. There is no window, no argument parsing and no
logging, the whole user interface is console output in German. The repository is an application, it
is **not** published as a NuGet package: no `GeneratePackageOnBuild`, no push script. It ships as an
Inno Setup installer that is committed into the repository.

One solution `src/SSHServerRestart.sln` with exactly one project:

- `src/SSHServerRestart/SSHServerRestart.csproj`, `OutputType` `Exe`, `RuntimeIdentifiers`
  `win-x64`, application icon `Restart.ico`.

There is no test project and no `.github` folder.

Layout inside `src/SSHServerRestart`:

- `Program.cs`: everything. `Main` loads the configuration, creates the `SshClient` in a `using` and
  calls the private `Restart`. `InitConfiguration` loads the XML file and swallows every exception by
  returning `null`, `CreateObjectsFromString<T>` runs the `XmlSerializer` over the document string.
  Keep new logic in that shape, one small private method per step.
- `Config.cs`: the `[Serializable]` payload type with `ServerName`, `ServerPort`, `User` and
  `Password`. Property names are the XML element names, renaming one breaks every existing
  `Config.xml` in the field.
- `Config.xml`: the shipped example configuration with the dummy address `202.202.202.202`. Copied
  to the output directory with `CopyToOutputDirectory=Always`, so a local edit in `bin` is
  overwritten by the next build, and the installer ships this file.
- `License.txt`: a byte-identical copy of the repository root `License.txt`, also copied to the
  output directory. Inno Setup reads the one in the project folder as its `LicenseFile`, so both
  have to stay in sync.
- `GlobalUsings.cs`: all usings of the project.

Repository root: `README.md` (the only user documentation, badges plus a link to the changelog),
`Changelog.md`, `License.txt` (MIT), `.gitignore`, `.gitattributes` and the `Setup` folder. There is
no `Updating.md`, no `HowToUse.md` and no screenshots.

`Setup` holds three files:

- `SSHServerRestart-Setup.iss`: the Inno Setup 6 script. `MyAppVersion` is maintained by hand, it
  does **not** come from GitVersion.
- `build-setup-files.bat`: deletes every `bin` and `obj` below `src`, publishes self contained to
  `src/SSHServerRestart/bin/publish` and deletes the `*.pdb` files. It does not compile the
  installer, that is a separate `ISCC.exe` call.
- `SSHServerRestart-Setup.exe`: the built installer, tracked in git even though `.gitignore` ignores
  `*.exe`. It only gets into a commit with `git add -f`.

## Build

```powershell
dotnet build src/SSHServerRestart.sln -c Release
```

There are no tests, so there is no `dotnet test` here. A behaviour change is verified by publishing
and starting the executable: with the shipped `Config.xml` it points at the unreachable dummy
address `202.202.202.202`, so the run ends in the connection timeout without touching any real
hardware.

- Single target framework `net10.0-windows`, no multi-targeting. `RuntimeIdentifiers` is `win-x64`.
- All build properties live directly in the single `.csproj`. There is **no**
  `Directory.Build.props` in this repository.
- `TreatWarningsAsErrors` is enabled, so every warning breaks the build, NuGet warnings (`NU****`)
  from restore included. A clean build reports zero warnings, keep it that way.
- `NU1803` (HTTP source usage during restore) is the one warning suppressed via `NoWarn`. Fix
  warnings instead of extending that list. `NuGetAudit` and `NuGetAuditMode=all` are on, so a
  vulnerable transitive package fails the build too. That is not theory: version 1.0.7.0 stopped
  building some time after its release because `SSH.NET` 2024.2.0 got the advisory
  `GHSA-q939-rpr3-3284` and restore started failing with `NU1903`. Keeping `SSH.NET` current is a
  build requirement, not cosmetics.
- Versions come from GitVersion.MsBuild out of the git tags, for example `1.0.8-1` for the first
  commit after tag `1.0.8`. Never edit a version property or an assembly version by hand. The
  exception is `MyAppVersion` in the `.iss`, which no tool touches.
- Restore needs nuget.org. If a private feed is configured globally on the machine and answers 404
  for public packages, restore fails with `NU1301`. Then build with an explicit source:
  `dotnet build src/SSHServerRestart.sln -c Release --source https://api.nuget.org/v3/index.json`.
- The installer is built with `ISCC.exe` from Inno Setup 6
  (`C:\Program Files (x86)\Inno Setup 6\ISCC.exe`). Calling it through the Bash shell mangles the
  `/O` option because of the MSYS path rewriting, use PowerShell for that.

## Code conventions

Follow the surrounding code, it is consistent throughout every file:

- File header comment block with `<copyright file="..." company="Hämmer Electronics">` and a
  `<summary>`, then the file-scoped namespace.
- XML doc comments on every type and every member, private members included, no exceptions.
- `Nullable`, `ImplicitUsings` and `LangVersion latest` are enabled.
- New `using` directives go into `GlobalUsings.cs`, inside the existing `#pragma warning disable
  IDE0065` block, never at the top of a file. The editorconfig requires usings inside the namespace
  (`csharp_using_directive_placement=inside_namespace:warning`), which global usings cannot satisfy,
  that is what the pragma is for. Do not add other pragmas. The comment text in that block is German
  because Visual Studio generated it, leave it alone.
- Fields, properties, methods and events are always accessed with `this.` qualification
  (`dotnet_style_qualification_for_*` at severity `warning`). `Program` is static, so nothing in the
  current code needs it.
- `src/.editorconfig` also enforces braces everywhere, no multiple blank lines, four spaces, CRLF,
  UTF-8, file scoped namespaces, `System` usings sorted first and `IDE0005` as warning. Analyzer
  warnings are fixed, not silenced.
- Every tracked text file is UTF-8 **without** BOM and CRLF, with one deliberate exception: the
  `.iss` needs a BOM, see below. `Config.xml` and `build-setup-files.bat` contain tabs, the C# files
  do not.
- The console output and the strings in `Config.xml` are German, everything else (code, comments,
  documentation, commit messages) is English.

## Known quirks

Do not silently "clean up" these, they are existing behaviour:

- **The poweroff task never runs.** `Restart` sends
  `client.RunCommand("execute /usr/syno/bin/syno_poweroff_task")`. There is no `execute` command on
  DSM, the login shell answers with `command not found` and exit status 127, and `RunCommand`'s
  return value is thrown away, so nobody ever noticed. What actually reboots the DiskStation is the
  following `client.RunCommand("reboot")`, without the services having been stopped first. Left as
  is on purpose: the correct replacement is `/usr/syno/bin/syno_poweroff_task -d` (`-d` stops the
  services and unmounts without powering off, so the `reboot` afterwards still applies), but that
  cannot be tested without a real DiskStation, and getting it wrong shuts down someone's NAS.
  Discuss before changing.
- **`Thread.Sleep(7000)` between the two commands.** `RunCommand` is synchronous and returns when
  the remote command has finished, so the sleep buys nothing today. It only starts to matter if the
  first command is ever changed into something that keeps working in the background.
- **The application was a `WinExe` until 1.0.8.0.** `OutputType` was `WinExe` with
  `UseWindowsForms=true`, while the whole interface is `Console.WriteLine` and `Console.ReadKey`.
  A GUI subsystem binary gets no console, so every message went nowhere and `Console.ReadKey` threw
  `InvalidOperationException`, which `Main`'s `catch` then printed into the same void. Since 1.0.8.0
  it is a plain `Exe` without `UseWindowsForms`, so the output is actually visible. Do not "restore"
  the `WinExe`.
- **The configuration lives in the installation directory.** `Main` reads
  `AppDomain.CurrentDomain.BaseDirectory + "Config.xml"` and the installer puts the application into
  `{commonpf}` (`C:\Program Files`), so editing the server address needs administrator rights. That
  is the shipped behaviour, the file is deliberately not looked up in `%APPDATA%`.
- **The password is stored in plain text** in `Config.xml`, next to the executable, and the shipped
  example uses `root`. That is the design of this tool, do not add half of a credential store to it.
- **A broken or missing `Config.xml` is not an error.** `InitConfiguration` catches everything and
  returns `null`, `Main` then falls back to `new Config()`, which means empty host, port `0` and
  empty credentials. The run fails a moment later in `SshClient`, and the real cause has already
  scrolled by as an exception dump.
- **`Config.xml` declares `encoding="UTF-8"` and is loaded through `XDocument.Load`**, then handed
  to the `XmlSerializer` as `xDocument.ToString()`, which drops the declaration. The detour over the
  string exists in this shape since the .NET Framework days, changing it gains nothing.
- **The `.iss` must keep its BOM.** Inno Setup 6 only reads a script as UTF-8 if a BOM is present,
  otherwise it interprets the system code page. The file contains `Hämmer Electronics`, so without
  the BOM the installer's publisher depends on the code page of whoever compiles it. It was
  Windows-1252 without BOM up to 1.0.7.0 and is UTF-8 with BOM since 1.0.8.0. Any editor that saves
  "UTF-8 without BOM" silently breaks this again, check the first three bytes after editing.
- **`MyAppVersion` in the `.iss` is maintained by hand** and duplicates the version that GitVersion
  derives from the tag. It is the only place in the repository where a version number is typed.
- **The installer is committed.** `Setup/SSHServerRestart-Setup.exe` is tracked although
  `.gitignore` lists `*.exe`, it needs `git add -f`. Self contained since 1.0.8.0, which puts around
  28 MB per release into the git history instead of the 6 MB of a framework dependent build,
  permanently.
- **The publish is self contained.** `build-setup-files.bat` passes
  `-r win-x64 --self-contained true`, so the target machine needs no installed .NET runtime. Judge
  it by `runtimeconfig.json`: `includedFrameworks` means self contained, a `frameworks` block means
  framework dependent.
- **Quick launch icon on Windows 7.** The `.iss` creates a quick launch shortcut under
  `{userappdata}`, restricted to `OnlyBelowVersion: 0,6.1`, so it never triggers on a supported
  Windows. Inno Setup still warns about it because the installer requires admin rights. Removing
  those two lines would make the compile warning free.
- **`src/SSHServerRestart.sln.DotSettings`** is tracked and holds nothing but a ReSharper user
  dictionary of the German words from the console output (`beenden`, `Beliebige`, `dr_00FCcken`,
  `H_00E4mmer`, ...). Leave it alone.
- **`.gitattributes` is the unmodified Visual Studio template** with `* text=auto` active and every
  specific rule commented out. There is no binary rule for the tracked installer, git recognises it
  by heuristic.
- **AppVeyor and Snyk badges without CI in the repository.** `README.md` links builds that are
  configured outside of this repository. There is no pipeline file here.
- **`README.md`** is spelled with capitals here, the sibling repositories use `Readme.md`. The link
  targets in the badges point at `master`, not at a branch name that GitHub picked automatically.

## Releasing

The tag has to exist **before** the installer is built, otherwise GitVersion burns a prerelease
version such as `1.0.8-3+Branch.master.Sha...` into the shipped executable.

1. Make the change.
2. Add an entry at the top of `Changelog.md` in the existing format:
   `* **Version 1.0.8.0 (2026-08-18)** : Short description.`
3. Set `MyAppVersion` in `Setup/SSHServerRestart-Setup.iss` to the same four part version. Keep the
   BOM and the CRLF line endings.
4. Commit that.
5. Tag the commit with the plain version number, no `v` prefix (`1.0.7`, `1.0.6`, ...). The existing
   tags are lightweight tags, create new ones the same way.
6. Run `Setup/build-setup-files.bat` from the `Setup` folder. The batch does `cd ..\src` relative to
   the current directory, so it has to be started from there.
7. Compile the installer: `ISCC.exe Setup\SSHServerRestart-Setup.iss`. Output goes to
   `Setup/SSHServerRestart-Setup.exe`.
8. `git add -f Setup/SSHServerRestart-Setup.exe`, commit as `Updated setup files.`.
9. Push the commits and the tag.

The version in the `Changelog.md` has four parts (`1.0.8.0`), the tag has three (`1.0.8`).

## Git

- **Never amend a commit.** No `git commit --amend`, not for a typo in the message, not to add a
  forgotten file, not even when the commit is still local. Write a follow-up commit instead. The
  release versions come from tags on exact commits, an amended commit leaves its tag pointing at a
  commit that no longer exists in the branch.

## Writing style

- Commit messages are written **in English only**: short, precise subject line, explanatory body
  when needed.
- Code comments and comments in project files such as `.csproj` are **always English**, regardless
  of the language used in the conversation. The console output of the application is the one place
  where German belongs.
- **No em dashes or en dashes** (`—`, `–`), neither in prose, commit messages, code comments nor
  documentation. Use a regular hyphen, comma, colon, parentheses or a separate sentence.
- German texts (documentation, chat replies) always use real umlauts and ß, never ASCII
  transliterations such as `ae`, `oe`, `ue` or `ss`. Identifiers, file names and configuration keys
  stay unchanged where umlauts are technically undesirable.
