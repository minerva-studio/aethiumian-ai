# `Branch`

## 用途
在当前服务槽位启动并执行一个新的服务分支。

## 关键输入 / 输出
- 输入：`subtreeHead` (`NodeReference`)。
- 输出：无。

## 成功 / 失败语义
- 分支启动并完成时成功。
- 当分支头缺失或分支执行失败时失败。

## 重要限制
- 子树受宿主服务生命周期控制。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Services/Branch.cs)
