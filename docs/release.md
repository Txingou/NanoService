# 发布流程

## 版本管理

版本号统一维护在 `Directory.Build.props` 的 `<Version>` 中，需要发布新版本时手动递增（例如 `4.5.0 -> 4.6.0`），并同步更新 `AssemblyVersion` 与 `FileVersion`。

## GitHub Actions 发布

仓库通过 `.github/workflows/build.yml` 使用 NuGet Trusted Publishing 发布，每次推送到 `main`（或手动触发）时执行：还原、测试、打包 `NanoTransport` 与 `NanoService`，然后发布到 nuget.org。

- NuGet.org 已配置受信任发布策略：仓库 `Txingou/NanoService`，工作流文件 `build.yml`，环境 `production`。
- 工作流通过 `NuGet/login@v1` 使用 GitHub OIDC 换取短期 API Key，不需要存储长期密钥。
- 登录用户默认 `Mr.Ming`；如与 nuget.org 用户名不同，可在仓库 Settings -> Secrets and variables -> Actions 添加变量 `NUGET_USER` 覆盖。
- 发布使用 `--skip-duplicate`，重复版本不会重新上传。

## 手动打包

```powershell
dotnet pack src\NanoTransport -c Release -o artifacts\packages
dotnet pack src\NanoService -c Release -o artifacts\packages
```
