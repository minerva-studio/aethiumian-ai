# 运算节点

## 扩展基类（不计入公开目录）

- `Arithmetic`：所有算术/逻辑计算节点基类。
- `Constant`：固定返回布尔值（位于 `Runtime/Nodes/Flows/Constant.cs`，但公开名归入 Arithmetic 分类）。

## 分类节点

<a id="absolute"></a>
### [`Absolute`](arithmetic/absolute/index.md)
- 用途：计算整数、浮点、向量分量的绝对值。

<a id="add"></a>
### [`Add`](arithmetic/add/index.md)
- 用途：对两个值执行加法，或对字符串拼接。

<a id="and"></a>
### [`And`](arithmetic/and/index.md)
- 用途：逻辑与。

<a id="arccosine"></a>
### [`Arccosine`](arithmetic/arccosine/index.md)
- 用途：计算反余弦。

<a id="arcsine"></a>
### [`Arcsine`](arithmetic/arcsine/index.md)
- 用途：反正弦。

<a id="arctangent"></a>
### [`Arctangent`](arithmetic/arctangent/index.md)
- 用途：计算反正切。

<a id="arctangent-2"></a>
### [`Arctangent2`](arithmetic/arctangent-2/index.md)
- 用途：由 __参数0__ 计算反正切角。

<a id="assign"></a>
### [`Assign`](arithmetic/assign/index.md)
- 用途：将源值写入可写目标变量。

<a id="boolean"></a>
### [`Boolean`](arithmetic/boolean/index.md)
- 用途：读取布尔变量。

<a id="compare"></a>
### [`Compare`](arithmetic/compare/index.md)
- 用途：按选定模式比较两个数值。

<a id="constant"></a>
### [`Constant`](arithmetic/constant/index.md)
- 用途：返回固定布尔值。

<a id="copy"></a>
### [`Copy`](arithmetic/copy/index.md)
- 用途：将变量字段值复制到另一个变量引用。

<a id="cosine"></a>
### [`Cosine`](arithmetic/cosine/index.md)
- 用途：正余弦中的余弦计算。

<a id="create-vector-2"></a>
### [`CreateVector2`](arithmetic/create-vector-2/index.md)
- 用途：从数值分量构建 参数2。

<a id="create-vector-3"></a>
### [`CreateVector3`](arithmetic/create-vector-3/index.md)
- 用途：执行该节点定义的核心行为。

<a id="cross"></a>
### [`Cross`](arithmetic/cross/index.md)
- 用途：计算叉积。

<a id="direction-to"></a>
### [`DirectionTo`](arithmetic/direction-to/index.md)
- 用途：计算从源点到目标点的方向。

<a id="divide"></a>
### [`Divide`](arithmetic/divide/index.md)
- 用途：执行除法。

<a id="dot"></a>
### [`Dot`](arithmetic/dot/index.md)
- 用途：计算向量点积。

<a id="magnitude"></a>
### [`Magnitude`](arithmetic/magnitude/index.md)
- 用途：计算向量模长。

<a id="multiply"></a>
### [`Multiply`](arithmetic/multiply/index.md)
- 用途：乘法与标量-向量扩展。

<a id="normalize"></a>
### [`Normalize`](arithmetic/normalize/index.md)
- 用途：向量归一化。

<a id="not"></a>
### [`Not`](arithmetic/not/index.md)
- 用途：布尔取反。

<a id="or"></a>
### [`Or`](arithmetic/or/index.md)
- 用途：对两个布尔值执行逻辑或。

<a id="random"></a>
### [`Random`](arithmetic/random/index.md)
- 用途：生成随机数/向量。

<a id="set-vector"></a>
### [`SetVector`](arithmetic/set-vector/index.md)
- 用途：更新已有向量的指定分量。

<a id="sign-change"></a>
### [`SignChange`](arithmetic/sign-change/index.md)
- 用途：按界定符判断符号是否变化。

<a id="sine"></a>
### [`Sine`](arithmetic/sine/index.md)
- 用途：计算正弦。

<a id="square-root"></a>
### [`SquareRoot`](arithmetic/square-root/index.md)
- 用途：平方根。

<a id="string-len"></a>
### [`StringLen`](arithmetic/string-len/index.md)
- 用途：测量字符串长度。

<a id="subtract"></a>
### [`Subtract`](arithmetic/subtract/index.md)
- 用途：减法。

<a id="tangent"></a>
### [`Tangent`](arithmetic/tangent/index.md)
- 用途：计算正切。

<a id="type-object"></a>
### [`TypeObject`](arithmetic/type-object/index.md)
- 用途：将类型对象写入变量。

<a id="type-of"></a>
### [`TypeOf`](arithmetic/type-of/index.md)
- 用途：读取变量运行时 .NET 类型。

<a id="vector-component"></a>
### [`VectorComponent`](arithmetic/vector-component/index.md)
- 用途：提取向量各分量到可写变量。

