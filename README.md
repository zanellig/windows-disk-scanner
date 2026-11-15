# Windows disk scanner

A small C# command-line scanner that lists the largest directories and files under a Windows path. It uses `FindFirstFileExW` with `FIND_FIRST_EX_LARGE_FETCH` and scans subdirectories in parallel near the top of the tree.

The scanner reads file metadata and writes two tab-separated reports. It does not delete or change scanned files, or send scan results over the network.

## Build

On 64-bit Windows with the .NET Framework 4.x compiler installed, run this in PowerShell from the repository directory:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /optimize+ /target:exe /out:scan.exe .\scan.cs
```

## Run

```powershell
.\scan.exe 'C:\' 100 '.\c-drive'
```

Arguments, in order:

1. Root directory, using a normal absolute local path such as `C:\` or `C:\Users`.
2. Minimum report size in MiB. `100` means 100 × 1,048,576 bytes.
3. Output prefix. Its parent directory must already exist.

The example creates:

- `c-drive_dirs.tsv`: recursive directory totals, largest first.
- `c-drive_files.tsv`: individual files, largest first.

Each line contains a byte count, a tab, and a path. There is no header. The threshold only filters report entries; all readable files contribute to directory totals. Directory rows overlap because each parent includes its descendants, so do not add arbitrary directory rows together.

The console prints the total logical size in decimal GB, the number of directories whose enumeration could not start, and elapsed seconds. Administrator privileges can improve access to protected directories.

## Limits

- These are logical file lengths, not allocated disk space. Sparse or compressed files, cloud placeholders, and hard links can make totals differ from actual drive usage. Hard-linked files can be counted multiple times.
- Directory reparse points, including junctions and directory symlinks, are skipped.
- Inaccessible directories are skipped. The `denied` counter includes every failure to start directory enumeration, not just permission errors. Errors partway through enumeration are not separately reported.
- Files can change while a scan runs. The output is not a filesystem snapshot.
- The scanner expects all three valid arguments and does not validate them. Use ordinary absolute local paths, not UNC or already extended-length paths.
- Reports contain local file and directory names. Review them before sharing. Generated reports and executables are ignored by Git.
