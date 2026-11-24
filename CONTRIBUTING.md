# 贡献指南

感谢你愿意改进 TiCi。下面是最小但必要的约定。

## 环境

- Windows（编译、运行、自检都需要）
- 不需要 Visual Studio 或 .NET SDK，系统自带的 `csc.exe` 就够
- 可选：Visual Studio 2022 / MSBuild（打开 `src/TiCi.csproj`）

## 本地构建

```powershell
powershell -ExecutionPolicy Bypass -File src\build.ps1
# 产物: dist\TiCi.exe
```

## 提交前自检

改动涉及排版或界面时，请至少跑一遍内置自检：

```powershell
dist\TiCi.exe --render-test   # 渲染排版快照到 _selftest\
dist\TiCi.exe --smoke         # 启动 + 开关副屏 + 按键
dist\TiCi.exe --ui-test       # 连续缩放与改参数，统计预览区是否变空白
```

`_selftest\uitest.txt` 里每一步的 `brightPx` 不应出现 0（0 表示预览区画空了）。

## 代码约定

- 语言级别为 **C# 5**（这是系统自带 csc 的上限），不要使用字符串插值、
  `?.`、表达式体成员等 C# 6+ 语法
- 只引用 `System` / `System.Drawing` / `System.Windows.Forms`，
  不要引入 NuGet 包或 .NET 4.5+ 的门面程序集（会破坏 Win7 兼容性）
- 源文件使用 **UTF-8 with BOM**，缩进 4 空格
- 界面文案使用简体中文

## 提交信息

采用 Conventional Commits 风格：

```
feat: 支持自定义导播线位置
fix: 修正窄窗口下工具栏换行错位
docs: 补充 Win7 兼容性说明
```

## Pull Request

1. 从 `main` 开分支，命名如 `fix/preview-blank`
2. 一个 PR 只做一件事，附带复现步骤或截图
3. 在 PR 描述里说明：改了什么、为什么改、怎么验证的
4. CI 必须通过

## 报告问题

请使用 Issue 模板，附上：

- Windows 版本、屏幕分辨率与缩放比例
- 复现步骤
- 截图或 `_selftest\` 下的输出

感谢你的贡献。
