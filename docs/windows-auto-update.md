# Windows Auto Update

This project uses Velopack with GitHub Releases for free Windows auto-updates.

## What the user sees

- The installed Windows app checks GitHub Releases on startup.
- If a newer stable release exists, the app downloads it and restarts into the new version.
- The update source can also be reviewed from the app's `Database Sync` window.

## One-time setup

1. Push this project to a GitHub repository.
2. The workflow file is already included at `.github/workflows/windows-release.yml`.
3. The workflow uses the built-in `GITHUB_TOKEN`, so no extra upload token is required for standard GitHub Releases in the same repository.
4. The update source should point to the repository URL, for example:

```text
https://github.com/your-name/your-repo
```

## Release a new Windows version

1. Pick a new semver version such as `1.0.1`.
2. Create and push a Git tag:

```bash
git tag v1.0.1
git push origin v1.0.1
```

3. GitHub Actions will automatically:

- build the Windows app
- create the Velopack release
- upload the release to GitHub Releases
- attach build artifacts to the workflow run

You can also run the workflow manually from GitHub Actions with `workflow_dispatch`.

## Local optional commands

If you want to build locally before pushing a tag:

```bash
./scripts/package-windows-release.sh 1.0.1
```

The generated Velopack artifacts are placed in `Releases/win-x64`, but the recommended path is the GitHub Actions workflow because it runs on Windows and avoids local cross-packaging issues.

## Important notes

- Always publish a new version number for each release.
- Auto-update applies after you package and upload a release. Code changes do not sync to another computer until a release is published.
- The packaged app can also read `update-repo-url.txt` bundled next to the executable on first install.
