# `Assign`

## 用途
将源值写入可写目标变量。

## 关键输入 / 输出
- 输入：`destination` (`VariableReference`), `source` (`VariableField`)。
- 输出：写入到目标变量。

## 成功 / 失败语义
- 源值与目标类型兼容时成功。
- 源值与目标不兼容时失败。

## 重要限制
- 目标变量必须可写。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/Assign.cs)
