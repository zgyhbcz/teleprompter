# 网页版（备用方案）

`index.html` 是一个**完全独立的单文件网页提词器**，不依赖 exe、
不需要安装任何东西，双击即可用默认浏览器打开。

## 用途

- 临时借用别人的电脑，不方便拷 exe
- 系统缺少 .NET Framework 4.x（Windows 7 未装运行库时）
- 想在大屏浏览器 / 平板上快速提词

## 用法

- 双击 `index.html`，或用 `启动提词器.bat` 以无地址栏的全屏窗口打开
- 支持 `.docx`（浏览器端解压）与 `.txt`（自动识别 UTF-8 / GBK）
- 快捷键与 exe 版一致：`空格` 播放暂停、`↑↓` 快慢、`←→` 手动滚、`Tab` 稿件列表、`2`/`F11` 全屏

## 与 exe 版的差别

| | exe 版 | 网页版 |
| --- | --- | --- |
| 双屏独立输出 | ✅ | ❌ 单窗口 |
| 镜像 / 导播线 | ✅ | ✅ |
| 稿件持久化 | 文件 | 浏览器 localStorage |
| 依赖 | .NET 4.0+ | 任意现代浏览器 |

## 许可与致谢

本目录的设计思路（controller / display 分离、CSS transform 镜像、
键盘映射）参考了 [Eyevinn Open Teleprompter](https://github.com/Eyevinn/teleprompter)
（Apache-2.0）。实现为独立重写：不包含服务端、不使用 WebSocket 同步、
未复制其源文件。详见仓库根目录的 [NOTICE](../NOTICE)。
