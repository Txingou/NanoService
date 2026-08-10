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

```powershell
dotnet nuget push artifacts\packages\NanoTransport.4.4.0.nupkg --api-key <API_KEY> --source https://api.nuget.org/v3/index.json
dotnet nuget push artifacts\packages\NanoService.4.4.0.nupkg --api-key <API_KEY> --source https://api.nuget.org/v3/index.json
```
