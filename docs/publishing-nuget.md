# Publishing VelocityGrid to NuGet.org

VelocityGrid publishes three packages at exactly the same version. Publish them in dependency order:

1. `VelocityGrid.Native.WinUI`
2. `VelocityGrid.WinUI`
3. `VelocityGrid.Wpf`

The first public release is `0.1.0-preview.1`. NuGet versions are immutable: never rebuild or replace a version after uploading it. If a correction is needed, increment the version.

## One-time account setup

1. Create or sign in to an account at [nuget.org](https://www.nuget.org/).
2. Enable two-factor authentication and save the recovery codes.
3. Make the GitHub repository public before publishing so the package project, documentation, source, and issue links are available to consumers.
4. Confirm that the three package IDs above are still available by visiting their package URLs or beginning an upload.
5. In the nuget.org account menu, open **API Keys** and create a key:
   - name: `VelocityGrid GitHub Actions`;
   - scope: **Push new packages and package versions**;
   - glob pattern: `VelocityGrid.*`;
   - expiry: choose a short practical period and rotate it before expiry.
6. Copy the key immediately. NuGet displays it only once.
7. In the GitHub repository, open **Settings → Secrets and variables → Actions**, create a repository secret named `NUGET_API_KEY`, and paste the key. Never put the key in a file, command line committed to Git, issue, or workflow log.

NuGet.org Trusted Publishing is preferable when it is available for the account because it replaces the long-lived secret with short-lived credentials. The current workflow uses a scoped API key for a predictable first release and can be migrated later.

## Prepare the release

1. Ensure the working tree contains only the intended release changes.
2. Update `CHANGELOG.md` and all package versions together.
3. Run the complete package build from the repository root:

   ```powershell
   ./build/Build-Packages.ps1 -Version 0.1.0-preview.1
   ```

4. Confirm that `VelocityGrid/artifacts/packages` contains three `.nupkg` files and two managed `.snupkg` files.
5. Run the manual checks in `docs/release-checklist.md` against this exact commit.
6. Optionally preview each `.nupkg` without publishing by using nuget.org **Upload Package**. Review the title, description, README, dependencies, licence, repository link, and contained files, then cancel the upload.

## Commit and publish

The repository workflow publishes only tags beginning with `v`.

```powershell
git add --all
git commit -m "Prepare VelocityGrid 0.1.0-preview.1"
git push origin main
git tag -a v0.1.0-preview.1 -m "VelocityGrid 0.1.0-preview.1"
git push origin v0.1.0-preview.1
```

Pushing the tag starts `.github/workflows/packages.yml`. It rebuilds the packages from the tagged commit, validates all three isolated consumers, then publishes the native package, managed package, WPF package, and managed symbols in dependency order.

Do not create or push the tag until the `main` workflow for the release commit is green. A tag should identify the exact source that produced the package.

## Verify after publishing

1. Open **Manage packages** on nuget.org and wait for all validation/indexing checks to finish.
2. Verify the three public pages and their dependency graphs.
3. Create a clean application or clear local package caches, install `VelocityGrid.WinUI` or `VelocityGrid.Wpf` from nuget.org, and run it.
4. Confirm the C++ package installs into a clean C++/WinRT project and its projection header resolves.
5. Create a matching GitHub Release from the tag and use the changelog entry as its notes.

If a serious defect is found, unlist the affected version from **Manage packages** and publish a new version. NuGet packages cannot be overwritten or normally deleted.
