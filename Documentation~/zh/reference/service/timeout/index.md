# `Timeout`

## 用途
在经过配置时间后中断宿主执行。

## 关键输入 / 输出
- 输入：`时间` (`VariableField<float>`), `result` (`失败`/`Success`)。
- 输出：无。

## 成功 / 失败语义
- 安排超时回调；中断结果遵循配置的 `result`。

## 重要限制
- 使用固定 `delta` 计时并在服务重注册时重置。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Services/Timeout.cs)
