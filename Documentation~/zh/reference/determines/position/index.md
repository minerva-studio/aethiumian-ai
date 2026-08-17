# `Position`

## 用途
读取宿主 AI 游戏对象的世界坐标。

## 关键输入 / 输出
- 输入：无。
- 输出：`Vector3` 位置。

## 成功 / 失败语义
- 始终返回宿主对象的 `Transform` 位置。

## 重要限制
- 除了 `Transform` 存在性检查外，无额外显式有效性校验。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/Position.cs)
