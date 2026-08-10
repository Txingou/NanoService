# 发布流程

## 自动版本递增

每次 `git push` 推送分支到远程时，仓库内置的 `pre-push` hook 会自动把 `Directory.Build.props` 中的版本号递增一个小版本（例如 `4.3.1 -> 4.4.0`），追加提交 `chore: bump version to x.y.z`，并随本次推送一起上传。

- 安装 hook：`git config core.hooksPath .githooks`
- 手动递增：`.\scripts\bump-version.ps1`，可用 `-Part patch|minor|major` 或 `-Version 4.5.0` 指定
- 手动推送：`.\scripts\push.ps1`
- 禁用自动递增：`git config nano.autoversion false`
- 临时跳过 hook：`git push --no-verify`

## 打包

```powershell
dotnet pack src\NanoTransport -c Release -o artifacts\packages
dotnet pack src\NanoService -c Release -o artifacts\packages
```

## 发布 NuGet

NuGet 发布已由 GitHub Actions 工作流 `.github/workflows/nuget-release.yml` 接管，无需在本地使用 API Key 推送。

- 工作流在每次推送到 `main`（或手动触发）时执行：还原、测试、打包 `NanoService` 与 `NanoTransport`，然后发布到 nuget.org。
- 首次配置：在 GitHub 仓库 Settings -> Secrets and variables -> Actions 中新增 secret `NUGET_API_KEY`，值为 nuget.org 的 API Key。
- 推送使用 `--skip-duplicate`，重复版本不会被重新上传。
- 本地如需手动打包，仍可使用：

```powershell
dotnet pack src\NanoTransport -c Release -o artifacts\packages
dotnet pack src\NanoService -c Release -o artifacts\packages
```
