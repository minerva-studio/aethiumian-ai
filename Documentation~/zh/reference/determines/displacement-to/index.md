# `DisplacementTo`

## 用途

计算世界坐标 XY 平面中“目标位置减自身位置”的位移，分别启用 X/Y 阈值比较。所有启用的比较都通过时，节点成功。

## 关键输入 / 输出

- `object`：必需的目标游戏对象。
- `compareX`、`compareY`：启用对应轴的比较。
- `absoluteX`、`absoluteY`：仅在比较时对该轴取绝对值。
- `xMode`、`yMode`：数值比较运算符。
- `xExpect`、`yExpect`：常量或变量阈值；仅在对应比较启用时读取。
- `storeResult`、`result`：可选存储一个原始带符号的 `Vector2`，不归一化，也不取绝对值。

节点使用通用的判断节点 Inspector。地面近战可以启用两轴比较，对 X/Y 都取绝对值，分别比较攻击范围和高度容差。判断目标在上方以发起跳跃时，X 取绝对值，Y 保留符号并使用 `greater`。

## 成功 / 失败语义

- 目标变量未绑定时，遵循树的节点异常处理规则。
- 已绑定目标为空或已销毁时失败，不写入输出。
- 目标有效时，所有启用的轴比较都通过才成功；未启用任何比较时成功。
- 启用存储且输出已绑定时，即使比较失败，也会存储有效目标的位移。
- 每次执行先读取当前位置及启用的阈值，再写入输出。
- 零位移是有效测量值，按配置正常比较。

## 重要限制

- 使用 Transform 世界坐标，忽略 Z；测量的不是碰撞体间隙。
- 仅支持 And 组合，没有逐轴输出或长度比较。
- 此未发布节点的早期草稿使用嵌套的 `x`/`y`/`magnitude` 设置及组合模式。使用草稿的资产必须先通过 Editor 显式重新配置；不会自动迁移。

## 源码链接

[Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/DisplacementTo.cs)
