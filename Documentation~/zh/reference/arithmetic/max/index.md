# `Max`

## 用途
计算两个数值或向量的逐分量最大值。

## 关键输入 / 输出
- 输入：`a`, `b`（数值或向量）。
- 输出：`result`，包含每个分量中的较大值。

## 成功 / 失败语义
- 标量和向量组合兼容时成功。
- 组合类型不支持时失败。

## 重要限制
- 标量会广播到向量；两个向量的维度必须一致。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/Max.cs)
